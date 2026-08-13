using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A SENTENCE IS NOT ONE STYLE. Photon drew a rich paragraph as a single raster of the joined
/// text, so the bold word, the inline code and the link all arrived as flat prose — the web twin
/// had been emitting a span per run since the day runs existed.
/// <para>
/// The unit is a FRAGMENT: a run on a line. It is the only piece with a rectangle, which is what
/// both halves of this need — the raster to draw in its own style, and the link to be pressable.
/// </para>
/// </summary>
public class RichTextRunTests
{
    /// <summary>Eight dp per character, and bold twice that — a fake with an OPINION, so a
    /// paragraph measured in the base style comes out visibly wrong rather than close.</summary>
    private sealed class WidthByStyle : ITextMeasurer, ITextRasterizer
    {
        public readonly List<TypeStyle> Rasterized = new();

        private static float Advance(TypeStyle style) => style.Weight >= FontWeight.Bold ? 16 : 8;

        public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
        {
            var width = content.Length * Advance(style);
            return new TextMeasurement(width, style.LineHeight, style.LineHeight,
                [new MeasuredLine(width, false)]);
        }

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth,
            int maxLines, float scale)
        {
            if (content.Length == 0) return null;
            Rasterized.Add(style);
            var w = (int)(content.Length * Advance(style) * scale);
            var h = (int)(style.LineHeight * scale);
            return new TextRaster(w, h, new byte[w * h]);
        }
    }

    private static Text Sentence() => new("", TypeRole.BodyM)
    {
        Spans =
        [
            new TextRun("Read the "),
            new TextRun("guide") { Weight = FontWeight.Bold, Destination = "/docs/start" },
            new TextRun(" now"),
        ],
    };

    private static LayoutNode LayoutOf(Text text, float width = 400)
    {
        var ctx = new LayoutContext(PhotonTheme.Instance, new WidthByStyle());
        return LayoutEngine.Layout(text, width, 200, ctx);
    }

    [Fact]
    public void EachRunIsPlacedInItsOwnStyle()
    {
        var node = LayoutOf(Sentence());

        node.TextRuns.Should().NotBeNull();
        var pieces = node.TextRuns!.Where(f => f.Content != " ").ToList();
        pieces.Select(f => f.Content).Should().Equal("Read", "the", "guide", "now");
        pieces.Single(f => f.Content == "guide").Style.Weight.Should().Be(FontWeight.Bold);
        pieces.Single(f => f.Content == "Read").Style.Weight.Should().NotBe(FontWeight.Bold);
    }

    /// <summary>
    /// The width comes from EACH run's own style. Measured in the base style, this paragraph is
    /// 5 characters of "guide" too narrow — and the difference is exactly what overflows a card.
    /// </summary>
    [Fact]
    public void TheWidthAddsUpTheRunsAsTheyAreDrawn()
    {
        var node = LayoutOf(Sentence());

        // "Read the " at 8 + "guide" at 16 + " now" at 8.
        node.Bounds.Width.Should().Be(9 * 8 + 5 * 16 + 4 * 8);
    }

    /// <summary>A paragraph breaks between WORDS and never between runs — the distinction runs
    /// exist for, since a Row of Texts breaks at the run boundaries.</summary>
    [Fact]
    public void ItWrapsBetweenWordsNotBetweenRuns()
    {
        var node = LayoutOf(Sentence(), width: 120);

        var lines = node.TextRuns!.Where(f => f.Content != " ").Select(f => f.Y).Distinct().ToList();
        lines.Count.Should().BeGreaterThan(1, "120dp cannot hold this sentence");
        // "guide" is one piece wherever it lands: a run is never cut in half by a break.
        node.TextRuns!.Count(f => f.Content == "guide").Should().Be(1);
    }

    [Fact]
    public void ALinkedRunCarriesItsDestination()
    {
        var node = LayoutOf(Sentence());

        var linked = node.TextRuns!.Where(f => f.Destination is not null).ToList();
        linked.Should().ContainSingle();
        linked[0].Content.Should().Be("guide");
        linked[0].Destination.Should().Be("/docs/start");
    }

    /// <summary>
    /// The realizer draws ONE raster per piece, each in its own style — the test the flat path
    /// cannot pass, because it asks the rasterizer for the joined string exactly once.
    /// </summary>
    [Fact]
    public void TheRasterizerIsAskedForEachRunSeparately()
    {
        var service = new WidthByStyle();
        var host = new PhotonHost(new Paragraph(), PhotonTheme.Instance, ThemeMode.Light, 400, 200,
            measurer: service)
        {
            TextRasterizer = service,
        };

        host.RenderFrame(new DisplayListBuilder());

        service.Rasterized.Should().Contain(s => s.Weight == FontWeight.Bold);
        service.Rasterized.Should().Contain(s => s.Weight != FontWeight.Bold);
    }

    /// <summary>A link in the middle of a sentence is pressable: the region is the fragment's own
    /// rectangle, and a tap on it navigates through the same seam a Link node uses.</summary>
    [Fact]
    public void TappingALinkedRunNavigates()
    {
        var service = new WidthByStyle();
        var host = new PhotonHost(new Paragraph(), PhotonTheme.Instance, ThemeMode.Light, 400, 200,
            measurer: service)
        {
            TextRasterizer = service,
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        string? navigated = null;
        host.NavigationRequested = href => navigated = href;

        var region = frame.LinkRegions.Should().ContainSingle().Subject;
        region.Destination.Should().Be("/docs/start");
        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PressUp(region.Bounds.Center.X, region.Bounds.Center.Y);

        navigated.Should().Be("/docs/start");
    }

    /// <summary>A plain paragraph keeps the single-raster path: nothing to place, nothing to key.</summary>
    [Fact]
    public void APlainParagraphIsStillOnePiece()
    {
        var node = LayoutOf(new Text("just prose", TypeRole.BodyM));

        node.TextRuns.Should().BeNull();
        node.Text.Should().NotBeNull();
    }

    /// <summary>
    /// The REAL text stack, on the pixels: the same word drawn in the paragraph's style and in the
    /// bold run's covers more ink in the second. Every assertion above rides a fake measurer, which
    /// proves the placement and nothing about the face — this is the one that proves the style
    /// reaches CoreText, and it is the whole point of drawing a run separately.
    /// </summary>
    [MacFact]
    public void EachRunReachesTheRealFaceInItsOwnStyle()
    {
        var service = new eQuantic.UI.Native.Shell.Apple.CoreTextService();
        var body = PhotonTheme.Instance.Type(TypeRole.BodyM);

        static int Ink(TextRaster raster)
        {
            var lit = 0;
            foreach (var a in raster.Alpha) if (a > 0) lit++;
            return lit;
        }

        var plain = service.Rasterize("guide", body, 1f, float.PositiveInfinity, 1, 2f)!;
        var bold = service.Rasterize("guide", body with { Weight = FontWeight.Bold }, 1f,
            float.PositiveInfinity, 1, 2f)!;

        Ink(bold).Should().BeGreaterThan(Ink(plain), "a bold run is drawn in the bold cut");
    }

    private sealed class Paragraph : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Sentence();
    }
}
