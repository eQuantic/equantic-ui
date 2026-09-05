using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Artwork on the NATIVE side: the box a drawing takes, and the pixels it becomes.
/// <para>
/// A drawing is not an icon and the difference is the whole point of the node: an icon is one path
/// in one tint, a logo is several shapes each with a colour its designer chose. Photon draws it
/// with what it already has — one alpha raster per shape, each painted by a texture command that
/// already carried a tint — so nothing in the engine had to learn about colour rasters.
/// </para>
/// </summary>
public class DrawingTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    /// <summary>
    /// The platform's REAL rasterizer — CoreGraphics on a Mac, Direct2D on Windows — so the same
    /// assertions hold the two twins to the same answer; a host with neither skips rather than
    /// failing on a framework it does not have.
    /// </summary>
    private static IIconRasterizer PlatformRasterizer()
    {
        if (OperatingSystem.IsMacOS()) return new CoreGraphicsIconRasterizer();
        Skip.IfNot(OperatingSystem.IsWindows(), "Needs a platform icon rasterizer (CoreGraphics or Direct2D).");
        return new eQuantic.UI.Native.Shell.Windows.Graphics.Direct2DIconRasterizer();
    }

    /// <summary>A mark with two shapes in two colours, and one that asks to be tinted.</summary>
    private static VectorDrawing Mark() => SvgDocument.Parse("""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
          <rect width="120" height="40" fill="#4F46E5"/>
          <circle cx="20" cy="20" r="12" fill="#F59E0B"/>
          <path d="M44 12h60v16H44z" fill="currentColor"/>
        </svg>
        """);

    /// <summary>
    /// One number means the ARTWORK'S aspect, not a square. This is where a drawing parts company
    /// with <see cref="Vector"/>: an icon defaults to square because that is what an icon is, and a
    /// squashed logo is a wrong logo.
    /// </summary>
    [Fact]
    public void OneNumberMeansTheArtworksOwnAspect()
    {
        var node = LayoutEngine.Layout(new Drawing(Mark(), 240), 400, 300, Ctx);

        node.Bounds.Width.Should().Be(240);
        node.Bounds.Height.Should().Be(80, "the viewBox is 3:1");
    }

    /// <summary>Both numbers letterbox or stretch deliberately — the author's call, never the
    /// framework's guess.</summary>
    [Fact]
    public void BothNumbersAreObeyed()
    {
        var node = LayoutEngine.Layout(new Drawing(Mark(), 240, 240), 400, 300, Ctx);

        node.Bounds.Height.Should().Be(240);
    }

    /// <summary>
    /// The shapes reach the frame as SEPARATE tinted textures — one per painted shape, in document
    /// order, each carrying the colour the file chose. A single texture would have meant one tint
    /// for the whole mark, which is exactly what a drawing is not.
    /// </summary>
    [SkippableFact]
    public void EveryShapeIsItsOwnTintedTexture()
    {
        var host = new PhotonHost(new Drawing(Mark(), 240), PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            IconRasterizer = PlatformRasterizer(),
        };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var textures = Textures(builder);
        textures.Should().HaveCount(3, "three painted shapes, three rasters");
        textures[0].Paint.Color.Should().Be(Color.FromRgb(0x4F, 0x46, 0xE5));
        textures[1].Paint.Color.Should().Be(Color.FromRgb(0xF5, 0x9E, 0x0B));
        textures[2].Paint.Color.Should().Be(PhotonTheme.Instance.TextPrimary.Resolve(ThemeMode.Light),
            "the shape that said currentColor takes the tint, and nothing else does");
    }

    /// <summary>The tint answers `currentColor` and NOTHING else — a full-colour mark ignores it,
    /// which is what lets one node serve both a monochrome logo and a painted one.</summary>
    [SkippableFact]
    public void TheTintAnswersOnlyTheShapesThatAskedForIt()
    {
        var pink = new ColorToken(Color.FromRgb(255, 0, 128));
        var host = new PhotonHost(new Drawing(Mark(), 240) { Tint = pink },
            PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            IconRasterizer = PlatformRasterizer(),
        };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var textures = Textures(builder);
        textures[0].Paint.Color.Should().Be(Color.FromRgb(0x4F, 0x46, 0xE5), "the file chose this one");
        textures[2].Paint.Color.Should().Be(Color.FromRgb(255, 0, 128));
    }

    /// <summary>
    /// The pixels themselves. A shape rasterizes on the drawing's own grid, so the circle that sits
    /// in the left third of a 3:1 viewBox lands in the left third of the box — the check that
    /// catches a viewBox ignored, which is the failure that looks like "the logo is off".
    /// </summary>
    [SkippableFact]
    public void AShapeLandsWhereItsViewBoxPutsIt()
    {
        var drawing = Mark();
        var circle = drawing.Shapes[1];
        var glyph = new IconGlyph("", circle.Path, IconGlyphStyle.Fill, drawing.ViewBox);

        var raster = PlatformRasterizer().Rasterize(glyph, 240, 80, 1f)!;

        raster.Width.Should().Be(240);
        raster.Height.Should().Be(80);
        // cx=20 of 120 → 1/6 across; r=12 → the disc spans roughly x∈[16,64] at this scale.
        Ink(raster, 40, 40).Should().BeTrue("the disc covers its own centre");
        Ink(raster, 200, 40).Should().BeFalse("and nothing on the far side of the mark");
    }

    /// <summary>Without a platform rasterizer (tests, headless) a drawing is ONE placeholder box,
    /// not one per shape: it stands for the artwork, not for its parts.</summary>
    [Fact]
    public void HeadlessDrawsOnePlaceholderForTheWholeMark()
    {
        var host = new PhotonHost(new Drawing(Mark(), 240), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var commands = builder.Build().Commands.ToArray();
        commands.Should().NotContain(command => command.Kind == DrawCommandKind.Texture);
        commands.Count(command => command.Kind == DrawCommandKind.FillRRect).Should().Be(1);
    }

    private static List<DrawCommand> Textures(DisplayListBuilder builder) =>
        [.. builder.Build().Commands.ToArray().Where(command => command.Kind == DrawCommandKind.Texture)];

    private static bool Ink(TextRaster raster, int x, int y) =>
        raster.Alpha[y * raster.Width + x] > 8;

    /// <summary>
    /// The run, painted. A display-list assertion says the paint is a gradient; this says what a
    /// person sees — the plate running dark to bright across the mark, and the bead lit from its
    /// own centre. Both come out of the mask the rasterizer already made, filled by the engine.
    /// </summary>
    [SkippableFact]
    public void AGradientMarkPaints()
    {
        // The golden was rasterized by CoreGraphics. Direct2D fills the same paths and paints the
        // same gradient — the structural tests above hold it to that — but it anti-aliases its edges
        // differently, and a pixel-for-pixel golden is the one assertion that has to name its
        // rasterizer. A Direct2D golden is a decision for a Windows desk with a GPU, not a skip.
        Skip.IfNot(OperatingSystem.IsMacOS(), "The drawing-gradient golden is CoreGraphics output.");
        var artwork = SvgDocument.Parse(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 240 80\">"
            + "<defs>"
            + "<linearGradient id=\"plate\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">"
            + "<stop offset=\"0\" stop-color=\"#1E1B4B\"/><stop offset=\"1\" stop-color=\"#4338CA\"/>"
            + "</linearGradient>"
            + "<radialGradient id=\"bead\" cx=\"35%\" cy=\"30%\" r=\"70%\">"
            + "<stop offset=\"0\" stop-color=\"#C7D2FE\"/><stop offset=\"1\" stop-color=\"#6366F1\"/>"
            + "</radialGradient>"
            + "</defs>"
            + "<rect x=\"0\" y=\"0\" width=\"240\" height=\"80\" rx=\"16\" fill=\"url(#plate)\"/>"
            + "<circle cx=\"56\" cy=\"40\" r=\"24\" fill=\"url(#bead)\"/>"
            + "</svg>");

        var host = new PhotonHost(new Drawing(artwork, 240), PhotonTheme.Instance, ThemeMode.Light, 260, 100)
        {
            IconRasterizer = PlatformRasterizer(),
        };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(260, 100);
        backend.Render(builder.Build(), surface);
        Golden.GoldenImage.Match(surface, "drawing-gradient");
    }
}
