using eQuantic.UI.Components;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;

// The first REAL Photon window: the same write-once components the web serves, running as GPU
// pixels in an NSWindow — press visuals, hover diffs and the wave-3 anchored Select all live.
// `--self-test` presents 120 frames and exits 0 (the CI-able smoke).
// `--render-png <path>`: render one frame OFFSCREEN through the NORMATIVE Reference backend
// with the CoreText service — REAL glyphs from the engine, saved as PNG (the W4a proof).
if (Array.IndexOf(args, "--render-png") is var pngIndex and >= 0)
{
    var path = args.Length > pngIndex + 1 ? args[pngIndex + 1] : "photon-desktop.png";
    var text = new eQuantic.UI.Native.Shell.MacOS.CoreTextService();
    var host = new eQuantic.UI.Native.Components.PhotonHost(
        new DesktopDemo(), PhotonTheme.Instance, ThemeMode.Light, 800, 600, text)
    {
        RenderScale = 2f,
        TextRasterizer = text,
        IconRasterizer = new eQuantic.UI.Native.Shell.MacOS.CoreGraphicsIconRasterizer(),
    };
    var builder = new eQuantic.UI.Native.Engine.DisplayListBuilder();
    host.RenderFrame(builder);
    using var backend = new eQuantic.UI.Native.Engine.Reference.ReferenceBackend();
    using var surface = backend.CreateSurface(1600, 1200);
    backend.Render(builder.Build(), surface);
    var rgba = new byte[1600 * 1200 * 4];
    surface.ReadPixelsSrgb(rgba);
    File.WriteAllBytes(path, eQuantic.UI.Native.Engine.PngCodec.Encode(1600, 1200, rgba));
    Console.WriteLine($"[photon-shell] wrote {path}");
    return 0;
}

var selfTest = args.Contains("--self-test");
var window = new PhotonWindow("eQuantic.UI — Photon", 800, 600);
window.Run(new DesktopDemo(), PhotonTheme.Instance,
    maxFrames: selfTest ? 120 : 0);
Console.WriteLine($"[photon-shell] frames presented: {window.FramesPresented}");
return 0;

/// <summary>A write-once demo page: counter + variants row + a wave-3 Select, all library parts.</summary>
sealed class DesktopDemo : StatefulComponent
{
    private int _count;
    private int _size = 1;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var actions = new Row(gap: Space.S2);
        actions.Add(new Button($"Count: {_count}", onPressed: () => SetState(() => _count++)));
        actions.Add(new Button("Outline", Variant.Outline, onPressed: () => SetState(() => _count = 0)));
        actions.Add(new Button("Ghost", Variant.Ghost));

        var body = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        body.Add(new Text("Photon on macOS", TypeRole.Heading));
        body.Add(new Text("The same write-once tree the web serves — now as real GPU pixels.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
        body.Add(actions);
        body.Add(new ProgressBar(_count % 10 / 10f));
        body.Add(new Select(new[] { "Small", "Medium", "Large" }, _size,
            onChanged: i => SetState(() => _size = i)));

        var icons = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        foreach (var glyph in new[] { Icons.Search, Icons.CheckCircle, Icons.Warning, Icons.Heart, Icons.Mail, Icons.Person })
            icons.Add(new Icon(glyph, IconSize.Md, theme.TextSecondary));
        icons.Add(new Icon(Icons.HeartFilled, IconSize.Lg, theme.Colors(Variant.Destructive).Base));
        body.Add(icons);

        var page = new Column(gap: 0) { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S6) };
        page.Add(new Card(body) { Width = SizeValue.Fill });
        return page;
    }
}
