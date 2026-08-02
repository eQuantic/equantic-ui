using Android.Content.PM;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
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
/// Presentation goes through the NORMATIVE Reference backend for now: pixel-correct by definition,
/// and slower than the GPU path. The Vulkan swapchain replaces this method and nothing above it —
/// the display list, the layout and the trees never learn which one drew them.
/// </para>
/// </summary>
[Activity(
    // No Label: the launcher shows the APPLICATION's name, which the SDK sets from ApplicationTitle
    // — the same property the iOS head and the web manifest use, stated once by an app that cares.
    // NoActionBar because the app draws its OWN chrome: a system title bar would sit on top of a
    // header the design already accounts for, and no tree here would know it was there.
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.Density)]
public sealed class PhotonActivity : Activity, ISurfaceHolderCallback, Choreographer.IFrameCallback
{
    private SurfaceView? _view;
    private PhotonHost? _host;
    private ReferenceBackend? _backend;
    private Bitmap? _bitmap;
    private long _startedAt;

    /// <summary>Frames actually presented — the self-test's exit evidence, as on every other shell.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>The app the runner built. Static because Android constructs the Activity itself.</summary>
    internal static PhotonApplication? Application { get; set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

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

        _backend = new ReferenceBackend();
        _host = new PhotonHost(app.Root(), app.Options.Theme, Mode(), metrics.WidthPixels / scale,
            metrics.HeightPixels / scale, text)
        {
            RenderScale = scale,
            TextRasterizer = text,
            IconRasterizer = new AndroidIconRasterizer(),
        };

        Choreographer.Instance!.PostFrameCallback(this);
    }

    /// <summary>The surface's real size arrives here, and again on every rotation.</summary>
    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
    {
        if (_host is null) return;
        var scale = Resources!.DisplayMetrics!.Density;
        _host.Resize(width / scale, height / scale);
        ApplyInsets(scale);

        _bitmap?.Dispose();
        _bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        Choreographer.Instance!.RemoveFrameCallback(this);
        _bitmap?.Dispose();
        _bitmap = null;
        _backend?.Dispose();
        _backend = null;
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
        if (_host is null || _backend is null || _bitmap is null || _view?.Holder is not { } holder) return;

        var forced = Application?.Options.MaxFrames > 0;
        if (!_host.NeedsRender && forced != true) return;   // a still screen presents nothing

        // Insets can change without a resize — a keyboard, a call banner, a rotation mid-gesture.
        ApplyInsets(Resources!.DisplayMetrics!.Density);

        if (_startedAt == 0) _startedAt = frameTimeNanos;
        var builder = new DisplayListBuilder();
        _host.RenderFrame(builder, (frameTimeNanos - _startedAt) / 1_000_000f);

        using var surface = _backend.CreateSurface(_bitmap.Width, _bitmap.Height);
        _backend.Render(builder.Build(), surface);

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
            case MotionEventActions.Move:
                _host.PointerMove(x, y);
                return true;
            case MotionEventActions.Up:
                _host.PressUp(x, y);
                return true;
            // The system took the gesture away — a scroll view claimed it, a call arrived. Nothing
            // was decided, so nothing is reported and every surface returns to the caller's rest.
            case MotionEventActions.Cancel:
                _host.PointerCancel();
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
