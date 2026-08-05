using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.MacOS.MacOSPhotonRunner))]

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

        var options = app.Options;
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
        window.Run(app.Root(), options.Theme, options.Mode ?? ThemeMode.Light, options.MaxFrames,
            themeController);
        Console.WriteLine($"[photon] frames presented: {window.FramesPresented}");
    }

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
        var host = new Components.PhotonHost(app.Root(), options.Theme,
            options.Mode ?? ThemeMode.Light, options.Width, options.Height, textService)
        {
            TextRasterizer = textService,
            ImageLoader = new Shell.Apple.CoreGraphicsImageLoader(),
            IconRasterizer = new Shell.Apple.CoreGraphicsIconRasterizer(),
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
        Console.WriteLine($"[photon] screenshot: {path}");
    }
}
