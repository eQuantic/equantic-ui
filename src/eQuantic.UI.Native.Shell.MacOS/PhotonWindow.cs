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
    private const ulong StyleTitled = 1;
    private const ulong StyleClosable = 2;
    private const ulong StyleMiniaturizable = 4;
    private const ulong StyleResizable = 8;
    /// <summary>The content view extends UNDER the title bar — the whole window is the app's.</summary>
    private const ulong StyleFullSizeContentView = 1UL << 15;
    private const ulong BackingBuffered = 2;

    /// <summary>How tall the strip the window controls sit in is. Reported as a top safe-area inset
    /// under <see cref="WindowChrome.Unified"/>, so an app insets its own toolbar the same way it
    /// insets under a phone's notch.</summary>
    private const float TitleBarHeight = 28;
    private const ulong EventTypeLeftMouseDown = 1;
    private const ulong EventTypeLeftMouseUp = 2;
    private const ulong EventTypeMouseMoved = 5;
    private const ulong EventTypeLeftMouseDragged = 6;
    private const ulong EventTypeMouseExited = 9;
    private const ulong EventTypeScrollWheel = 22;

    // NSTrackingArea options — the crossing events, live while the window is key, with AppKit
    // keeping the rect glued to the view's visible rect so resizes need no re-install.
    private const ulong TrackingMouseEnteredAndExited = 0x01;
    private const ulong TrackingActiveInKeyWindow = 0x20;
    private const ulong TrackingInVisibleRect = 0x200;

    private readonly string _title;
    private readonly float _width;
    private readonly float _height;
    private float _currentWidth;
    private float _currentHeight;

    public PhotonWindow(string title, float width = 800, float height = 600,
        WindowChrome chrome = WindowChrome.Standard, bool resizable = true,
        float minWidth = 0, float minHeight = 0, bool smoothScroll = true)
    {
        _title = title;
        _width = width;
        _height = height;
        _currentWidth = width;
        _currentHeight = height;
        _chrome = chrome;
        _resizable = resizable;
        _minWidth = minWidth > 0 ? minWidth : 320;
        _minHeight = minHeight > 0 ? minHeight : 240;
        _smoothScroll = smoothScroll;
    }

    private readonly WindowChrome _chrome;
    private readonly bool _resizable;
    private readonly float _minWidth;
    private readonly float _minHeight;
    private readonly bool _smoothScroll;

    /// <summary>Frames actually presented — the self-test's exit evidence.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>
    /// Opens the window and runs the event/render loop until the window closes (or
    /// <paramref name="maxFrames"/> presents, when positive — the self-test mode).
    /// </summary>
    public void Run(VisualNode root, IAppTheme theme, ThemeMode mode = ThemeMode.Light, int maxFrames = 0,
        eQuantic.UI.Native.Hosting.PhotonThemeController? themeController = null)
    {
        LoadFrameworks();
        using var backend = new MetalBackend();

        // NSApplication — a regular, activatable app (no bundle needed for the dev shell).
        var app = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
        SendVoid(app, Sel("setActivationPolicy:"), 0ul);
        SendVoid(app, Sel("finishLaunching"));

        // NSWindow + CAMetalLayer content.
        var styleMask = StyleTitled | StyleClosable | StyleMiniaturizable
            | (_resizable ? StyleResizable : 0)
            | (_chrome == WindowChrome.Unified ? StyleFullSizeContentView : 0);

        var window = Send(Send(objc_getClass("NSWindow"), Sel("alloc")),
            Sel("initWithContentRect:styleMask:backing:defer:"),
            new CGRect(0, 0, _width, _height), styleMask, BackingBuffered, false);
        SendVoid(window, Sel("setTitle:"), NSString(_title));

        // Unified: the bar stops being drawn and stops being a strip the content sits below — what
        // stays is the three controls, floating over the app's own top edge.
        if (_chrome == WindowChrome.Unified)
        {
            SendVoid(window, Sel("setTitlebarAppearsTransparent:"), true);
            SendVoid(window, Sel("setTitleVisibility:"), 1ul); // NSWindowTitleHidden
        }
        SendVoid(window, Sel("setReleasedWhenClosed:"), false);
        SendVoid(window, Sel("setAcceptsMouseMovedEvents:"), true);
        SendVoid(window, Sel("setContentMinSize:"), new CGSize(_minWidth, _minHeight));
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

        // Crossing the window edge is the one pointer move AppKit never delivers — without a
        // tracking area there is no MouseExited event, and the last hovered box keeps its Hover
        // diff forever. InVisibleRect ignores the rect and tracks the view's own bounds; the
        // event reaches Route() through the same pump as every other pointer event.
        var trackingArea = Send(Send(objc_getClass("NSTrackingArea"), Sel("alloc")),
            Sel("initWithRect:options:owner:userInfo:"), default(CGRect),
            TrackingMouseEnteredAndExited | TrackingActiveInKeyWindow | TrackingInVisibleRect,
            contentView, IntPtr.Zero);
        SendVoid(contentView, Sel("addTrackingArea:"), trackingArea);

        SendVoid(window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
        SendVoid(app, Sel("activateIgnoringOtherApps:"), true);
        // Saying YES to acceptsFirstResponder only makes the view ELIGIBLE; someone still has to
        // hand it the keyboard. Without this the window opens deaf.
        SendVoid(window, Sel("makeFirstResponder:"), contentView);

        // W4: CoreText serves BOTH measuring (layout breaks) and rasterizing (A8 coverage) —
        // real glyphs in the window, breaks identical by construction.
        var textService = new CoreTextService();
        // ONE builder for the loop's lifetime: Reset keeps every buffer's capacity, so a steady
        // 120 Hz stops growing fresh lists per frame.
        var frameBuilder = new DisplayListBuilder();
        var host = new PhotonHost(root, theme, mode, _width, _height, textService)
        {
            // Production owns its frames: nothing retains a RealizeResult past the next render,
            // so each replaced tree feeds the next (LayoutNodePool).
            RecycleFrames = true,
            RenderScale = scale,
            // A Mac is driven by a POINTER: the controls tighten, exactly as every native desktop
            // app's do. The same tree on a phone stays comfortable, and no page chose either.
            Density = Density.Compact,
            SmoothScroll = _smoothScroll,
            Clipboard = new MacClipboard(),
            // Whatever the system kept for itself is a safe area, exactly as a phone's notch is.
            SafeAreaInsets = _chrome == WindowChrome.Unified
                ? new EdgeInsets(0, TitleBarHeight, 0, 0)
                : default,
            // W4b: the Metal textured pipeline is live — REAL glyphs on screen.
            TextRasterizer = textService,
            ImageLoader = new CoreGraphicsImageLoader(),
            IconRasterizer = new CoreGraphicsIconRasterizer(),
        };

        // The app's hand on the light/dark switch, attached now that the host exists: flipping
        // the mode re-realizes the SAME tree against the other palette on the next frame.
        themeController?.Attach(mode, next =>
        {
            host.Mode = next;
            host.Invalidate();
        });

        var clock = Stopwatch.StartNew();

        // Draw one frame, wherever we are called from.
        void Present()
        {
            var drawable = Send(layer, Sel("nextDrawable"));
            if (drawable == IntPtr.Zero) return;
            frameBuilder.Reset();
            var builder = frameBuilder;
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
        PhotonContentView.OnKey = (key, modifiers, typed) =>
        {
            // The named key first — Tab, Enter, Backspace, a chord the app declared. Only what
            // nothing claimed becomes text, so Space runs the focused button instead of typing one
            // into whatever field was last touched.
            var handled = host.KeyDown(key, modifiers);
            if (!handled && typed.Length > 0) handled = host.TextInput(typed);
            // Drawn straight away rather than left to the next tick: a caret that appears a frame
            // after the click, or a character that shows up late, both read as a slow app.
            if (handled) Present();
            return handled;
        };
        // The IME doors: while a field holds the caret the input context interprets keys, and
        // what it decides comes back through these — composed text, the composition in flight,
        // the ranges it asks about, and the caret rect its candidate window anchors to.
        PhotonContentView.HasTextFocus = () => host.HasTextFocus;
        PhotonContentView.OnInsertText = text => { if (host.CommitText(text)) Present(); };
        PhotonContentView.OnMarkedText = text => { if (host.SetMarkedText(text)) Present(); };
        PhotonContentView.HasMarked = () => host.HasMarkedText;
        PhotonContentView.SelectedRangeSource = () => host.SelectionRange;
        PhotonContentView.MarkedRangeSource = () => host.MarkedRange;
        PhotonContentView.CaretRectSource = () =>
            host.CaretRect() is { } rect ? (rect.X, rect.Y, rect.Width, rect.Height) : null;

        PhotonAccessibility.Source = host.Semantics;
        PhotonAccessibility.OnActivate = path =>
        {
            var handled = host.ActivatePath(path);
            // Same rule as a key press: the pressed state paints NOW, not at the next tick.
            if (handled) Present();
            return handled;
        };
        PhotonAccessibility.OnAdjust = (path, direction) =>
        {
            var handled = host.AdjustPath(path, direction);
            if (handled) Present();
            return handled;
        };

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
            // A due frame is the caret's next blink transition — two a second, not every vsync.
            if (host.NeedsRender || host.IsFrameDue((float)clock.Elapsed.TotalMilliseconds)) Present();
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

            if (host.NeedsRender || forced
                || host.IsFrameDue((float)clock.Elapsed.TotalMilliseconds)) Present();

            if (maxFrames > 0 && FramesPresented >= maxFrames)
            {
                // The AX probe, through the REAL dispatch: what VoiceOver would receive if it asked
                // this window right now. Self-test evidence, printed beside FramesPresented.
                var axChildren = Send(contentView, Sel("accessibilityChildren"));
                var axCount = axChildren != IntPtr.Zero ? SendULong(axChildren, Sel("count")) : 0;
                var axFirst = axCount > 0 ? Send(axChildren, Sel("firstObject")) : IntPtr.Zero;
                var axSample = axFirst != IntPtr.Zero
                    ? $" — first: {FromNSString(Send(axFirst, Sel("accessibilityRole")))} \"{FromNSString(Send(axFirst, Sel("accessibilityLabel")))}\""
                    : "";
                Console.WriteLine($"[photon] accessibility elements: {axCount}{axSample}");
                ProbeIme(contentView, host, (float)clock.Elapsed.TotalMilliseconds);
                SendVoid(window, Sel("close"));
                break;
            }
        }
    }

    /// <summary>
    /// Self-test evidence for the IME plumbing, through the REAL registered selectors: focus the
    /// first field (when the page has one), send <c>setMarkedText:</c> and <c>insertText:</c> to
    /// the view the way the input context would, and report what the field ended up holding. This
    /// proves the protocol methods are registered with the right encodings and route to the host —
    /// only human fingers can prove the system INVOKES them.
    /// </summary>
    private static void ProbeIme(IntPtr contentView, PhotonHost host, float timeMs)
    {
        var frame = host.LastFrame;
        if (frame is null || frame.TextRegions.Count == 0)
        {
            // No field on this page: open the ⌘K search the way the keyboard would, and let the
            // next frames realize the palette and adopt its autofocus.
            host.KeyDown("k", Primitives.KeyModifiers.Command);
            host.RenderFrame(new DisplayListBuilder(), timeMs + 16);
            host.RenderFrame(new DisplayListBuilder(), timeMs + 32);
            frame = host.LastFrame;
        }
        if (frame is null || frame.TextRegions.Count == 0)
        {
            Console.WriteLine("[photon] ime probe: no text field reachable");
            return;
        }
        if (!host.HasTextFocus)
        {
            var field = frame.TextRegions[0].Bounds;
            host.PressDown(field.Center.X, field.Center.Y);
            host.PressUp(field.Center.X, field.Center.Y);
        }

        SendVoid(contentView, Sel("setMarkedText:selectedRange:replacementRange:"),
            NSString("´"), new NSRange(0, 1), NSRange.NotFound);
        var marked = host.MarkedText;
        SendVoid(contentView, Sel("insertText:replacementRange:"), NSString("á"), NSRange.NotFound);
        var committed = host.HasMarkedText ? "still marked!" : "committed";
        Console.WriteLine($"[photon] ime probe: marked '{marked}' → {committed}");
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
            case EventTypeMouseExited:
            case EventTypeScrollWheel:
                break;
            default:
                return;
        }

        if (type == EventTypeMouseExited)
        {
            // The pointer left the content view — its location is already outside; hover clears.
            host.PointerLeave();
            return;
        }

        var location = SendPoint(e, Sel("locationInWindow"));
        var x = (float)location.X;
        var y = _currentHeight - (float)location.Y;

        switch (type)
        {
            case EventTypeLeftMouseDown:
                // Shift+click extends a sheet/text selection — the same bits OnKeyDown reads.
                var mouseFlags = SendULong(e, Sel("modifierFlags"));
                var mouseModifiers = Primitives.KeyModifiers.None;
                if ((mouseFlags & (1UL << 17)) != 0) mouseModifiers |= Primitives.KeyModifiers.Shift;
                if ((mouseFlags & (1UL << 20)) != 0) mouseModifiers |= Primitives.KeyModifiers.Command;
                host.PressDown(x, y, (int)SendULong(e, Sel("clickCount")), mouseModifiers);
                break;
            case EventTypeLeftMouseUp:
                host.PressUp(x, y);
                break;
            case EventTypeMouseMoved:
            case EventTypeLeftMouseDragged:
                host.PointerMove(x, y);
                Cursors.Apply(host.CursorAt(x, y));
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
