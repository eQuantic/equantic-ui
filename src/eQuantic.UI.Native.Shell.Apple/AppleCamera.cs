using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// AVFoundation, shared by the Mac and the iPhone: an AVCaptureSession feeding BGRA sample buffers
/// through a runtime delegate class, swizzled into one reused RGBA buffer per session.
/// <para>
/// FRAMES AS PIXELS rather than a platform preview layer, deliberately. AVCaptureVideoPreviewLayer
/// would composite OUTSIDE the engine — above every clip, corner radius, transform and overlay the
/// display list draws — and a preview that ignores the page's own geometry is not a node, it is a
/// hole. One BGRA→RGBA copy per frame at 640×480 is the price of the preview being an ordinary
/// texture, and it is cheap.
/// </para>
/// </summary>
public sealed class AppleCamera : ICamera
{
    private const string AVFoundationLib = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";

    // AVAuthorizationStatus.
    private const long AuthNotDetermined = 0;
    private const long AuthDenied = 2;
    private const long AuthAuthorized = 3;

    public bool IsAvailable
    {
        get
        {
            dlopen(AVFoundationLib, 2);
            var device = Send(objc_getClass("AVCaptureDevice"),
                Sel("defaultDeviceWithMediaType:"), NSString("vide"));
            return device != IntPtr.Zero;
        }
    }

    public PermissionState Permission
    {
        get
        {
            dlopen(AVFoundationLib, 2);
            return SendLong(objc_getClass("AVCaptureDevice"),
                Sel("authorizationStatusForMediaType:"), NSString("vide")) switch
            {
                AuthNotDetermined => PermissionState.NotDetermined,
                AuthAuthorized => PermissionState.Granted,
                _ => PermissionState.Denied,
            };
        }
    }

    public async ValueTask<ICameraSession?> StartPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return null;

        if (Permission == PermissionState.NotDetermined && !await AskAsync(cancellationToken)) return null;
        if (Permission != PermissionState.Granted) return null;

        return AppleCameraSession.Open();
    }

    private static unsafe Task<bool> AskAsync(CancellationToken cancellationToken)
    {
        // The framework OWNS this block — copies it, calls it once on an arbitrary queue, releases
        // it — which is the global-block lifetime, again. The reply lets its context go.
        var pending = new PendingReply<bool>((IntPtr)(delegate* unmanaged<IntPtr, byte, void>)&AccessReplied);
        SendVoid(objc_getClass("AVCaptureDevice"),
            Sel("requestAccessForMediaType:completionHandler:"), NSString("vide"), pending.Block.Handle);

        cancellationToken.Register(() => pending.Cancel(cancellationToken));
        return pending.Task;
    }

    /// <summary>AVFoundation's answer to the ask. BOOL crosses as a byte: a bool is not blittable.</summary>
    [UnmanagedCallersOnly]
    private static void AccessReplied(IntPtr block, byte granted) =>
        ObjCBlock.Context<PendingReply<bool>>(block)?.Reply(granted != 0);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern long SendLong(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    internal static extern IntPtr dlopen(string path, int mode);
}

/// <summary>One running feed. Disposing stops the session, which turns the camera light off.</summary>
public sealed partial class AppleCameraSession : ICameraSession
{
    private static readonly Lock Gate = new();

    private IntPtr _session;
    private volatile bool _stopped;
    private byte[]? _rgba;
    private int _width;
    private int _height;
    private int _version;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public int FrameWidth => _width;
    public int FrameHeight => _height;
    public byte[]? FrameBytes => _rgba;
    public int FrameVersion => _version;

    internal static AppleCameraSession? Open()
    {
        var session = new AppleCameraSession();
        return session.Start() ? session : null;
    }

    private bool Start()
    {
        var device = Send(objc_getClass("AVCaptureDevice"),
            Sel("defaultDeviceWithMediaType:"), NSString("vide"));
        if (device == IntPtr.Zero) return false;

        var input = Send(objc_getClass("AVCaptureDeviceInput"),
            Sel("deviceInputWithDevice:error:"), device, IntPtr.Zero);
        if (input == IntPtr.Zero) return false;

        _session = Send(Send(objc_getClass("AVCaptureSession"), Sel("alloc")), Sel("init"));
        SendVoid(_session, Sel("beginConfiguration"));
        // 640×480: a preview's resolution. The swizzle cost scales with this, and a preview that
        // burns a core at 1080p to show a face is a preview nobody thanks. The preset is the
        // EXPORTED CONSTANT, dereferenced — a literal spelling of its name was silently ignored
        // and the session ran at 1080p, which the capture's own readout gave away.
        SendVoid(_session, Sel("addInput:"), input);
        // AFTER the input, which is Apple's own order: asked before one, the session says yes and
        // then quietly reverts to the device's active format when the input lands — the capture's
        // own 1920×1080 readout is how that surfaced.
        var preset = ReadAvConstant("AVCaptureSessionPreset640x480");
        if (preset != IntPtr.Zero && SendBoolArg(_session, Sel("canSetSessionPreset:"), preset))
            SendVoid(_session, Sel("setSessionPreset:"), preset);

        var output = Send(Send(objc_getClass("AVCaptureVideoDataOutput"), Sel("alloc")), Sel("init"));
        // BGRA out of the box — kCVPixelFormatType_32BGRA, by value because the constant IS the
        // fourcc. The settings key is a CoreVideo exported CFString, read through dlsym.
        var formatKey = ReadConstant("kCVPixelBufferPixelFormatTypeKey");
        var bgra = Send(objc_getClass("NSNumber"), Sel("numberWithUnsignedInt:"), (nuint)0x42475241);
        var settings = Send(objc_getClass("NSDictionary"),
            Sel("dictionaryWithObject:forKey:"), bgra, formatKey);
        SendVoid(output, Sel("setVideoSettings:"), settings);

        // This session's own delegate, which finds this session and no other. WEAK, and never
        // freed: a frame already on the camera's queue may read it after Dispose, and a freed handle
        // could hand that read whatever object took its slot. One handle per session is the price.
        var context = GCHandle.Alloc(this, GCHandleType.Weak);
        SendVoid(output, Sel("setSampleBufferDelegate:queue:"),
            DelegateClass.Value.New(context),
            dispatch_queue_create("eq.camera", IntPtr.Zero));
        SendVoid(_session, Sel("addOutput:"), output);
        SendVoid(_session, Sel("commitConfiguration"));

        // startRunning BLOCKS until the pipeline is up — off the caller's thread, always.
        var session = _session;
        Task.Run(() => SendVoid(session, Sel("startRunning")));
        return true;
    }

    public ImageData? Capture()
    {
        byte[] snapshot;
        int width, height;
        lock (Gate)
        {
            if (_rgba is null || _width == 0) return null;
            snapshot = (byte[])_rgba.Clone();
            width = _width;
            height = _height;
        }
        return new ImageData(Bmp.Encode(snapshot, width, height), "image/bmp", width, height);
    }

    public void Dispose()
    {
        _stopped = true;
        if (_session == IntPtr.Zero) return;
        var session = _session;
        _session = IntPtr.Zero;
        Task.Run(() => SendVoid(session, Sel("stopRunning")));   // blocks, like startRunning
    }

    // ---- the frame delegate --------------------------------------------------------------------

    private static readonly Lazy<ObjCDelegateClass> DelegateClass = new(DefineDelegateClass);

    // A static entry point (#795): a device build has no JIT to make a wrapper for a delegate. It
    // finds its session in the delegate object it was called on.
    private static unsafe ObjCDelegateClass DefineDelegateClass() => ObjCDelegateClass.Define("EQCameraDelegate",
        ("captureOutput:didOutputSampleBuffer:fromConnection:",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidOutput, "v@:@@@"));

    [UnmanagedCallersOnly]
    private static void DidOutput(IntPtr self, IntPtr selector, IntPtr output, IntPtr sampleBuffer, IntPtr connection) =>
        DelegateClass.Value.Context<AppleCameraSession>(self)?.FrameArrived(sampleBuffer);

    private void FrameArrived(IntPtr sampleBuffer)
    {
        if (_stopped) return;   // a frame that was already on the queue when the session stopped

        var pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer);
        if (pixelBuffer == IntPtr.Zero) return;

        CVPixelBufferLockBaseAddress(pixelBuffer, 1 /* readOnly */);
        try
        {
            var width = (int)CVPixelBufferGetWidth(pixelBuffer);
            var height = (int)CVPixelBufferGetHeight(pixelBuffer);
            var stride = (int)CVPixelBufferGetBytesPerRow(pixelBuffer);
            var basePtr = CVPixelBufferGetBaseAddress(pixelBuffer);
            if (basePtr == IntPtr.Zero || width == 0) return;

            lock (Gate)
            {
                if (_rgba is null || _width != width || _height != height)
                {
                    _rgba = new byte[width * height * 4];
                    _width = width;
                    _height = height;
                }

                // BGRA → RGBA, row by row (the stride may be padded past the row).
                unsafe
                {
                    var source = (byte*)basePtr;
                    fixed (byte* target = _rgba)
                    {
                        for (var y = 0; y < height; y++)
                        {
                            var row = source + y * stride;
                            var line = target + y * width * 4;
                            for (var x = 0; x < width; x++)
                            {
                                line[x * 4 + 0] = row[x * 4 + 2];
                                line[x * 4 + 1] = row[x * 4 + 1];
                                line[x * 4 + 2] = row[x * 4 + 0];
                                line[x * 4 + 3] = 255;
                            }
                        }
                    }
                }
                _version++;
            }
        }
        finally
        {
            CVPixelBufferUnlockBaseAddress(pixelBuffer, 1);
        }
    }

    private static IntPtr ReadConstant(string symbol)
    {
        AppleCamera.dlopen("/System/Library/Frameworks/CoreVideo.framework/CoreVideo", 2);
        var address = dlsym(new IntPtr(-2) /* RTLD_DEFAULT */, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    private static IntPtr ReadAvConstant(string symbol)
    {
        var address = dlsym(new IntPtr(-2) /* RTLD_DEFAULT */, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBoolArg(IntPtr receiver, IntPtr selector, IntPtr arg1);

    private const string CoreMediaLib = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
    private const string CoreVideoLib = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";

    [DllImport(CoreMediaLib)]
    private static extern IntPtr CMSampleBufferGetImageBuffer(IntPtr sampleBuffer);

    [DllImport(CoreVideoLib)]
    private static extern int CVPixelBufferLockBaseAddress(IntPtr pixelBuffer, ulong flags);

    [DllImport(CoreVideoLib)]
    private static extern int CVPixelBufferUnlockBaseAddress(IntPtr pixelBuffer, ulong flags);

    [DllImport(CoreVideoLib)]
    private static extern IntPtr CVPixelBufferGetBaseAddress(IntPtr pixelBuffer);

    [DllImport(CoreVideoLib)]
    private static extern nuint CVPixelBufferGetWidth(IntPtr pixelBuffer);

    [DllImport(CoreVideoLib)]
    private static extern nuint CVPixelBufferGetHeight(IntPtr pixelBuffer);

    [DllImport(CoreVideoLib)]
    private static extern nuint CVPixelBufferGetBytesPerRow(IntPtr pixelBuffer);

    [DllImport("/usr/lib/system/libdispatch.dylib")]
    private static extern IntPtr dispatch_queue_create(string label, IntPtr attributes);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);
}

/// <summary>
/// The smallest still-image encoder that every decoder on every target reads: an uncompressed
/// 32-bit BMP. A camera capture is a demo artifact and an input to the app's own pipeline, not an
/// archival format — and a BMP needs no codec on either side of the crossing.
/// </summary>
public static class Bmp
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        const int headerSize = 14 + 40;
        var stride = width * 4;
        var file = new byte[headerSize + stride * height];

        file[0] = (byte)'B';
        file[1] = (byte)'M';
        Write32(file, 2, file.Length);
        Write32(file, 10, headerSize);
        Write32(file, 14, 40);                 // BITMAPINFOHEADER
        Write32(file, 18, width);
        Write32(file, 22, height);             // positive = bottom-up, handled by the row flip below
        file[26] = 1;                          // planes
        file[28] = 32;                         // bpp
        Write32(file, 34, stride * height);

        for (var y = 0; y < height; y++)
        {
            var source = y * stride;
            var target = headerSize + (height - 1 - y) * stride;
            for (var x = 0; x < width; x++)
            {
                file[target + x * 4 + 0] = rgba[source + x * 4 + 2];   // BMP stores BGRA
                file[target + x * 4 + 1] = rgba[source + x * 4 + 1];
                file[target + x * 4 + 2] = rgba[source + x * 4 + 0];
                file[target + x * 4 + 3] = rgba[source + x * 4 + 3];
            }
        }
        return file;
    }

    private static void Write32(byte[] target, int offset, int value)
    {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)(value >> 16);
        target[offset + 3] = (byte)(value >> 24);
    }
}
