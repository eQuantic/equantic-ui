using CoreAnimation;
using CoreGraphics;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using Foundation;
using Metal;
using ObjCRuntime;
using UIKit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;
// Both the shared shell and UIKit define the Core Graphics value types. On iOS the platform's own
// win: they are what every UIKit call hands back.
using CGPoint = CoreGraphics.CGPoint;
using CGSize = CoreGraphics.CGSize;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The Photon surface as an iOS screen. UIKit owns three things and no more — the view, the touches
/// and the clock — while the drawable is driven by the SAME messages the macOS window sends, so the
/// pixels come off one engine on both platforms rather than two that agree by inspection.
/// <para>
/// The clock is a <see cref="CADisplayLink"/> synchronised to the panel, but a frame is only
/// PRESENTED when the host says something changed. A still screen costs a callback that returns
/// immediately, which is what keeps a phone's battery out of this.
/// </para>
/// <para>
/// The system's own margins — the notch, the home indicator, a call banner — arrive as
/// <see cref="PhotonHost.SafeAreaInsets"/>, so a <c>SafeArea</c> node lays out against what the
/// device actually reserves instead of numbers guessed at build time.
/// </para>
/// </summary>
[Register("PhotonViewController")]
public sealed class PhotonViewController : UIViewController
{
    private readonly VisualNode _root;
    private readonly IAppTheme _theme;
    private readonly ThemeMode? _forcedMode;

    private MetalBackend? _backend;
    private PhotonHost? _host;
    private CADisplayLink? _clock;
    private CoreTextService? _text;
    private double _startedAt;
    private nfloat _lastWidth;
    private nfloat _lastHeight;

    /// <param name="forcedMode">
    /// Null follows the system's light/dark setting, which is what an app should do; a value pins
    /// the mode, which is what a screenshot test wants.
    /// </param>
    public PhotonViewController(VisualNode root, IAppTheme theme, ThemeMode? forcedMode = null)
    {
        _root = root;
        _theme = theme;
        _forcedMode = forcedMode;
    }

    public PhotonViewController(NativeHandle handle) : base(handle) =>
        throw new NotSupportedException("PhotonViewController is built in code, not from a storyboard.");

    /// <summary>
    /// Whether a scroll GLIDES to where it was sent instead of jumping. On by default on every
    /// surface — a phone rolls too, and a jump reads as a redraw there just as it does on a
    /// desktop. Reduce Motion turns it off whatever this says.
    /// </summary>
    public bool SmoothScroll { get; set; } = true;

    /// <summary>Frames actually presented — the self-test's exit evidence, as on macOS.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>Stops after this many presents, when positive. Zero runs until the app closes.</summary>
    public int MaxFrames { get; init; }

    public override void LoadView() => View = new PhotonView();

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        _backend = new MetalBackend();
        var view = (PhotonView)View!;
        var layer = view.MetalLayer;
        layer.Device = Runtime.GetINativeObject<IMTLDevice>(_backend.DeviceHandle, owns: false);
        layer.PixelFormat = (MTLPixelFormat)MetalBackend.PixelFormatBgra8UnormSrgb;
        layer.FramebufferOnly = true;

        // Rasterise at the panel's own density: layout stays in dp, glyphs and icons land at native
        // resolution. A phone is where the difference between 2x and 3x is a whole readable step.
        var scale = (float)UIScreen.MainScreen.Scale;
        layer.ContentsScale = scale;

        // The platform's locale, copied onto .NET before the first realize (I18N-PLAN D5/W6) —
        // NSLocale carries the D13 pair natively. The statics-only write: PhotonApp.Run does not
        // carry services, so the controller instance (repaint-on-switch) joins when it does, the
        // same fence the theme controller lives behind on this shell.
        var (uiCulture, formatCulture) = AppleLocale.Resolve();
        eQuantic.UI.Native.Hosting.PhotonCultureController.ApplyToProcess(uiCulture, formatCulture);

        _text = new CoreTextService();
        _host = new PhotonHost(_root, _theme, Mode(), (float)view.Bounds.Width, (float)view.Bounds.Height, _text)
        {
            RecycleFrames = true,   // production owns its frames (LayoutNodePool)
            RenderScale = scale,
            TextRasterizer = _text,
            ImageLoader = new CoreGraphicsImageLoader(),
            IconRasterizer = new CoreGraphicsIconRasterizer(),
            ReducedMotion = UIAccessibility.IsReduceMotionEnabled,
            SmoothScroll = SmoothScroll,
        };

        _clock = CADisplayLink.Create(OnFrame);
        _clock.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
    }

    /// <summary>
    /// The layout pass is where the surface's real size and the system's margins are finally known —
    /// a rotation, a split view, a call banner all arrive here and nowhere else.
    /// </summary>
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        if (_host is null || View is not PhotonView view) return;

        var bounds = view.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var insets = view.SafeAreaInsets;
        _host.SafeAreaInsets = new EdgeInsets(
            (float)insets.Left, (float)insets.Top, (float)insets.Right, (float)insets.Bottom);

        if (bounds.Width == _lastWidth && bounds.Height == _lastHeight) return;
        _lastWidth = bounds.Width;
        _lastHeight = bounds.Height;

        var scale = view.MetalLayer.ContentsScale;
        view.MetalLayer.DrawableSize = new CGSize(bounds.Width * scale, bounds.Height * scale);
        _host.Resize((float)bounds.Width, (float)bounds.Height);
    }

    /// <summary>Following the system means re-rendering when it changes, not only at launch.</summary>
    public override void TraitCollectionDidChange(UITraitCollection? previous)
    {
        base.TraitCollectionDidChange(previous);
        if (_host is null || _forcedMode is not null) return;
        if (_host.Mode == Mode()) return;
        _host.Mode = Mode();
        _host.Invalidate();
    }

    private ThemeMode Mode() => _forcedMode
        ?? (TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? ThemeMode.Dark : ThemeMode.Light);

    // ---- Touches --------------------------------------------------------------------------------

    // ONE finger owns the interaction. A second landing mid-gesture would otherwise jump the
    // pointer between two positions the host has no way to tell apart — and a drag that teleports
    // is worse than a second finger that does nothing.
    private UITouch? _tracking;

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        if (_tracking is not null || _host is null) return;
        if (touches.AnyObject is not UITouch touch) return;
        _tracking = touch;
        var point = touch.LocationInView(View);
        _host.PressDown((float)point.X, (float)point.Y);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        if (Tracked(touches) is not { } point || _host is null) return;
        // A finger, labelled as one: drags and pans run; hover never does (spec S5).
        _host.PointerMove((float)point.X, (float)point.Y, PointerKind.Touch);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        if (Tracked(touches) is not { } point || _host is null) return;
        _tracking = null;
        _host.PressUp((float)point.X, (float)point.Y, PointerKind.Touch);
    }

    /// <summary>
    /// The system took the touch away — a scroll view claimed it, a call arrived, the app went to
    /// the background. Nothing was decided, so nothing is reported and every surface returns to
    /// where its caller last put it.
    /// </summary>
    public override void TouchesCancelled(NSSet touches, UIEvent? evt)
    {
        if (Tracked(touches) is null || _host is null) return;
        _tracking = null;
        _host.PointerCancel(PointerKind.Touch);
    }

    /// <summary>The position of the finger we are tracking, or null when this set is not about it.</summary>
    private CGPoint? Tracked(NSSet touches)
    {
        if (_tracking is null) return null;
        foreach (var candidate in touches)
        {
            if (!ReferenceEquals(candidate, _tracking)) continue;
            return _tracking.LocationInView(View);
        }
        return null;
    }

    // ---- The clock ------------------------------------------------------------------------------

    private void OnFrame()
    {
        if (_host is null || _backend is null || View is not PhotonView view) return;

        var forced = MaxFrames > 0;
        if (_startedAt == 0) _startedAt = _clock!.Timestamp;
        var nowMs = (float)((_clock!.Timestamp - _startedAt) * 1000);
        // Dirty, forced, or DUE — the caret's next blink transition. The vsync tick itself keeps
        // firing either way; what it skips is the render and the present.
        if (!_host.NeedsRender && !_host.IsFrameDue(nowMs) && !forced) return;

        var drawable = Send(view.MetalLayer.Handle, Sel("nextDrawable"));
        if (drawable == IntPtr.Zero) return;         // the pool is empty this vsync; try the next

        var builder = new DisplayListBuilder();
        _host.RenderFrame(builder, nowMs);
        _backend.RenderToDrawable(builder.Build(), Send(drawable, Sel("texture")),
            MetalBackend.PixelFormatBgra8UnormSrgb, drawable);
        FramesPresented++;

        if (forced && FramesPresented >= MaxFrames) Stop();
    }

    private void Stop()
    {
        _clock?.RemoveFromRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        _clock?.Invalidate();
        _clock = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _backend?.Dispose();
            _backend = null;
        }
        base.Dispose(disposing);
    }
}
