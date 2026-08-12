using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Choreographer = Android.Views.Choreographer;
// This assembly's own namespace ends in `Android`, so an inline Android.* path would resolve against
// it. The alias keeps the platform's enum reachable without qualifying it at every use.
using UiMode = Android.Content.Res.UiMode;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The Photon surface as an Android screen. The Activity owns three things and no more — the view,
/// the touches and the clock — exactly as the iOS view controller does, and the tree above it is
/// the same C# on both.
/// <para>
/// The clock is the <see cref="Choreographer"/>, which is the display's own vsync rather than a
/// timer guessing at it, and a frame is only PRESENTED when the host says something changed. A
/// still screen costs a callback that returns immediately, which is what keeps a phone's battery
/// out of this.
/// </para>
/// <para>
/// Presentation goes through VULKAN, straight into the window's own surface — the same engine and
/// the same renderer the Metal backend runs, so the two can only differ in API calls. Where Vulkan
/// is unavailable the NORMATIVE Reference backend draws the same display list in software: correct
/// by definition, slower, and invisible to everything above it.
/// </para>
/// <para>
/// The LAUNCHER activity is generated into the app's own assembly, not this one. Android would
/// otherwise never load the app: nothing would have referenced it, so the registration that names
/// its program would never run. Subclassing is also what gives an app somewhere to override.
/// </para>
/// </summary>
public class PhotonActivity : Activity, ISurfaceHolderCallback, Choreographer.IFrameCallback
{
    private SurfaceView? _view;
    private PhotonHost? _host;
    private VulkanBackend? _vulkan;
    private VulkanSwapchain? _swapchain;
    private IntPtr _window;
    private ReferenceBackend? _reference;
    private Bitmap? _bitmap;
    private long _startedAt;

    /// <summary>Frames actually presented — the self-test's exit evidence, as on every other shell.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>The app the runner built. Static because Android constructs the Activity itself.</summary>
    internal static PhotonApplication? Application { get; set; }

    /// <summary>
    /// The activity on screen. A capability needs one to start anything — a picker, a camera, a
    /// permission prompt — and Android constructs the activity itself, so it says which it is.
    /// </summary>
    internal static PhotonActivity? Current { get; private set; }

    private readonly Dictionary<int, TaskCompletionSource<Intent?>> _pending = new();
    private int _nextRequest = 1;

    /// <summary>
    /// Starts something and AWAITS what comes back. Android answers activity results through a
    /// callback on the activity, which is a shape no capability can expose to an app — so it is
    /// bridged here, once, and every capability that starts an intent gets to be an ordinary
    /// awaitable method.
    /// </summary>
    internal Task<Intent?> PickAsync(Intent intent, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<Intent?>();
        int request;
        lock (_pending)
        {
            request = _nextRequest++;
            _pending[request] = completion;
        }

        // Cancelling stops the WAIT, not the system UI: nothing can dismiss the picker from here,
        // and pretending otherwise would leave a caller believing it had.
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        StartActivityForResult(intent, request);
        return completion.Task;
    }

    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingPermissions = new();

    /// <summary>
    /// The permission twin of <see cref="PickAsync"/>: Android answers permission requests through
    /// a callback on the activity, and this bridges it once so a capability can simply await.
    /// True = every requested permission was granted.
    /// </summary>
    internal Task<bool> RequestPermissionsAsync(string[] permissions,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<bool>();
        int request;
        lock (_pendingPermissions)
        {
            request = _nextRequest++;
            _pendingPermissions[request] = completion;
        }
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        RequestPermissions(permissions, request);
        return completion.Task;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
        global::Android.Content.PM.Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        TaskCompletionSource<bool>? completion;
        lock (_pendingPermissions)
        {
            if (!_pendingPermissions.Remove(requestCode, out completion)) return;
        }
        var granted = grantResults.Length > 0
            && grantResults.All(g => g == global::Android.Content.PM.Permission.Granted);
        completion.TrySetResult(granted);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        TaskCompletionSource<Intent?>? completion;
        lock (_pending)
        {
            if (!_pending.Remove(requestCode, out completion)) return;
        }
        // Cancelled and "came back with nothing" are the same answer to a caller: no picture.
        completion.TrySetResult(resultCode == Result.Ok ? data : null);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Current = this;

        _view = new SurfaceView(this);
        _view.Holder!.AddCallback(this);
        SetContentView(_view);
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        // Android launched us; nobody ran a Main. The app describes itself in a CreateApp the
        // host can find, which is the same method every other platform's entry point calls.
        var app = Application ??= PhotonApplication.Create();

        var metrics = Resources!.DisplayMetrics!;
        var scale = metrics.Density;
        var text = new AndroidTextService();

        // The window behind the Surface is what Vulkan draws at. Holding it keeps the surface
        // alive for as long as the swapchain does.
        _window = AndroidWindow.FromSurface(holder.Surface!);
        if (_window != IntPtr.Zero && VulkanBackend.IsSupported)
        {
            _vulkan = new VulkanBackend();
        }
        else
        {
            _reference = new ReferenceBackend();
        }

        // Which one drew the frame is not a detail to guess at from a screenshot.
        global::Android.Util.Log.Info("Photon", _vulkan is not null
            ? "presenting through Vulkan"
            : "presenting through the Reference backend (no Vulkan surface)");

        // The device's locale, copied onto .NET before the first realize (I18N-PLAN D5/W6):
        // Android's Locale.getDefault() carries language and region in one, so the D13 pair
        // collapses to a single culture here.
        var cultureController = app.Services.GetService(typeof(PhotonCultureController)) as PhotonCultureController;
        cultureController?.Apply(AndroidLocale.Resolve());

        _host = new PhotonHost(app.Root(), app.Options.Theme, Mode(), metrics.WidthPixels / scale,
            metrics.HeightPixels / scale, text)
        {
            RecycleFrames = true,   // production owns its frames (LayoutNodePool)
            RenderScale = scale,
            TextRasterizer = text,
            IconRasterizer = new AndroidIconRasterizer(),
            // A phone rolls too: a scroll that jumps reads as a redraw there just as on a desktop.
            SmoothScroll = app.Options.SmoothScroll,
        };
        cultureController?.Attach(_host.Invalidate);

        Choreographer.Instance!.PostFrameCallback(this);
    }

    /// <summary>The surface's real size arrives here, and again on every rotation.</summary>
    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
    {
        if (_host is null) return;
        var scale = Resources!.DisplayMetrics!.Density;
        _host.Resize(width / scale, height / scale);
        ApplyInsets(scale);

        if (_vulkan is not null)
        {
            if (_swapchain is null) _swapchain = _vulkan.CreateSwapchain(_window, width, height);
            else _swapchain.Resize(width, height);
            return;
        }

        _bitmap?.Dispose();
        _bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        Choreographer.Instance!.RemoveFrameCallback(this);
        _swapchain?.Dispose();
        _swapchain = null;
        _vulkan?.Dispose();
        _vulkan = null;
        if (_window != IntPtr.Zero) AndroidWindow.Release(_window);
        _window = IntPtr.Zero;
        _bitmap?.Dispose();
        _bitmap = null;
        _reference?.Dispose();
        _reference = null;
        _host = null;
    }

    /// <summary>
    /// The system's own margins — the status bar, the gesture handle, a cutout. A SafeArea node lays
    /// out against what the DEVICE reserves, which is the same contract the iOS shell honours and
    /// the reason the Wallet's trees need no change to sit correctly on either.
    /// </summary>
    private void ApplyInsets(float scale)
    {
        if (_host is null || _view?.RootWindowInsets is not { } insets) return;
        var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
        _host.SafeAreaInsets = new EdgeInsets(
            bars.Left / scale, bars.Top / scale, bars.Right / scale, bars.Bottom / scale);
    }

    // ---- The clock ------------------------------------------------------------------------------

    public void DoFrame(long frameTimeNanos)
    {
        Choreographer.Instance!.PostFrameCallback(this);
        if (_host is null || _view?.Holder is not { } holder) return;

        var forced = Application?.Options.MaxFrames > 0;
        if (_startedAt == 0) _startedAt = frameTimeNanos;
        var nowMs = (frameTimeNanos - _startedAt) / 1_000_000f;
        // Dirty, forced, or DUE — the caret's next blink transition (see PhotonHost.IsFrameDue).
        if (!_host.NeedsRender && !_host.IsFrameDue(nowMs) && forced != true) return;

        // Insets can change without a resize — a keyboard, a call banner, a rotation mid-gesture.
        ApplyInsets(Resources!.DisplayMetrics!.Density);

        var builder = new DisplayListBuilder();
        _host.RenderFrame(builder, nowMs);
        var displayList = builder.Build();

        if (_vulkan is not null && _swapchain is not null)
        {
            // False means the window changed shape under us — a fact about the WINDOW, not a
            // failure: rebuild against what it is now and draw the next frame into that.
            if (!_vulkan.Present(displayList, _swapchain))
            {
                _swapchain.Resize(holder.SurfaceFrame!.Width(), holder.SurfaceFrame.Height());
                return;
            }
            FramesPresented++;
        }
        else if (_reference is not null && _bitmap is not null)
        {
            using var surface = _reference.CreateSurface(_bitmap.Width, _bitmap.Height);
            _reference.Render(displayList, surface);

            // The engine speaks RGBA; an Android bitmap wants ARGB with the bytes the other way round.
            var rgba = new byte[_bitmap.Width * _bitmap.Height * 4];
            surface.ReadPixelsSrgb(rgba);
            var argb = new int[rgba.Length / 4];
            for (var i = 0; i < argb.Length; i++)
            {
                var p = i * 4;
                argb[i] = (rgba[p + 3] << 24) | (rgba[p] << 16) | (rgba[p + 1] << 8) | rgba[p + 2];
            }
            _bitmap.SetPixels(argb, 0, _bitmap.Width, 0, 0, _bitmap.Width, _bitmap.Height);

            var canvas = holder.LockCanvas();
            if (canvas is null) return;
            canvas.DrawBitmap(_bitmap, 0, 0, null);
            holder.UnlockCanvasAndPost(canvas);
            FramesPresented++;
        }

        if (forced == true && FramesPresented >= Application!.Options.MaxFrames)
        {
            Console.WriteLine($"[photon] frames presented: {FramesPresented}");
            Choreographer.Instance.RemoveFrameCallback(this);
            Finish();
        }
    }

    // ---- Touches --------------------------------------------------------------------------------

    // ONE finger owns the interaction. A second landing mid-gesture would jump the pointer between
    // two positions the host cannot tell apart, and a drag that teleports is worse than a second
    // finger that does nothing.
    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (_host is null || e is null) return false;
        var scale = Resources!.DisplayMetrics!.Density;
        var x = e.GetX() / scale;
        var y = e.GetY() / scale;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _host.PressDown(x, y);
                return true;
            // OnTouchEvent is fingers by definition (a mouse hover arrives as generic motion,
            // never here) — labelled Touch so drags and pans run and hover never does (spec S5).
            case MotionEventActions.Move:
                _host.PointerMove(x, y, PointerKind.Touch);
                return true;
            case MotionEventActions.Up:
                _host.PressUp(x, y, PointerKind.Touch);
                return true;
            // The system took the gesture away — a scroll view claimed it, a call arrived. Nothing
            // was decided, so nothing is reported and every surface returns to the caller's rest.
            case MotionEventActions.Cancel:
                _host.PointerCancel(PointerKind.Touch);
                return true;
            default:
                return base.OnTouchEvent(e);
        }
    }

    private ThemeMode Mode()
    {
        if (Application?.Options.Mode is { } forced) return forced;
        var night = Resources!.Configuration!.UiMode & UiMode.NightMask;
        return night == UiMode.NightYes ? ThemeMode.Dark : ThemeMode.Light;
    }
}
