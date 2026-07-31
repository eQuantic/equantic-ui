using System.Diagnostics;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Primitives;
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
    private const ulong StyleTitledClosableMiniaturizable = 1 | 2 | 4;
    private const ulong BackingBuffered = 2;
    private const ulong EventTypeLeftMouseDown = 1;
    private const ulong EventTypeLeftMouseUp = 2;
    private const ulong EventTypeMouseMoved = 5;
    private const ulong EventTypeLeftMouseDragged = 6;
    private const ulong EventTypeScrollWheel = 22;

    private readonly string _title;
    private readonly float _width;
    private readonly float _height;

    public PhotonWindow(string title, float width = 800, float height = 600)
    {
        _title = title;
        _width = width;
        _height = height;
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
            new CGRect(0, 0, _width, _height), StyleTitledClosableMiniaturizable, BackingBuffered, false);
        SendVoid(window, Sel("setTitle:"), NSString(_title));
        SendVoid(window, Sel("setReleasedWhenClosed:"), false);
        SendVoid(window, Sel("setAcceptsMouseMovedEvents:"), true);
        SendVoid(window, Sel("center"));

        var scale = (float)SendDouble(window, Sel("backingScaleFactor"));
        if (scale <= 0) scale = 1;

        var layer = Send(objc_getClass("CAMetalLayer"), Sel("layer"));
        SendVoid(layer, Sel("setDevice:"), backend.DeviceHandle);
        SendVoid(layer, Sel("setPixelFormat:"), MetalBackend.PixelFormatBgra8UnormSrgb);
        SendVoid(layer, Sel("setFramebufferOnly:"), true);
        SendVoid(layer, Sel("setContentsScale:"), (double)scale);
        SendVoid(layer, Sel("setDrawableSize:"), new CGSize(_width * scale, _height * scale));
        var contentView = Send(window, Sel("contentView"));
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
            IconRasterizer = new CoreGraphicsIconRasterizer(),
        };

        var clock = Stopwatch.StartNew();
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

            if (host.NeedsRender || forced)
            {
                var drawable = Send(layer, Sel("nextDrawable"));
                if (drawable != IntPtr.Zero)
                {
                    var builder = new DisplayListBuilder();
                    host.RenderFrame(builder, (float)clock.Elapsed.TotalMilliseconds);
                    backend.RenderToDrawable(builder.Build(), Send(drawable, Sel("texture")),
                        MetalBackend.PixelFormatBgra8UnormSrgb, drawable);
                    FramesPresented++;
                }
            }

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
        var y = _height - (float)location.Y;

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
                host.ScrollBy(x, y, (float)-SendDouble(e, Sel("scrollingDeltaY")));
                break;
        }
    }
}
