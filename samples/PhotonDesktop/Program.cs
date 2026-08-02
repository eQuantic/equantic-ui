using eQuantic.Studio;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;

// eQUANTIC STUDIO — the component gallery, running as real GPU pixels in an NSWindow. It is the
// SDK's build target and its acceptance test at once: every control the library ships is on screen,
// live, and the inspector reads the control ladder at render time rather than quoting it.
// `--self-test` presents 120 frames and exits 0 (the CI-able smoke).
// `--render-png <path>`: one frame OFFSCREEN through the NORMATIVE Reference backend with the
// CoreText service — real glyphs from the engine, saved as PNG (the W4a proof).
// `--section <name>` aims the render at one gallery section, so a screenshot can prove any of them.
var section = Array.IndexOf(args, "--section") is var s and >= 0 && args.Length > s + 1
    && Enum.TryParse<GallerySection>(args[s + 1], ignoreCase: true, out var parsed)
        ? parsed
        : GallerySection.Buttons;

if (Array.IndexOf(args, "--render-png") is var pngIndex and >= 0)
{
    var path = args.Length > pngIndex + 1 ? args[pngIndex + 1] : "equantic-studio.png";
    const int width = 1280;
    const int height = 824;

    var text = new CoreTextService();
    var host = new eQuantic.UI.Native.Components.PhotonHost(
        new StudioShell(section), PhotonTheme.Instance, ThemeMode.Light, width, height, text)
    {
        RenderScale = 2f,
        TextRasterizer = text,
        IconRasterizer = new CoreGraphicsIconRasterizer(),
        ImageLoader = new CoreGraphicsImageLoader(),
    };

    var builder = new eQuantic.UI.Native.Engine.DisplayListBuilder();
    host.RenderFrame(builder);
    using var backend = new eQuantic.UI.Native.Engine.Reference.ReferenceBackend();
    using var surface = backend.CreateSurface(width * 2, height * 2);
    backend.Render(builder.Build(), surface);
    var rgba = new byte[width * 2 * height * 2 * 4];
    surface.ReadPixelsSrgb(rgba);
    File.WriteAllBytes(path, eQuantic.UI.Native.Engine.PngCodec.Encode(width * 2, height * 2, rgba));
    Console.WriteLine($"[studio] wrote {path}");
    return 0;
}

var selfTest = args.Contains("--self-test");
var window = new PhotonWindow("eQuantic Studio", 1280, 824);
window.Run(new StudioShell(section), PhotonTheme.Instance, maxFrames: selfTest ? 120 : 0);
Console.WriteLine($"[studio] frames presented: {window.FramesPresented}");
return 0;
