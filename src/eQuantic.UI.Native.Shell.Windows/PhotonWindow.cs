using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Photon window on Windows: one HWND whose client area the engine presents into — through
/// Vulkan where a driver exposes a surface, through the Reference backend and GDI where none does —
/// with OS input routed into the host's ordinary pointer, keyboard and text pipelines. Layout and
/// input stay in dp; <see cref="PhotonHost.RenderScale"/> rasters at the window's DPI, re-read on
/// every <c>WM_DPICHANGED</c> (per-monitor V2). The loop is cooperative: it blocks on the next
/// message while idle and free-runs only while motion is active or a frame is due.
/// <para>
/// A live resize or a caption drag never returns to that loop — Windows runs its own inside
/// <c>DefWindowProc</c> until the button comes up — so the frame is drawn from the messages that
/// arrive inside it: <c>WM_SIZE</c> on every step, and a timer for motion that continues meanwhile.
/// </para>
/// <para>
/// One window per process on this head, held in statics like the Mac's content view: the window
/// procedure is a plain function pointer, and there is exactly one instance for it to find.
/// </para>
/// </summary>
public sealed unsafe class PhotonWindow
{
    /// <summary>How tall the strip the window controls sit in is, under <see cref="WindowChrome.Unified"/>
    /// — Windows 11's own caption height. Reported as a top safe-area inset, so an app insets its own
    /// toolbar the same way it insets under a phone's notch.</summary>
    private const float TitleBarHeight = 32;

    /// <summary>Windows 11 draws its caption buttons 46 wide; ours match, so muscle memory does.</summary>
    private const float CaptionButtonWidth = 46;

    private const nuint LiveResizeTimer = 1;

    private static PhotonWindow? _current;

    private readonly string _className;
    private readonly string _title;
    private readonly float _width;
    private readonly float _height;
    private readonly WindowChrome _chrome;
    private readonly bool _resizable;
    private readonly float _minWidth;
    private readonly float _minHeight;
    private readonly bool _smoothScroll;

    private IntPtr _hwnd;
    private IntPtr _hinstance;
    private uint _dpi = 96;
    private float _scale = 1f;
    private int _clientWidth;
    private int _clientHeight;
    private uint _style;

    private PhotonHost? _host;
    private IAppTheme? _theme;
    private IPresenter? _presenter;
    private string? _presenterName;
    private double _presentTotalMs;
    private readonly DisplayListBuilder _builder = new();
    private readonly Stopwatch _clock = new();
    private PhotonThemeController? _themeController;
    private bool _followSystemTheme;
    private bool _closed;
    private bool _presenting;
    private bool _paintRequested;
    private Exception? _fault;
    private long _lastPresentMs;

    private bool _trackingLeave;
    private bool _swallowChar;
    private char _highSurrogate;
    private int _clickCount;
    private uint _lastClickTime;
    private int _lastClickX;
    private int _lastClickY;
    private uint _wheelLines = 3;
    private bool _imeAssociated = true;
    private nint _hoveredCaption;
    private nint _pressedCaption;

    public PhotonWindow(string className, string title, float width = 800, float height = 600,
        WindowChrome chrome = WindowChrome.Standard, bool resizable = true,
        float minWidth = 0, float minHeight = 0, bool smoothScroll = true)
    {
        _className = className;
        _title = title;
        _width = width;
        _height = height;
        _chrome = chrome;
        _resizable = resizable;
        _minWidth = minWidth > 0 ? minWidth : 320;
        _minHeight = minHeight > 0 ? minHeight : 240;
        _smoothScroll = smoothScroll;
    }

    /// <summary>Frames actually presented — the self-test's exit evidence.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>Which path drew them, printed beside the count so a screenshot is never mistaken
    /// for the other one.</summary>
    public string PresenterName => _presenterName ?? "nothing yet";

    /// <summary>The mean wall time of a presented frame — realize, rasterize and blit — the number
    /// that says whether the software path is bearable on this machine.</summary>
    public double AveragePresentMs => FramesPresented == 0 ? 0 : _presentTotalMs / FramesPresented;

    /// <summary>Text another process handed this window through <c>WM_COPYDATA</c> — the URL a
    /// second instance was launched with, forwarded here so the running app opens it.</summary>
    public Action<string>? OnCopyData { get; set; }

    /// <summary>
    /// Opens the window and runs the message/render loop until the window closes (or
    /// <paramref name="maxFrames"/> presents, when positive — the self-test mode). Any exception a
    /// handler raised inside the window procedure ends the loop and is rethrown HERE, on the
    /// caller's stack, rather than tearing the process down inside a callback nobody can catch.
    /// </summary>
    public void Run(VisualNode root, IAppTheme theme, ThemeMode mode, int maxFrames = 0,
        PhotonThemeController? themeController = null, PhotonCultureController? cultureController = null,
        bool followSystemTheme = false)
    {
        if (_current is not null)
            throw new InvalidOperationException("One Photon window per process on this head; the other is still open.");
        _current = this;
        _theme = theme;
        _themeController = themeController;
        _followSystemTheme = followSystemTheme;
        try
        {
            RunCore(root, theme, mode, maxFrames, themeController, cultureController);
        }
        finally
        {
            _current = null;
        }
        if (_fault is not null) throw new InvalidOperationException("The window's handler failed.", _fault);
    }

    private void RunCore(VisualNode root, IAppTheme theme, ThemeMode mode, int maxFrames,
        PhotonThemeController? themeController, PhotonCultureController? cultureController)
    {
        Com.EnsureInitialized();
        SetProcessDpiAwarenessContext(DpiAwarenessPerMonitorV2);
        _hinstance = GetModuleHandleW(null);
        RefreshWheelLines();

        // The class: our window procedure, the arrow as the resting cursor (WM_SETCURSOR decides
        // the rest), the app's own icon from its executable, and NO background brush — the engine
        // paints every pixel, and a brush here is a white flash at every resize.
        var icon = ProcessIcon();
        fixed (char* className = _className)
        {
            var wndClass = new WNDCLASSEXW
            {
                Size = (uint)sizeof(WNDCLASSEXW),
                Style = 0,
                WndProc = (IntPtr)(delegate* unmanaged<IntPtr, uint, nuint, nint, nint>)&WindowProcedure,
                Instance = _hinstance,
                Icon = icon,
                IconSmall = icon,
                Cursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
                ClassName = (IntPtr)className,
            };
            if (RegisterClassExW(&wndClass) == 0)
                throw new InvalidOperationException($"Window class registration failed (error {Marshal.GetLastPInvokeError()}).");
        }

        _style = WS_OVERLAPPEDWINDOW;
        if (!_resizable) _style &= ~(WS_THICKFRAME | WS_MAXIMIZEBOX);

        // Created hidden at a provisional size, then measured: the DPI that matters is the one of
        // the monitor the window LANDS on, and that is only known once it exists.
        _hwnd = CreateWindowExW(WS_EX_APPWINDOW, _className, _title, _style, CW_USEDEFAULT, CW_USEDEFAULT,
            100, 100, IntPtr.Zero, IntPtr.Zero, _hinstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"Window creation failed (error {Marshal.GetLastPInvokeError()}).");

        _dpi = GetDpiForWindow(_hwnd);
        if (_dpi == 0) _dpi = 96;
        _scale = _dpi / BaseDpi;
        WindowsTheme.ApplyToTitleBar(_hwnd, mode);
        PlaceWindow();

        // Which path draws is decided ONCE, before the host exists, and said out loud: a driver
        // without Vulkan is common on a virtual machine or over Remote Desktop, and a screenshot from
        // the software path must never be read as evidence about the GPU one.
        _presenter = CreatePresenter();
        _presenterName = _presenter.Name;
        Console.WriteLine($"[photon] presenting through {_presenter.Name}");

        // DirectWrite serves BOTH measuring (layout breaks) and rasterizing (A8 coverage) — real
        // glyphs in the window, breaks identical by construction.
        var textService = new DirectWriteTextService();
        var iconRasterizer = new Direct2DIconRasterizer();
        var imageLoader = new WicImageLoader();
        _host = new PhotonHost(root, theme, mode, _clientWidth / _scale, _clientHeight / _scale, textService)
        {
            // Production owns its frames: nothing retains a RealizeResult past the next render.
            RecycleFrames = true,
            RenderScale = _scale,
            // A desktop is driven by a POINTER: the controls tighten, exactly as every native
            // desktop app's do. The same tree on a phone stays comfortable.
            Density = Density.Compact,
            SmoothScroll = _smoothScroll,
            Clipboard = new WindowsClipboard(),
            // Whatever the system kept for itself is a safe area, exactly as a phone's notch is.
            SafeAreaInsets = _chrome == WindowChrome.Unified ? new EdgeInsets(0, TitleBarHeight, 0, 0) : default,
            // And WHERE in that strip the controls sit — the three caption buttons at the END. A
            // toolbar that is the title bar asks for SafeEdges.WindowControls and keeps clear of
            // them; on a Mac the same node keeps clear of the traffic lights at the start.
            WindowControlsInsets = _chrome == WindowChrome.Unified
                ? new EdgeInsets(0, TitleBarHeight, 3 * CaptionButtonWidth, 0)
                : default,
            TextRasterizer = textService,
            ImageLoader = imageLoader,
            IconRasterizer = iconRasterizer,
        };

        // The app's hand on the light/dark switch, attached now that the host exists: flipping the
        // mode re-realizes the SAME tree against the other palette on the next frame.
        themeController?.Attach(mode, next =>
        {
            _host.Mode = next;
            WindowsTheme.ApplyToTitleBar(_hwnd, next);
            _host.Invalidate();
        });
        cultureController?.Attach(_host.Invalidate);

        _clock.Start();
        ShowWindow(_hwnd, SW_SHOW);

        try
        {
            Loop(maxFrames);
        }
        finally
        {
            _presenter.Dispose();
            _presenter = null;
            textService.Dispose();
            iconRasterizer.Dispose();
            imageLoader.Dispose();
            if (_hwnd != IntPtr.Zero && !_closed) DestroyWindow(_hwnd);
        }
    }

    private void Loop(int maxFrames)
    {
        var forced = maxFrames > 0;
        MSG message;
        while (!_closed)
        {
            // While idle (no motion, nothing dirty, nothing due) block briefly on the next message
            // instead of spinning; while animating, pace to roughly a display refresh so a fast
            // software frame does not burn a core drawing the same tween twice.
            var now = Now();
            var idle = !(_host!.NeedsRender || _paintRequested || forced || _host.IsFrameDue(now));
            var since = (long)now - _lastPresentMs;
            var timeout = idle ? 50u : (uint)Math.Clamp(16 - since, 0, 16);
            MsgWaitForMultipleObjectsEx(0, null, timeout, QS_ALLINPUT, MWMO_INPUTAVAILABLE);

            while (PeekMessageW(&message, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                if (message.Message == WM_QUIT) { _closed = true; break; }
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }
            if (_closed || _fault is not null) break;

            now = Now();
            if (_host.NeedsRender || _paintRequested || forced || _host.IsFrameDue(now)) Present(IntPtr.Zero);

            if (forced && FramesPresented >= maxFrames)
            {
                Console.WriteLine($"[photon] accessibility elements: {_host.Semantics().Count}");
                DestroyWindow(_hwnd);
                // Let the destroy messages drain so the process leaves nothing half-torn-down.
                while (PeekMessageW(&message, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }
                _closed = true;
            }
        }
    }

    private float Now() => (float)_clock.Elapsed.TotalMilliseconds;

    // ---- Geometry -----------------------------------------------------------------------------

    /// <summary>
    /// Sizes the CLIENT area to the requested dp at the window's own DPI, clamped to the work area
    /// of the monitor it is on, and centres it there. Measured back after placing, because under
    /// <see cref="WindowChrome.Unified"/> the caption strip becomes client area and the frame
    /// arithmetic Windows offers does not know that; one correction closes the difference exactly.
    /// </summary>
    private void PlaceWindow()
    {
        var info = new MONITORINFO { Size = (uint)sizeof(MONITORINFO) };
        var monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        RECT work;
        if (monitor != IntPtr.Zero && GetMonitorInfoW(monitor, &info)) work = info.Work;
        else work = new RECT { Left = 0, Top = 0, Right = 1280, Bottom = 800 };

        var frame = new RECT { Left = 0, Top = 0, Right = (int)MathF.Round(_width * _scale), Bottom = (int)MathF.Round(_height * _scale) };
        AdjustWindowRectExForDpi(&frame, _style, false, WS_EX_APPWINDOW, _dpi);
        var outerWidth = Math.Min(frame.Width, work.Width);
        var outerHeight = Math.Min(frame.Height, work.Height);
        var x = work.Left + (work.Width - outerWidth) / 2;
        var y = work.Top + (work.Height - outerHeight) / 2;
        SetWindowPos(_hwnd, IntPtr.Zero, x, y, outerWidth, outerHeight, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        RECT client;
        GetClientRect(_hwnd, &client);
        var wantWidth = Math.Min((int)MathF.Round(_width * _scale), client.Width + (work.Width - outerWidth));
        var wantHeight = Math.Min((int)MathF.Round(_height * _scale), client.Height + (work.Height - outerHeight));
        if (client.Width != wantWidth || client.Height != wantHeight)
        {
            outerWidth += wantWidth - client.Width;
            outerHeight += wantHeight - client.Height;
            x = work.Left + (work.Width - outerWidth) / 2;
            y = work.Top + (work.Height - outerHeight) / 2;
            SetWindowPos(_hwnd, IntPtr.Zero, x, y, outerWidth, outerHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            GetClientRect(_hwnd, &client);
        }
        _clientWidth = client.Width;
        _clientHeight = client.Height;
    }

    private IPresenter CreatePresenter()
    {
        if (VulkanBackend.IsSupported)
        {
            try
            {
                return new VulkanPresenter(_hinstance, _hwnd, _clientWidth, _clientHeight);
            }
            catch (Exception error)
            {
                // A loader that enumerates a device and then cannot present to a window is a real
                // configuration (a compute-only ICD, a driver mid-update); the software path is the
                // honest answer, and the reason is said once.
                Console.WriteLine($"[photon] Vulkan is present but cannot present to this window ({error.Message}); using the Reference backend.");
            }
        }
        return new SoftwarePresenter(_hwnd, _clientWidth, _clientHeight);
    }

    private static IntPtr ProcessIcon()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path)) return IntPtr.Zero;
        IntPtr large, small;
        return ExtractIconExW(path, 0, &large, &small, 1) > 0 ? large : IntPtr.Zero;
    }

    private void RefreshWheelLines()
    {
        uint lines = 3;
        if (SystemParametersInfoW(SPI_GETWHEELSCROLLLINES, 0, &lines, 0)) _wheelLines = lines;
    }

    /// <summary>The frame band a resize grabs, in device pixels at the current DPI.</summary>
    private int FrameThickness() =>
        GetSystemMetricsForDpi(SM_CYSIZEFRAME, _dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, _dpi);

    // ---- Presenting ---------------------------------------------------------------------------

    /// <summary>Draws one frame, wherever we are called from — the loop, WM_SIZE mid-drag, WM_PAINT.</summary>
    private void Present(IntPtr hdc)
    {
        if (_host is null || _presenter is null || _presenting || _clientWidth <= 0 || _clientHeight <= 0) return;
        _presenting = true;
        try
        {
            _paintRequested = false;
            var started = Now();
            _builder.Reset();
            _host.RenderFrame(_builder, started);
            if (_chrome == WindowChrome.Unified) DrawCaptionButtons(_builder);
            _presenter.Present(_builder.Build(), hdc);
            FramesPresented++;
            _lastPresentMs = (long)Now();
            _presentTotalMs += _lastPresentMs - started;
            SyncIme();
        }
        finally
        {
            _presenting = false;
        }
    }

    private void Resized(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _clientWidth = width;
        _clientHeight = height;
        _presenter?.Resize(width, height);
        _host?.Resize(width / _scale, height / _scale);
        // Drawn NOW rather than left to the loop: a live resize runs inside DefWindowProc and the
        // loop is not running, so this message is the only chance the content has to follow the edge.
        Present(IntPtr.Zero);
    }

    /// <summary>
    /// The three window controls, drawn by the shell under <see cref="WindowChrome.Unified"/>:
    /// Windows draws nothing once the caption is gone, and a window nobody can close from the frame
    /// is not a window. Minimise, maximise/restore and close, right-aligned in the strip the app
    /// left empty, in the theme's ink — hover lights the button, and close lights it the colour
    /// every Windows user already knows.
    /// </summary>
    private void DrawCaptionButtons(DisplayListBuilder builder)
    {
        if (_host is null || _theme is null) return;
        var widthDp = _clientWidth / _scale;
        var ink = _theme.TextPrimary.Resolve(_host.Mode);
        var buttons = new[] { HTMINBUTTON, HTMAXBUTTON, HTCLOSE };
        for (var index = 0; index < buttons.Length; index++)
        {
            var code = buttons[index];
            var x = widthDp - (buttons.Length - index) * CaptionButtonWidth;
            var bounds = new Rect(x, 0, CaptionButtonWidth, TitleBarHeight);
            var glyph = ink;
            if (_hoveredCaption == code)
            {
                var wash = code == HTCLOSE
                    ? new Color(0xC4, 0x2B, 0x1C, 0xFF)
                    : new Color(ink.R, ink.G, ink.B, 0x1A);
                builder.FillRect(bounds, Paint.Solid(wash));
                if (code == HTCLOSE) glyph = new Color(0xFF, 0xFF, 0xFF, 0xFF);
            }
            var paint = Paint.Solid(glyph);
            var center = bounds.Center;
            switch (code)
            {
                case HTMINBUTTON:
                    builder.FillRect(new Rect(center.X - 5, center.Y - 0.5f, 10, 1), paint);
                    break;
                case HTMAXBUTTON:
                    if (IsZoomed(_hwnd))
                    {
                        builder.StrokeRRect(new RRect(new Rect(center.X - 5, center.Y - 3, 8, 8), new CornerRadii(1)), 1, paint);
                        builder.StrokeRRect(new RRect(new Rect(center.X - 3, center.Y - 5, 8, 8), new CornerRadii(1)), 1, paint);
                    }
                    else
                    {
                        builder.StrokeRRect(new RRect(new Rect(center.X - 5, center.Y - 5, 10, 10), new CornerRadii(1.5f)), 1, paint);
                    }
                    break;
                default:
                    var toOrigin = Matrix2D.Translation(-center.X, -center.Y);
                    var back = Matrix2D.Translation(center.X, center.Y);
                    builder.PushTransform(toOrigin * Matrix2D.Rotation(MathF.PI / 4) * back);
                    builder.FillRect(new Rect(center.X - 6.5f, center.Y - 0.5f, 13, 1), paint);
                    builder.FillRect(new Rect(center.X - 0.5f, center.Y - 6.5f, 1, 13), paint);
                    builder.Pop();
                    break;
            }
        }
    }

    /// <summary>Which caption button a client-space point (device pixels) is over, or 0.</summary>
    private nint CaptionButtonAt(int x, int y)
    {
        if (_chrome != WindowChrome.Unified || y < 0 || y >= TitleBarHeight * _scale) return 0;
        var fromRight = (_clientWidth - x) / (CaptionButtonWidth * _scale);
        if (fromRight < 0) return 0;
        return (int)fromRight switch
        {
            0 => HTCLOSE,
            1 => HTMAXBUTTON,
            2 => HTMINBUTTON,
            _ => 0,
        };
    }

    /// <summary>Whether the app put something PRESSABLE under a point in the caption strip — a
    /// control of its own toolbar, which the click belongs to rather than to a window drag.</summary>
    private bool AppControlAt(float xDp, float yDp)
    {
        if (_host?.LastFrame is not { } frame) return false;
        var point = new Point(xDp, yDp);
        foreach (var region in frame.HitRegions) if (region.Bounds.Contains(point)) return true;
        foreach (var region in frame.TextRegions) if (region.Bounds.Contains(point)) return true;
        foreach (var region in frame.DragRegions) if (region.Bounds.Contains(point)) return true;
        foreach (var region in frame.LinkRegions) if (region.Bounds.Contains(point)) return true;
        foreach (var region in frame.ScrollRegions) if (region.Bounds.Contains(point)) return true;
        return false;
    }

    private void SetCaptionHover(nint code)
    {
        if (_hoveredCaption == code) return;
        _hoveredCaption = code;
        _host?.Invalidate();
        _paintRequested = true;
    }

    // ---- IME ----------------------------------------------------------------------------------

    /// <summary>
    /// The IME is ASSOCIATED only while a field holds the caret: with a Japanese or Chinese method
    /// active, typing "k" over a button would otherwise open a composition for a chord. Re-associated
    /// with the window's default context the moment a field takes focus.
    /// </summary>
    private void SyncIme()
    {
        if (_host is null) return;
        var wanted = _host.HasTextFocus;
        if (wanted == _imeAssociated) return;
        if (wanted) ImmAssociateContextEx(_hwnd, IntPtr.Zero, IACE_DEFAULT);
        else ImmAssociateContext(_hwnd, IntPtr.Zero);
        _imeAssociated = wanted;
    }

    /// <summary>The candidate window anchors under the caret — the CJK picker appears beneath the
    /// text being composed, not in a corner of the screen.</summary>
    private void PositionImeWindows(IntPtr context)
    {
        if (_host?.CaretRect() is not { } caret) return;
        var composition = new COMPOSITIONFORM
        {
            Style = CFS_POINT,
            CurrentPos = new POINT { X = (int)(caret.X * _scale), Y = (int)(caret.Y * _scale) },
        };
        ImmSetCompositionWindow(context, &composition);
        var candidate = new CANDIDATEFORM
        {
            Index = 0,
            Style = CFS_CANDIDATEPOS,
            CurrentPos = new POINT { X = (int)(caret.X * _scale), Y = (int)((caret.Y + caret.Height) * _scale) },
        };
        ImmSetCandidateWindow(context, &candidate);
    }

    private static string CompositionString(IntPtr context, uint kind)
    {
        var bytes = ImmGetCompositionStringW(context, kind, null, 0);
        if (bytes <= 0) return "";
        var buffer = new char[bytes / sizeof(char)];
        fixed (char* destination = buffer)
        {
            bytes = ImmGetCompositionStringW(context, kind, destination, (uint)bytes);
        }
        return bytes <= 0 ? "" : new string(buffer, 0, bytes / sizeof(char));
    }

    // ---- The window procedure -----------------------------------------------------------------

    [UnmanagedCallersOnly]
    private static nint WindowProcedure(IntPtr hwnd, uint message, nuint wParam, nint lParam)
    {
        var window = _current;
        if (window is null) return DefWindowProcW(hwnd, message, wParam, lParam);
        try
        {
            return window.Handle(hwnd, message, wParam, lParam);
        }
        catch (Exception error)
        {
            // An exception must not cross into user32: it would take the process down with no
            // managed stack anyone could read. Remembered, the loop is asked to stop, and Run rethrows.
            window._fault ??= error;
            window._closed = true;
            PostQuitMessage(1);
            return 0;
        }
    }

    private nint Handle(IntPtr hwnd, uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WM_NCCALCSIZE when _chrome == WindowChrome.Unified && wParam != 0:
            {
                // Windows computes the frame; the TOP is put back where the window's edge is, so the
                // caption stops existing and the content owns the top edge. The side and bottom
                // borders stay — the shadow, the rounded corners and the resize grip live there.
                var parameters = (NCCALCSIZE_PARAMS*)lParam;
                var originalTop = parameters->Rect0.Top;
                var result = DefWindowProcW(hwnd, message, wParam, lParam);
                parameters->Rect0.Top = originalTop;
                // Maximised, the frame hangs off the screen by its own thickness; the content must not.
                if (IsZoomed(hwnd)) parameters->Rect0.Top += FrameThickness();
                return result;
            }

            case WM_NCHITTEST when _chrome == WindowChrome.Unified:
            {
                var fromWindows = DefWindowProcW(hwnd, message, wParam, lParam);
                if (fromWindows != HTCLIENT) return fromWindows;
                var point = new POINT { X = LoWord(lParam), Y = HiWord(lParam) };
                ScreenToClient(hwnd, &point);
                var frame = FrameThickness();
                if (_resizable && !IsZoomed(hwnd) && point.Y < frame)
                    return point.X < frame ? HTTOPLEFT : point.X >= _clientWidth - frame ? HTTOPRIGHT : HTTOP;
                if (point.Y < TitleBarHeight * _scale)
                {
                    var button = CaptionButtonAt(point.X, point.Y);
                    if (button != 0) return button;
                    return AppControlAt(point.X / _scale, point.Y / _scale) ? HTCLIENT : HTCAPTION;
                }
                return HTCLIENT;
            }

            case WM_NCMOUSEMOVE when _chrome == WindowChrome.Unified:
            {
                SetCaptionHover((nint)wParam is HTMINBUTTON or HTMAXBUTTON or HTCLOSE ? (nint)wParam : 0);
                var track = new TRACKMOUSEEVENT { Size = (uint)sizeof(TRACKMOUSEEVENT), Flags = TME_LEAVE | TME_NONCLIENT, Track = hwnd };
                TrackMouseEvent(&track);
                break;
            }

            case WM_NCMOUSELEAVE:
                SetCaptionHover(0);
                break;

            case WM_NCLBUTTONDOWN when _chrome == WindowChrome.Unified
                && (nint)wParam is HTMINBUTTON or HTMAXBUTTON or HTCLOSE:
                // Ours to track: DefWindowProc would try to draw a caption button that is not there.
                _pressedCaption = (nint)wParam;
                return 0;

            case WM_NCLBUTTONUP when _chrome == WindowChrome.Unified && _pressedCaption != 0:
            {
                var released = (nint)wParam;
                if (released == _pressedCaption)
                {
                    var command = released switch
                    {
                        HTMINBUTTON => SC_MINIMIZE,
                        HTMAXBUTTON => IsZoomed(hwnd) ? SC_RESTORE : SC_MAXIMIZE,
                        _ => SC_CLOSE,
                    };
                    PostMessageW(hwnd, WM_SYSCOMMAND, command, 0);
                }
                _pressedCaption = 0;
                return 0;
            }

            case WM_SIZE:
                if (wParam != SIZE_MINIMIZED) Resized((int)(lParam & 0xFFFF), (int)((lParam >> 16) & 0xFFFF));
                return 0;

            case WM_GETMINMAXINFO:
            {
                var info = (MINMAXINFO*)lParam;
                var frame = new RECT { Right = (int)(_minWidth * _scale), Bottom = (int)(_minHeight * _scale) };
                AdjustWindowRectExForDpi(&frame, _style, false, WS_EX_APPWINDOW, _dpi);
                info->MinTrackSize = new POINT { X = frame.Width, Y = frame.Height };
                return 0;
            }

            case WM_DPICHANGED:
            {
                // The monitor changed under the window, or its scale did: every dp is now a
                // different number of pixels. Windows suggests where the window should sit so it
                // keeps its size in inches; the host keeps its size in dp and rasters at the new scale.
                _dpi = (uint)HiWord(wParam);
                if (_dpi == 0) _dpi = 96;
                _scale = _dpi / BaseDpi;
                var suggested = (RECT*)lParam;
                SetWindowPos(hwnd, IntPtr.Zero, suggested->Left, suggested->Top, suggested->Width, suggested->Height,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                if (_host is not null)
                {
                    _host.RenderScale = _scale;
                    _host.Invalidate();
                }
                return 0;
            }

            case WM_PAINT:
            {
                PAINTSTRUCT paint;
                var hdc = BeginPaint(hwnd, &paint);
                if (hdc != IntPtr.Zero && _host is not null && _presenter is not null) Present(hdc);
                EndPaint(hwnd, &paint);
                return 0;
            }

            case WM_ERASEBKGND:
                return 1;   // the frame paints every pixel; erasing first is a flash

            case WM_SETCURSOR when LoWord(lParam) == HTCLIENT && _host is not null:
            {
                POINT point;
                GetCursorPos(&point);
                ScreenToClient(hwnd, &point);
                Cursors.Apply(_host.CursorAt(point.X / _scale, point.Y / _scale));
                return 1;
            }

            case WM_MOUSEMOVE when _host is not null:
            {
                if (!_trackingLeave)
                {
                    // Crossing the window edge is the one pointer move Windows never delivers unasked
                    // — without this there is no WM_MOUSELEAVE, and the last hovered box keeps its
                    // Hover diff forever.
                    var track = new TRACKMOUSEEVENT { Size = (uint)sizeof(TRACKMOUSEEVENT), Flags = TME_LEAVE, Track = hwnd };
                    _trackingLeave = TrackMouseEvent(&track);
                }
                var (x, y) = PointOf(lParam);
                _host.PointerMove(x / _scale, y / _scale);
                SetCaptionHover(0);
                return 0;
            }

            case WM_LBUTTONDOWN when _host is not null:
            {
                // Captured so a drag that leaves the window still reports its moves and its release —
                // the same guarantee AppKit's tracking mode gives the Mac for free.
                SetCapture(hwnd);
                var (x, y) = PointOf(lParam);
                var time = (uint)Environment.TickCount;
                var near = Math.Abs(x - _lastClickX) <= 4 * _scale && Math.Abs(y - _lastClickY) <= 4 * _scale;
                _clickCount = near && time - _lastClickTime <= GetDoubleClickTime() ? _clickCount + 1 : 1;
                _lastClickTime = time;
                _lastClickX = x;
                _lastClickY = y;
                _host.PressDown(x / _scale, y / _scale, _clickCount, WindowsKeys.Modifiers());
                return 0;
            }

            case WM_LBUTTONUP when _host is not null:
            {
                ReleaseCapture();
                _pressedCaption = 0;
                var (x, y) = PointOf(lParam);
                _host.PressUp(x / _scale, y / _scale);
                return 0;
            }

            case WM_MOUSELEAVE:
                _trackingLeave = false;
                _host?.PointerLeave();
                return 0;

            case WM_MOUSEWHEEL when _host is not null:
            {
                // Wheel messages carry SCREEN coordinates, unlike every other mouse message.
                var point = new POINT { X = LoWord(lParam), Y = HiWord(lParam) };
                ScreenToClient(hwnd, &point);
                var travel = WindowsKeys.WheelTravel(HiWord(wParam), _wheelLines);
                _host.ScrollBy(point.X / _scale, point.Y / _scale, travel);
                return 0;
            }

            case WM_KEYDOWN or WM_SYSKEYDOWN when _host is not null:
            {
                // The named key first — Tab, Enter, Backspace, a chord the app declared. Only what
                // nothing claimed becomes text, so Space runs the focused button instead of typing
                // one into whatever field was last touched. The text arrives NEXT, as WM_CHAR, and
                // is swallowed when the key was already answered.
                var name = WindowsKeys.NameOf((uint)wParam);
                var handled = name.Length > 0 && _host.KeyDown(name, WindowsKeys.Modifiers());
                _swallowChar = handled;
                if (handled)
                {
                    // Drawn straight away rather than left to the next tick: a caret that appears a
                    // frame after the click reads as a slow app.
                    Present(IntPtr.Zero);
                    return 0;
                }
                break;   // Alt+F4, F10 and the system menu keep working
            }

            case WM_CHAR when _host is not null:
            {
                if (_swallowChar) { _swallowChar = false; return 0; }
                var character = (char)wParam;
                // A supplementary-plane character arrives as two messages; the pair is one string.
                if (char.IsHighSurrogate(character)) { _highSurrogate = character; return 0; }
                string text;
                if (char.IsLowSurrogate(character) && _highSurrogate != 0)
                {
                    text = new string([_highSurrogate, character]);
                    _highSurrogate = '\0';
                }
                else
                {
                    text = WindowsKeys.TypedText(character, WindowsKeys.Modifiers());
                }
                if (text.Length > 0 && _host.TextInput(text)) Present(IntPtr.Zero);
                return 0;
            }

            case WM_SYSCHAR:
                // Alt+letter chords were offered to the app as WM_SYSKEYDOWN; the beep DefWindowProc
                // answers an unclaimed one with helps nobody. Alt+Space stays the system menu.
                if ((char)wParam == ' ') break;
                return 0;

            case WM_IME_STARTCOMPOSITION when _host is not null:
            {
                var context = ImmGetContext(hwnd);
                if (context != IntPtr.Zero)
                {
                    PositionImeWindows(context);
                    ImmReleaseContext(hwnd, context);
                }
                // Not passed on: the default composition window would draw the text a second time,
                // beside the marked run the field already shows.
                return 0;
            }

            case WM_IME_COMPOSITION when _host is not null:
            {
                var context = ImmGetContext(hwnd);
                if (context == IntPtr.Zero) break;
                try
                {
                    var changed = false;
                    // The platform finished composing: committed text enters the field. Handled here
                    // rather than by DefWindowProc, which would replay it as WM_CHARs — twice.
                    if (((uint)lParam & GCS_RESULTSTR) != 0)
                        changed |= _host.CommitText(CompositionString(context, GCS_RESULTSTR));
                    // The composition in flight changed ("" = cancelled).
                    if (((uint)lParam & GCS_COMPSTR) != 0)
                        changed |= _host.SetMarkedText(CompositionString(context, GCS_COMPSTR));
                    PositionImeWindows(context);
                    if (changed) Present(IntPtr.Zero);
                }
                finally
                {
                    ImmReleaseContext(hwnd, context);
                }
                return 0;
            }

            case WM_IME_ENDCOMPOSITION when _host is not null:
                if (_host.SetMarkedText("")) Present(IntPtr.Zero);
                return 0;

            case WM_SETTINGCHANGE:
                RefreshWheelLines();
                if (_followSystemTheme && WindowsTheme.IsColorSetChange(lParam)) FollowSystemTheme();
                break;

            case WM_THEMECHANGED:
                if (_followSystemTheme) FollowSystemTheme();
                break;

            case WM_ENTERSIZEMOVE:
                // Windows runs its own loop until the button comes up; a timer is what still fires
                // inside it, so motion keeps moving while the edge is dragged.
                SetTimer(hwnd, LiveResizeTimer, 16, IntPtr.Zero);
                break;

            case WM_EXITSIZEMOVE:
                KillTimer(hwnd, LiveResizeTimer);
                break;

            case WM_TIMER when wParam == LiveResizeTimer && _host is not null:
                if (_host.NeedsRender || _host.IsFrameDue(Now())) Present(IntPtr.Zero);
                return 0;

            case WM_COPYDATA:
            {
                var data = (COPYDATASTRUCT*)lParam;
                if (data->Bytes != IntPtr.Zero && data->Size >= sizeof(char))
                {
                    var text = new string((char*)data->Bytes, 0, (int)(data->Size / sizeof(char)));
                    OnCopyData?.Invoke(text.TrimEnd('\0'));
                }
                return 1;
            }

            case WM_CLOSE:
                DestroyWindow(hwnd);
                return 0;

            case WM_DESTROY:
                _closed = true;
                PostQuitMessage(0);
                return 0;
        }
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private void FollowSystemTheme()
    {
        var mode = WindowsTheme.SystemMode();
        if (_themeController is not null) _themeController.Apply(mode);
        else if (_host is not null)
        {
            _host.Mode = mode;
            WindowsTheme.ApplyToTitleBar(_hwnd, mode);
            _host.Invalidate();
        }
    }
}
