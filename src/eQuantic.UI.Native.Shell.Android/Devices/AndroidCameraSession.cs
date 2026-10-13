using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Views;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// One running feed. The camera delivers YUV frames to an <see cref="ImageReader"/> on a thread of
/// its own; each is converted to RGBA, turned upright, into the one of two buffers the renderer was
/// not handed (see <see cref="FramePair"/>), and the engine uploads the published one as a texture
/// when the version moves.
/// <para>
/// UPRIGHT is the session's job, not the page's: a phone's sensor is mounted sideways, and the frame
/// is turned by the sensor's orientation less the display's rotation, read per frame, so a phone
/// turned while the preview runs keeps the picture the right way up.
/// </para>
/// <para>
/// The hardware goes when the activity leaves the screen and comes back with it. Android takes the
/// camera from an app nobody sees anyway; letting it go first is what keeps the session able to open
/// it again, and the last frame stays in the buffer meanwhile. A camera lost while the app stayed on
/// screen (to the window that had the focus) is asked for again when the focus returns. Disposing
/// stops it for good.
/// </para>
/// </summary>
internal sealed class AndroidCameraSession : ICameraSession
{
    /// <summary>A preview's resolution, as on the Apple shells: the conversion costs by the pixel.</summary>
    private const int PreferredWidth = 640;
    private const int PreferredHeight = 480;

    /// <summary>
    /// The camera's thread, one for the process and never told to quit. A thread per session would
    /// have to outlive the session by exactly as long as the camera goes on reporting about a
    /// device being closed, and one that quit sooner turns each of those reports into a message
    /// posted to a dead thread. A looper with nothing to do costs nothing.
    /// </summary>
    private static readonly Lazy<Handler> Worker = new(() =>
    {
        var thread = new HandlerThread("eq.camera");
        thread.Start();
        return new Handler(thread.Looper!);
    });

    private readonly Lock _gate = new();
    private readonly PhotonActivity _activity;
    private readonly CameraManager _manager;
    private readonly string _camera;
    private readonly int _sensorOrientation;
    private readonly bool _facesFront;
    private readonly Handler _handler = Worker.Value;
    private readonly ImageReader _reader;

    private CameraDevice? _device;
    private CameraCaptureSession? _capture;
    private Opening? _opening;   // the open this session stands on: in flight, or the one that succeeded
    private readonly FramePair _frames = new();
    private int _width;
    private int _height;
    private int _version;
    private bool _suspended;   // the activity is off the screen: no camera until it is back
    private bool _disposed;

    private AndroidCameraSession(PhotonActivity activity, CameraManager manager, string camera)
    {
        _activity = activity;
        _manager = manager;
        _camera = camera;

        var characteristics = manager.GetCameraCharacteristics(camera);
        _sensorOrientation = (characteristics.Get(CameraCharacteristics.SensorOrientation) as Java.Lang.Integer)?.IntValue() ?? 0;
        _facesFront = (characteristics.Get(CameraCharacteristics.LensFacing) as Java.Lang.Integer)?.IntValue() == (int)LensFacing.Front;
        var size = ChooseSize(characteristics);

        // Two images: the one being converted, and the next the camera is already filling.
        _reader = ImageReader.NewInstance(size.Width, size.Height, ImageFormatType.Yuv420888, 2);
        _reader.SetOnImageAvailableListener(new FrameListener(this), _handler);
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public int FrameWidth => _width;
    public int FrameHeight => _height;
    public byte[]? FrameBytes => _frames.Published;
    public int FrameVersion => _version;

    /// <summary>Opens the camera and waits for its first configured session. Null when the device
    /// does not answer.</summary>
    internal static async Task<ICameraSession?> OpenAsync(PhotonActivity activity, CameraManager manager,
        string camera, CancellationToken cancellationToken)
    {
        AndroidCameraSession session;
        try
        {
            session = new AndroidCameraSession(activity, manager, camera);
        }
        catch (CameraAccessException)
        {
            return null;
        }

        // Before the camera opens: an activity that leaves the screen while it is opening must be
        // heard, or the camera would open behind it.
        activity.Stopped += session.Suspend;
        activity.Started += session.Resume;
        activity.Resumed += session.Resume;

        bool opened;
        try
        {
            opened = await session.OpenDeviceAsync().WaitAsync(cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            // The wait was abandoned and the open was not: without this the camera would open with
            // nobody holding the session that owns it.
            session.Dispose();
            throw;
        }

        if (opened) return session;
        session.Dispose();
        return null;
    }

    public ImageData? Capture()
    {
        byte[] snapshot;
        int width, height;
        lock (_gate)
        {
            if (_frames.Published is not { } frame || _width == 0) return null;
            snapshot = (byte[])frame.Clone();
            width = _width;
            height = _height;
        }

        // ARGB_8888 is R, G, B, A in memory, which is the buffer's own order.
        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
        using var pixels = Java.Nio.ByteBuffer.Wrap(snapshot);
        bitmap.CopyPixelsFromBuffer(pixels);
        using var encoded = new MemoryStream();
        return bitmap.Compress(Bitmap.CompressFormat.Jpeg!, 90, encoded)
            ? new ImageData(encoded.ToArray(), "image/jpeg", width, height)
            : null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _activity.Stopped -= Suspend;
        _activity.Started -= Resume;
        _activity.Resumed -= Resume;
        Release();
        // Under the gate: a frame being converted holds it, and closing the reader closes the image
        // that frame is still reading.
        lock (_gate) _reader.Close();
    }

    // ---- the device ------------------------------------------------------------------------------

    /// <summary>
    /// One attempt to open the camera, from the request to a configured capture session. Every
    /// callback of the platform carries the attempt it belongs to, and counts only while that
    /// attempt is still the session's own.
    /// <para>
    /// Without it the callbacks spoke for "the session", and the activity can leave the screen at
    /// any point of an open: a release in the middle left the open pending with nobody to finish
    /// it, so no later return to the screen asked for the camera again and a first start waited
    /// for ever. Now whoever gives the camera up settles the attempt it abandons, and whatever
    /// that attempt still has to say arrives for an attempt that is no longer current.
    /// </para>
    /// </summary>
    private sealed class Opening
    {
        private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>True once a capture session is running, false when the attempt ended without one.</summary>
        public Task<bool> Result => _result.Task;

        /// <summary>The first answer is the answer: an attempt abandoned and then heard from again
        /// stays abandoned.</summary>
        public void Settle(bool opened) => _result.TrySetResult(opened);
    }

    /// <summary>Asks for the camera, or answers with the open already under way or done.</summary>
    private Task<bool> OpenDeviceAsync()
    {
        Opening opening;
        lock (_gate)
        {
            if (_disposed || _suspended) return Task.FromResult(false);
            if (_opening is { } standing) return standing.Result;
            _opening = opening = new Opening();
        }

        try
        {
            _manager.OpenCamera(_camera, new DeviceCallback(this, opening), _handler);
        }
        catch (Exception failure) when (failure is CameraAccessException or Java.Lang.SecurityException)
        {
            // In use by another app, disabled by policy, or the permission was taken back. Ended
            // like any other failure, so the next time the activity comes back it may try again.
            Failed(opening);
        }
        return opening.Result;
    }

    private void Opened(Opening opening, CameraDevice device)
    {
        bool current;
        lock (_gate)
        {
            current = ReferenceEquals(_opening, opening);
            if (current) _device = device;
        }
        if (!current)
        {
            // The session gave this attempt up while the camera was opening (the activity left the
            // screen, or the session was disposed): the device has nobody to belong to.
            device.Close();
            return;
        }

        try
        {
            var callback = new CaptureCallback(this, opening);
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                device.CreateCaptureSession(new SessionConfiguration((int)SessionType.Regular,
                    [new OutputConfiguration(_reader.Surface!)], new HandlerExecutor(_handler), callback));
            }
            else
            {
                device.CreateCaptureSession([_reader.Surface!], callback, _handler);
            }
        }
        catch (Exception failure) when (failure is CameraAccessException
            or Java.Lang.IllegalStateException or Java.Lang.IllegalArgumentException)
        {
            // The device was closed under this call (the activity left, or the session was
            // disposed, between the gate and here), which the camera reports as an illegal state
            // and the closed reader's surface as an illegal argument.
            Failed(opening);
        }
    }

    private void Configured(Opening opening, CameraCaptureSession capture)
    {
        CameraDevice? device;
        lock (_gate)
        {
            device = ReferenceEquals(_opening, opening) ? _device : null;
            if (device is not null) _capture = capture;
        }
        if (device is null)
        {
            // Given up between the device and its capture session. Whoever gave it up settled it.
            capture.Close();
            return;
        }

        try
        {
            var request = device.CreateCaptureRequest(CameraTemplate.Preview);
            request.AddTarget(_reader.Surface!);
            capture.SetRepeatingRequest(request.Build(), null, _handler);
            opening.Settle(true);
        }
        catch (Exception failure) when (failure is CameraAccessException or Java.Lang.IllegalStateException)
        {
            Failed(opening);
        }
    }

    /// <summary>The device did not open, was taken away, or refused its capture session. Said by an
    /// attempt the session has already left, it changes nothing.</summary>
    private void Failed(Opening opening) => Release(opening);

    /// <summary>
    /// Lets the hardware go and keeps the session: the reader, the thread and the last frame stay.
    /// The open the session stood on ends here too, so the next return to the screen starts
    /// another. Given an attempt, it does this only while that attempt is the session's own.
    /// </summary>
    private void Release(Opening? only = null)
    {
        CameraCaptureSession? capture;
        CameraDevice? device;
        Opening? ended;
        lock (_gate)
        {
            if (only is not null && !ReferenceEquals(_opening, only)) return;
            capture = _capture;
            device = _device;
            ended = _opening;
            _capture = null;
            _device = null;
            _opening = null;
        }
        try { capture?.Close(); } catch (Java.Lang.IllegalStateException) { /* the device went first */ }
        device?.Close();
        ended?.Settle(false);
    }

    private void Suspend()
    {
        // Said before the release, so that nothing asks for the camera in between.
        lock (_gate) _suspended = true;
        Release();
    }

    private void Resume()
    {
        lock (_gate) _suspended = false;
        _ = OpenDeviceAsync();   // answers at once when the camera is open or already opening
    }

    // ---- frames ----------------------------------------------------------------------------------

    private void FrameArrived(ImageReader reader)
    {
        var turns = QuarterTurns();

        // The whole frame under the gate. Dispose closes the reader under it too, and a closed
        // reader has closed its images: read outside, a frame in the middle of its conversion
        // would find its image gone, on a thread where nothing catches that.
        lock (_gate)
        {
            if (_disposed) return;

            // Every Java object here is disposed as soon as it is read. Each holds a global
            // reference until a collection runs, and this path allocates nothing managed to cause
            // one: thirty frames a second would fill the table of references instead.
            using var image = reader.AcquireLatestImage();
            if (image is null) return;
            var planes = image.GetPlanes();
            try
            {
                if (planes is not { Length: 3 }) return;
                var (width, height) = Yuv420.Turned(image.Width, image.Height, turns);

                // Into the array the renderer was not handed, and published only when it is whole:
                // the render thread uploads the published frame with no lock on this one.
                Yuv420.ToRgba(Bytes(planes[0]), planes[0].RowStride,
                    Bytes(planes[1]), Bytes(planes[2]), planes[1].RowStride, planes[1].PixelStride,
                    image.Width, image.Height, turns, _frames.Next(width * height * 4));
                _width = width;
                _height = height;
                _frames.Publish();
                _version++;
            }
            finally
            {
                if (planes is not null)
                    foreach (var plane in planes) plane.Dispose();
                image.Close();
            }
        }
    }

    /// <summary>A plane's bytes where the camera wrote them, with no copy. The memory is the
    /// image's, and stays until the image closes; the buffer object that names it is let go here.</summary>
    private static unsafe ReadOnlySpan<byte> Bytes(global::Android.Media.Image.Plane plane)
    {
        using var buffer = plane.Buffer!;
        return new ReadOnlySpan<byte>((void*)buffer.GetDirectBufferAddress(), buffer.Capacity());
    }

    /// <summary>
    /// How far the frame has to turn to stand upright on the display as it is now. A back camera
    /// turns with the sensor less the display; a front one, which looks the other way, with their sum.
    /// </summary>
    private int QuarterTurns()
    {
        var display = (int)DisplayRotation() * 90;
        var degrees = _facesFront ? (_sensorOrientation + display) % 360 : (_sensorOrientation - display + 360) % 360;
        return degrees / 90;
    }

    private SurfaceOrientation DisplayRotation()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            return _activity.Display?.Rotation ?? SurfaceOrientation.Rotation0;
        return _activity.WindowManager?.DefaultDisplay?.Rotation ?? SurfaceOrientation.Rotation0;
    }

    /// <summary>640×480 when the camera offers it, otherwise the size nearest to it in pixels.</summary>
    private static global::Android.Util.Size ChooseSize(CameraCharacteristics characteristics)
    {
        var preferred = new global::Android.Util.Size(PreferredWidth, PreferredHeight);
        var map = characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap) as StreamConfigurationMap;
        var sizes = map?.GetOutputSizes((int)ImageFormatType.Yuv420888);
        if (sizes is not { Length: > 0 }) return preferred;

        const int target = PreferredWidth * PreferredHeight;
        return sizes.MinBy(size => Math.Abs(size.Width * size.Height - target))!;
    }

    // ---- the platform's callbacks ----------------------------------------------------------------

    private sealed class DeviceCallback(AndroidCameraSession session, Opening opening) : CameraDevice.StateCallback
    {
        public override void OnOpened(CameraDevice camera) => session.Opened(opening, camera);

        public override void OnDisconnected(CameraDevice camera)
        {
            camera.Close();
            session.Failed(opening);
        }

        public override void OnError(CameraDevice camera, CameraError error)
        {
            camera.Close();
            session.Failed(opening);
        }
    }

    private sealed class CaptureCallback(AndroidCameraSession session, Opening opening) : CameraCaptureSession.StateCallback
    {
        public override void OnConfigured(CameraCaptureSession capture) => session.Configured(opening, capture);

        public override void OnConfigureFailed(CameraCaptureSession capture) => session.Failed(opening);
    }

    /// <summary>
    /// The camera's thread as the executor a session configuration asks for. The platform has one
    /// (<c>android.os.HandlerExecutor</c>, API 28), and the .NET binding does not carry it.
    /// </summary>
    private sealed class HandlerExecutor(Handler handler) : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        public void Execute(Java.Lang.IRunnable? command)
        {
            if (command is not null) handler.Post(command);
        }
    }

    private sealed class FrameListener(AndroidCameraSession session)
        : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader is not null) session.FrameArrived(reader);
        }
    }
}
