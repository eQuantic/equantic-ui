using System.Reflection;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Windows.Win32;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.Windows.WindowsPhotonRunner))]
// Which operating system this shell exists for. The host reads it: a Mac that carries this
// assembly beside its own shell — the test project, a publish for more than one desktop — must
// find ONE runner, and the Windows capabilities must not shadow the Mac's by registering first.
// Stated in source because the SDK only writes the attribute for a platform-specific target
// framework, and this shell builds for plain net10.0 on every host.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// Runs a built app in a window. The desktop is where a phone app is ITERATED on — same engine,
/// same trees, at the geometry the design was drawn for — so the options carry a size here that a
/// device would simply report for itself.
/// </summary>
public sealed class WindowsPhotonRunner : IPhotonRunner
{
    public void Run(PhotonApplication app)
    {
        // A WinExe has no console of its own, and `dotnet run` from a terminal still deserves the
        // "[photon] frames presented" line. Attaching to the parent's console is what every Windows
        // tool with a window does; a plain console app already has one and the call is a no-op.
        AttachParentConsole();

        // BEFORE any window: the awareness a process declares cannot change once one exists, and a
        // window created unaware is stretched by the system afterwards — blurry text on every
        // monitor that is not 96 dpi, which is every laptop sold this decade.
        SetProcessDpiAwarenessContext(DpiAwarenessPerMonitorV2);
        Com.EnsureInitialized();

        var options = app.Options;

        // The platform's locale, copied onto .NET BEFORE anything renders (screenshots included).
        var cultureController = app.Services.GetService(typeof(PhotonCultureController)) as PhotonCultureController;
        var (uiCulture, formatCulture) = WindowsLocale.Resolve();
        cultureController?.Apply(uiCulture, formatCulture);

        if (options.ScreenshotPath is { } screenshot)
        {
            RenderScreenshot(app, screenshot);
            return;
        }

        // The URL this process was launched with, if any — and if another instance of this app is
        // already showing a window, the URL is THEIRS: forwarded, and this process ends without
        // opening a second window over the one the person is looking at.
        var className = WindowsDeepLinks.WindowClassName(app);
        var relay = WindowsDeepLinks.Install(app);
        if (WindowsDeepLinks.LaunchUrl(app) is { } launched)
        {
            if (WindowsDeepLinks.ForwardToRunningInstance(className, launched)) return;
            relay.Offer(launched.OriginalString);
        }

        // Null FOLLOWS the system's light/dark setting; a value pins it.
        var mode = options.Mode ?? WindowsTheme.SystemMode();
        var window = new PhotonWindow(className, options.Title, options.Width, options.Height,
            options.Chrome, options.Resizable, options.MinWidth, options.MinHeight, options.SmoothScroll)
        {
            OnCopyData = text => relay.Offer(text),
        };
        // The concrete controller only — an app that registered its own IThemeController has
        // taken over the switch, and this attach quietly steps aside.
        var themeController = app.Services.GetService(typeof(IThemeController)) as PhotonThemeController;
        window.Run(app.Root(), options.Theme, mode, options.MaxFrames, themeController, cultureController,
            followSystemTheme: options.Mode is null);
        Console.WriteLine($"[photon] frames presented: {window.FramesPresented} through {window.PresenterName}, "
            + $"{window.AveragePresentMs:F0} ms per frame");
    }

    /// <summary>
    /// Headless: the SAME tree, laid out with the SAME DirectWrite metrics, rasterized by the
    /// reference backend to a PNG — what a CI screenshot step or a fidelity pass against a design
    /// handoff looks at. Motion is given time to settle so entrances don't smear the frame.
    /// </summary>
    private static void RenderScreenshot(PhotonApplication app, string path)
    {
        var options = app.Options;
        using var textService = new DirectWriteTextService();
        using var iconRasterizer = new Direct2DIconRasterizer();
        using var imageLoader = new WicImageLoader();
        var host = new PhotonHost(app.Root(), options.Theme,
            options.Mode ?? ThemeMode.Light, options.Width, options.Height, textService)
        {
            TextRasterizer = textService,
            ImageLoader = imageLoader,
            IconRasterizer = iconRasterizer,
            Density = Density.Compact,
        };

        var width = (int)options.Width;
        var height = (int)options.Height;
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);
        var builder = new DisplayListBuilder();
        for (var frame = 0; frame < 4; frame++)
        {
            builder = new DisplayListBuilder();
            host.RenderFrame(builder, frame * 800f);
        }
        backend.Render(builder.Build(), surface);

        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        File.WriteAllBytes(path, PngCodec.Encode(width, height, pixels));
        Console.WriteLine($"[photon] screenshot: {path}");
    }

    private static void AttachParentConsole()
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // No usable handles came with the console; the app runs without a log line, as before.
        }
    }
}
