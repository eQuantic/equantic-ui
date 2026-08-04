using System.Diagnostics;
using System.Runtime.InteropServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;
using static eQuantic.UI.Native.Shell.MacOS.AppKit;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The FIRST real Photon window (W5 milestone 1): an NSWindow whose content view hosts a
/// CAMetalLayer; every frame the PhotonHost realizes the write-once tree into a display list and
/// the Metal backend encodes it STRAIGHT into the layer's drawable (BGRA8_sRGB pipeline). OS input
/// (mouse down/up/move/drag, scroll) routes into the host's ordinary pointer pipeline — press
/// visuals, hover diffs, drag-to-dismiss and anchored menus all behave exactly like the tests.
/// Layout/input stay in dp; <see cref="PhotonHost.RenderScale"/> rasters at backingScaleFactor
/// (retina). The loop is cooperative: it blocks on the next OS event while idle and free-runs on
/// the frame clock only while motion is active.
/// v1 fences: fixed window size (PhotonHost dimensions are immutable — resize lands with the host
/// viewport work), keyboard/IME (W4/M4), per-process ObjC lifetimes (the documented spike leak),
/// arm64-only msgSend bindings.
/// </summary>
public sealed class PhotonWindow
{
    private const ulong StyleTitledClosableMiniaturizableResizable = 1 | 2 | 4 | 8;
    private const ulong BackingBuffered = 2;
    private const ulong EventTypeLeftMouseDown = 1;
    private const ulong EventTypeLeftMouseUp = 2;
    private const ulong EventTypeMouseMoved = 5;
    private const ulong EventTypeLeftMouseDragged = 6;
    private const ulong EventTypeScrollWheel = 22;

    private readonly string _title;
    private readonly float _width;
    private readonly float _height;
    private float _currentWidth;
    private float _currentHeight;

    public PhotonWindow(string title, float width = 800, float height = 600)
    {
        _title = title;
        _width = width;
        _height = height;
        _currentWidth = width;
        _currentHeight = height;
    }

    /// <summary>Frames actually presented — the self-test's exit evidence.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>
    /// Opens the window and runs the event/render loop until the window closes (or
    /// <paramref name="maxFrames"/> presents, when positive — the self-test mode).
    /// </summary>
    public void Run(VisualNode root, IAppTheme theme, ThemeMode mode = ThemeMode.Light, int maxFrames = 0)
    {
        LoadFrameworks();
        using var backend = new MetalBackend();

        // NSApplication — a regular, activatable app (no bundle needed for the dev shell).
        var app = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
        SendVoid(app, Sel("setActivationPolicy:"), 0ul);
        SendVoid(app, Sel("finishLaunching"));

        // NSWindow + CAMetalLayer content.
        var window = Send(Send(objc_getClass("NSWindow"), Sel("alloc")),
            Sel("initWithContentRect:styleMask:backing:defer:"),
            new CGRect(0, 0, _width, _height), StyleTitledClosableMiniaturizableResizable, BackingBuffered, false);
        SendVoid(window, Sel("setTitle:"), NSString(_title));
        SendVoid(window, Sel("setReleasedWhenClosed:"), false);
        SendVoid(window, Sel("setAcceptsMouseMovedEvents:"), true);
        SendVoid(window, Sel("setContentMinSize:"), new CGSize(320, 240));
        SendVoid(window, Sel("center"));

        var scale = (float)SendDouble(window, Sel("backingScaleFactor"));
        if (scale <= 0) scale = 1;

        var layer = Send(objc_getClass("CAMetalLayer"), Sel("layer"));
        SendVoid(layer, Sel("setDevice:"), backend.DeviceHandle);
        SendVoid(layer, Sel("setPixelFormat:"), MetalBackend.PixelFormatBgra8UnormSrgb);
        SendVoid(layer, Sel("setFramebufferOnly:"), true);
        // The frame and the layer's size change in ONE transaction — see MetalCommandList.Submit.
        SendVoid(layer, Sel("setPresentsWithTransaction:"), true);
        // …and until the new frame lands, the old one stays ANCHORED at the top-left instead of
        // being scaled to fill: a window growing shows background at the new edge, never a
        // stretched copy of what was there before.
        SendVoid(layer, Sel("setContentsGravity:"), NSString("topLeft"));
        SendVoid(layer, Sel("setContentsScale:"), (double)scale);
        SendVoid(layer, Sel("setDrawableSize:"), new CGSize(_width * scale, _height * scale));
        // OUR content view: the one place AppKit reports a live resize to (see PhotonContentView).
        var viewClass = PhotonContentView.Register();
        var contentView = Send(Send(viewClass, Sel("alloc")), Sel("init"));
        SendVoid(window, Sel("setContentView:"), contentView);
        SendVoid(contentView, Sel("setWantsLayer:"), true);
        SendVoid(contentView, Sel("setLayer:"), layer);

        SendVoid(window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
        SendVoid(app, Sel("activateIgnoringOtherApps:"), true);

        // W4: CoreText serves BOTH measuring (layout breaks) and rasterizing (A8 coverage) —
        // real glyphs in the window, breaks identical by construction.
        var textService = new CoreTextService();
        var host = new PhotonHost(root, theme, mode, _width, _height, textService)
        {
            RenderScale = scale,
            // W4b: the Metal textured pipeline is live — REAL glyphs on screen.
            TextRasterizer = textService,
            ImageLoader = new CoreGraphicsImageLoader(),
            IconRasterizer = new CoreGraphicsIconRasterizer(),
        };

        var clock = Stopwatch.StartNew();

        // Draw one frame, wherever we are called from.
        void Present()
        {
            var drawable = Send(layer, Sel("nextDrawable"));
            if (drawable == IntPtr.Zero) return;
            var builder = new DisplayListBuilder();
            host.RenderFrame(builder, (float)clock.Elapsed.TotalMilliseconds);
            backend.RenderToDrawable(builder.Build(), Send(drawable, Sel("texture")),
                MetalBackend.PixelFormatBgra8UnormSrgb, drawable);
            FramesPresented++;
        }

        // Called by AppKit ON EVERY STEP of a resize drag, from inside its own loop.
        void OnLiveResize(float w, float h)
        {
            if (w <= 0 || h <= 0) return;
            _currentWidth = w;
            _currentHeight = h;
            SendVoid(layer, Sel("setDrawableSize:"), new CGSize(w * scale, h * scale));
            host.Resize(w, h);
            Present();
        }

        PhotonContentView.OnResized = OnLiveResize;

        // A LIVE RESIZE never returns to the loop below: AppKit runs its own, inside sendEvent:,
        // until the button comes up. The window keeps growing and the last frame is stretched to
        // fit it — which is exactly what a resize looked like, snapping into place only on release.
        // A run-loop OBSERVER is called from inside whatever loop is running, so the frame is drawn
        // there: it re-measures against the new size and the content follows the edge.
        void FollowTheWindow()
        {
            var live = SendRect(contentView, Sel("bounds"));
            if (live.Width <= 0 || live.Height <= 0) return;
            if ((float)live.Width != _currentWidth || (float)live.Height != _currentHeight)
            {
                _currentWidth = (float)live.Width;
                _currentHeight = (float)live.Height;
                SendVoid(layer, Sel("setDrawableSize:"), new CGSize(_currentWidth * scale, _currentHeight * scale));
                host.Resize(_currentWidth, _currentHeight);
            }
            if (host.NeedsRender) Present();
        }

        // A TIMER in the common modes, because that is what still fires while AppKit is tracking a
        // drag. An observer only runs when the loop it is attached to reaches one of its phases,
        // and a resize never gives it one; a common-modes timer is scheduled by the loop that IS
        // running, whichever that happens to be. 120 Hz so it is never the thing you are waiting
        // for — it does nothing at all unless the window changed or a frame is due.
        AppKit.TimerCallback onTick = (_, _) => FollowTheWindow();
        var tickHandle = GCHandle.Alloc(onTick);
        var commonModes = AppKit.CFStringCreateWithCString(IntPtr.Zero, AppKit.CommonModes, 0x08000100 /* UTF8 */);
        var timer = AppKit.CFRunLoopTimerCreate(IntPtr.Zero, AppKit.CFAbsoluteTimeGetCurrent(),
            1.0 / 120.0, 0, 0, Marshal.GetFunctionPointerForDelegate(onTick), IntPtr.Zero);
        AppKit.CFRunLoopAddTimer(AppKit.CFRunLoopGetCurrent(), timer, commonModes);
        // COMMON modes, not the default one. The moment a button goes down, AppKit switches to
        // NSEventTrackingRunLoopMode and delivers the drag and the mouse-UP there. Asking only for
        // the default mode means the up never arrives: the press is begun and never completed, so
        // a control lights up and answers nothing. A synthetic click that never moves works — every
        // human click moves a pixel — which is exactly how this survived a self-test.
        // The same mode is why a live resize showed a stretched frame until the button was let go:
        // the resize's own events were invisible too, so nothing re-rendered until tracking ended.
        var runLoopMode = NSString("kCFRunLoopDefaultMode");
        var nsDate = objc_getClass("NSDate");
        var distantPast = Send(nsDate, Sel("distantPast"));

        while (true)
        {
            // Drain pending OS events; while idle (no motion, nothing dirty) block briefly on the
            // next event instead of spinning.
            // Self-test presents EVERY cycle (a static tree renders on demand only and would
            // otherwise idle forever short of maxFrames).
            var forced = maxFrames > 0;
            var idle = !host.NeedsRender && !forced;
            var until = idle ? Send(nsDate, Sel("dateWithTimeIntervalSinceNow:"), 0.05) : distantPast;
            while (true)
            {
                var e = Send(app, Sel("nextEventMatchingMask:untilDate:inMode:dequeue:"),
                    ulong.MaxValue, until, runLoopMode, true);
                if (e == IntPtr.Zero) break;
                Route(e, host);
                SendVoid(app, Sel("sendEvent:"), e);
                until = distantPast; // only the first wait blocks
            }

            if (!SendBool(window, Sel("isVisible"))) break;

            // W5 resize: poll the content size each cycle (no ObjC delegate class needed) — on a
            // change, resize the drawable and the HOST (state survives; layout adopts next frame).
            var content = SendRect(contentView, Sel("bounds"));
            var newW = (float)content.Width;
            var newH = (float)content.Height;
            if (newW > 0 && newH > 0 && (newW != _currentWidth || newH != _currentHeight))
            {
                _currentWidth = newW;
                _currentHeight = newH;
                SendVoid(layer, Sel("setDrawableSize:"), new CGSize(newW * scale, newH * scale));
                host.Resize(newW, newH);
            }

            if (host.NeedsRender || forced) Present();

            if (maxFrames > 0 && FramesPresented >= maxFrames)
            {
                SendVoid(window, Sel("close"));
                break;
            }
        }
    }

    /// <summary>OS event → the host's pointer pipeline. AppKit is bottom-left; the UI is top-left.</summary>
    private void Route(IntPtr e, PhotonHost host)
    {
        var type = SendULong(e, Sel("type"));
        switch (type)
        {
            case EventTypeLeftMouseDown:
            case EventTypeLeftMouseUp:
            case EventTypeMouseMoved:
            case EventTypeLeftMouseDragged:
            case EventTypeScrollWheel:
                break;
            default:
                return;
        }

        var location = SendPoint(e, Sel("locationInWindow"));
        var x = (float)location.X;
        var y = _currentHeight - (float)location.Y;

        switch (type)
        {
            case EventTypeLeftMouseDown:
                host.PressDown(x, y);
                break;
            case EventTypeLeftMouseUp:
                host.PressUp(x, y);
                break;
            case EventTypeMouseMoved:
            case EventTypeLeftMouseDragged:
                host.PointerMove(x, y);
                break;
            case EventTypeScrollWheel:
                host.ScrollBy(x, y, WheelTravel(e));
                break;
        }
    }

    /// <summary>
    /// How far this wheel event asked the content to move, in dp. A trackpad (and a Magic Mouse)
    /// reports PRECISE deltas already in points; a wheel reports LINES, and one line is
    /// <see cref="Touch.WheelLine"/>. Passing the line count straight through moved a page three
    /// pixels per notch, which is a hundred turns of the wheel to reach the bottom of anything.
    /// </summary>
    private static float WheelTravel(IntPtr e) => Touch.WheelTravel(
        (float)-SendDouble(e, Sel("scrollingDeltaY")),
        SendBool(e, Sel("hasPreciseScrollingDeltas")));
}
