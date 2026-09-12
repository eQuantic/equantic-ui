using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.MacOS.MacOSPhotonRunner))]
// Which operating system this shell exists for, read by the host: a machine that carries this
// assembly beside another desktop's shell — a publish for more than one desktop — finds ONE
// runner. Stated in source because the SDK writes the attribute only for platform-specific target
// frameworks, and this shell builds for plain net10.0; the phone shells get theirs from their TFMs.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("macos")]

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// Runs a built app in a window. The desktop is where a phone app is ITERATED on — same engine,
/// same trees, at the geometry the design was drawn for — so the options carry a size here that a
/// device would simply report for itself.
/// </summary>
public sealed class MacOSPhotonRunner : IPhotonRunner
{
    public void Run(PhotonApplication app)
    {
        // AppKit is a MAIN-THREAD framework: an NSWindow built anywhere else takes the whole
        // process down with an ObjC exception nobody can catch. Saying so in .NET terms, before
        // touching AppKit, is the difference between a fixable mistake and a crash log.
        if (!Shell.Apple.ObjC.SendBool(Shell.Apple.ObjC.objc_getClass("NSThread"),
                Shell.Apple.ObjC.Sel("isMainThread")))
        {
            throw new InvalidOperationException(
                "A Photon window must be created on the MAIN thread — AppKit requires it. Call "
                + "Run() from Main (the SDK's generated entry point already does), or dispatch to "
                + "the main queue first. For a headless render use --Photon:ScreenshotPath, which "
                + "needs no window at all.");
        }

        // BEFORE anything else, and before the run loop exists: a cold launch delivers its URL as
        // an AppleEvent within milliseconds of the process starting, so the handler has to be
        // listening already. Installing it when a component first asks for IDeepLinks is installing
        // it after the only delivery that mattered. Idempotent — the container hands out this same
        // instance.
        AppKit.LoadFrameworks();
        Shell.Apple.AppleDeepLinks.Install();

        var options = app.Options;

        // The platform's locale, copied onto .NET BEFORE anything renders (screenshots included):
        // a Finder-launched process has no LANG/LC_*, so without this read the app formats
        // invariant on a pt-BR machine. NSLocale carries the D13 pair natively.
        var cultureController = app.Services.GetService(typeof(PhotonCultureController)) as PhotonCultureController;
        var (uiCulture, formatCulture) = AppleLocale.Resolve();
        cultureController?.Apply(uiCulture, formatCulture);

        if (options.ScreenshotPath is { } screenshot)
        {
            RenderScreenshot(app, screenshot);
            return;
        }
        var window = new PhotonWindow(options.Title, options.Width, options.Height,
            options.Chrome, options.Resizable, options.MinWidth, options.MinHeight,
            options.SmoothScroll);
        // The concrete controller only — an app that registered its own IThemeController has
        // taken over the switch, and this attach quietly steps aside.
        var themeController = app.Services.GetService(typeof(IThemeController)) as PhotonThemeController;
        // Null MEANS follow the system, which is what the option has always documented and what
        // iOS and Android have always done. This shell answered Light regardless, so a Mac in dark
        // mode opened every Photon app light.
        window.Run(app.Root(), options.Theme, options.Mode ?? SystemMode(), options.MaxFrames,
            themeController, cultureController);
        ReportFrames($"frames presented: {window.FramesPresented}", app);
    }

    /// <summary>
    /// The frame summary, plus what the render CONTAINED. A boundary is meant to turn a crash into
    /// a small red box, and every automated signal — frames presented, exit code, the accessibility
    /// count — sides with the box. So the names go on the line everyone reads, and StrictRender
    /// decides whether the exit code says so too.
    /// </summary>
    private static void ReportFrames(string summary, PhotonApplication app)
    {
        var contained = eQuantic.UI.Primitives.ComponentBoundary.Contained;
        Console.WriteLine(contained.Count == 0
            ? $"[photon] {summary}"
            : $"[photon] {summary} — CONTAINED: {string.Join(", ", contained)}");
        if (contained.Count > 0 && app.Options.StrictRender) Environment.ExitCode = 1;
    }

    /// <summary>What the machine is set to, falling back to light when it cannot be read — the
    /// same fallback this shell used to apply unconditionally.</summary>
    private static ThemeMode SystemMode() => Shell.Apple.AppleAppearance.Resolve() ?? ThemeMode.Light;

    /// <summary>
    /// Headless: the SAME tree, laid out with the SAME CoreText metrics, rasterized by the
    /// reference backend to a PNG — what a CI screenshot step or a fidelity pass against a
    /// design handoff looks at. Motion is given time to settle so entrances don't smear the frame.
    /// </summary>
    private static void RenderScreenshot(PhotonApplication app, string path)
    {
        AppKit.LoadFrameworks();
        var options = app.Options;
        var textService = new Shell.Apple.CoreTextService();
        var mode = options.Mode ?? SystemMode();
        // The theme controller learns the mode here too, exactly as PhotonWindow.Run does when
        // attaching it. Without this the app's OWN theme switch falls back to its default while the
        // frame renders in the resolved mode — a screenshot whose controls misreport the state it
        // is a picture of. It went unnoticed while the fallback happened to BE the rendered mode;
        // the day the shells started following the system, the Studio's Light/Dark segmented
        // control sat on "Light" over a dark frame. Nothing re-renders from a sink here, so the
        // seeding is the whole of it.
        (app.Services.GetService(typeof(IThemeController)) as PhotonThemeController)?.Attach(mode, _ => { });

        var host = new Components.PhotonHost(app.Root(), options.Theme,
            mode, options.Width, options.Height, textService)
        {
            TextRasterizer = textService,
            ImageLoader = new Shell.Apple.CoreGraphicsImageLoader(),
            IconRasterizer = new Shell.Apple.CoreGraphicsIconRasterizer(),
            Density = Density.Compact,
        };

        var width = (int)options.Width;
        var height = (int)options.Height;
        using var backend = new Engine.Reference.ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);
        var builder = new Engine.DisplayListBuilder();
        for (var frame = 0; frame < 4; frame++)
        {
            builder = new Engine.DisplayListBuilder();
            host.RenderFrame(builder, frame * 800f);
        }
        backend.Render(builder.Build(), surface);

        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        File.WriteAllBytes(path, Engine.PngCodec.Encode(width, height, pixels));
        // The headless path needs this MORE than the windowed one, not less: a CI screenshot step
        // is exactly where nobody is looking at the picture.
        ReportFrames($"screenshot: {path}", app);
    }
}
