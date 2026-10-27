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
/// <para>
/// The KEYBOARD follows the host's edit (<see cref="PhotonKeyboard"/>): it opens when a field, a code
/// surface or a sheet takes the caret and closes when it lets go, and the tree lays out above it.
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
    private PhotonKeyboard? _keyboard;
    private double _startedAt;
    private nfloat _lastWidth;
    private nfloat _lastHeight;

    /// <param name="root">The tree this controller presents.</param>
    /// <param name="theme">The app's theme, which every measurement and every paint reads.</param>
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
            // What ⌘C, X and V on a keyboard attached to the device copy, cut and paste through.
            Clipboard = new IosClipboard(),
        };

        // The semantics bridge: VoiceOver reads the SAME target-neutral tree the macOS bridge
        // does, and its gestures reach the same handlers a touch does — by the node's id, like
        // everything else that has to survive the next frame.
        view.Semantics = new PhotonAccessibility(view,
            () => _host?.Semantics() ?? [],
            id => _host?.Activate(id) ?? false,
            (id, direction) => _host?.Adjust(id, direction) ?? false);

        _keyboard = new PhotonKeyboard(view, _host);

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

    /// <summary>What the tracked touch is, read when it lands and kept for the whole gesture.</summary>
    private PointerKind _kind = PointerKind.Touch;

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        if (_tracking is not null || _host is null) return;
        if (touches.AnyObject is not UITouch touch) return;
        _tracking = touch;
        _kind = KindOf(touch);
        var point = touch.LocationInView(View);
        _host.PressDown((float)point.X, (float)point.Y, kind: _kind);
    }

    /// <summary>
    /// A finger, the Pencil, or the pointer of a trackpad or a mouse on an iPad, which arrives as a touch
    /// too. A finger pans what it lands on and never hovers (spec S5); a pointer's click is a press, as
    /// a mouse's is on the desktop.
    /// </summary>
    private static PointerKind KindOf(UITouch touch) => touch.Type switch
    {
        UITouchType.IndirectPointer => PointerKind.Mouse,
        UITouchType.Stylus => PointerKind.Stylus,
        _ => PointerKind.Touch,
    };

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        if (Tracked(touches) is not { } point || _host is null) return;
        _host.PointerMove((float)point.X, (float)point.Y, _kind);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        if (Tracked(touches) is not { } point || _host is null) return;
        _tracking = null;
        _host.PressUp((float)point.X, (float)point.Y, _kind);
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
        _host.PointerCancel(_kind);
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
        // The screen just changed: if that changed what a screen reader would read, tell it.
        view.Semantics?.FrameRendered();
        // And what a live region said since the last frame, wherever the reader is (#1081).
        PhotonAnnouncements.Post(_host.TakeAnnouncements());
        // And whether the keyboard has to open, start over, close or take a new value.
        _keyboard?.Sync();

        if (forced && FramesPresented >= MaxFrames)
        {
            ProbeAccessibility(view);
            // What the render CONTAINED, on the one line a self-test reads. No exit code here — a
            // UIViewController has no process status to set — but silence was the worse half: this
            // shell printed nothing at all, so a run whose whole chrome threw looked like a clean one.
            var contained = eQuantic.UI.Primitives.ComponentBoundary.Contained;
            var missing = eQuantic.UI.Primitives.FaceResolution.Unresolved;
            var summary = $"frames presented: {FramesPresented}";
            if (contained.Count > 0) summary += $" — CONTAINED: {string.Join(", ", contained)}";
            // CoreTextService serves this shell too, so a family it could not find is RECORDED here
            // whether or not anything says so. Recording without surfacing is the silence again.
            if (missing.Count > 0) summary += $" — FACE NOT FOUND: {string.Join(", ", missing)}";
            Console.WriteLine($"[photon] {summary}");
            Stop();
        }
    }

    /// <summary>
    /// Self-test evidence for the semantics bridge, taken through the REAL dispatch: what VoiceOver
    /// would receive if it asked this view right now, and what happens when it double-taps the
    /// first control. The selectors are answered by the Objective-C runtime, so only a message
    /// actually SENT proves they are registered — a managed call would pass whether or not UIKit
    /// can reach them.
    /// </summary>
    private static void ProbeAccessibility(PhotonView view)
    {
        var elements = Send(view.Handle, Sel("accessibilityElements"));
        var count = elements != IntPtr.Zero ? SendULong(elements, Sel("count")) : 0;
        if (count == 0)
        {
            PhotonLog.Write("[photon] accessibility elements: 0");
            return;
        }

        var first = Send(elements, Sel("firstObject"));
        var label = FromNSString(Send(first, Sel("accessibilityLabel")));
        var traits = SendULong(first, Sel("accessibilityTraits"));
        PhotonLog.Write($"[photon] accessibility elements: {count} — first: traits 0x{traits:x} \"{label}\"");

        // The whole loop, end to end: VoiceOver's double tap on the first BUTTON reaches the same
        // handler a finger does. A screen with no button reports that instead of a false pass.
        for (var index = 0UL; index < count; index++)
        {
            var element = Send(elements, Sel("objectAtIndex:"), (long)index);
            if ((SendULong(element, Sel("accessibilityTraits")) & (ulong)UIAccessibilityTrait.Button) == 0) continue;
            var ran = SendBool(element, Sel("accessibilityActivate"));
            PhotonLog.Write($"[photon] accessibility activate \"{FromNSString(Send(element, Sel("accessibilityLabel")))}\": {ran}");
            return;
        }
        PhotonLog.Write("[photon] accessibility activate: no button on this screen");
    }

    /// <summary>
    /// The scene went to the background, where the system refuses GPU work: the clock stops
    /// rather than ask for a drawable it will not get.
    /// </summary>
    internal void Pause()
    {
        if (_clock is not null) _clock.Paused = true;
    }

    /// <summary>
    /// The scene is coming back. The clock runs again and the next tick presents a frame, because
    /// what the system kept of the last one while the app was away is not something to rely on.
    /// A self-test whose clock already stopped stays stopped.
    /// </summary>
    internal void Resume()
    {
        if (_clock is null) return;
        _clock.Paused = false;
        _host?.Invalidate();
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
            _keyboard?.Dispose();
            _keyboard = null;
            _backend?.Dispose();
            _backend = null;
        }
        base.Dispose(disposing);
    }
}
