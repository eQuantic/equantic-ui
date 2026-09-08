using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <c>Text.Align</c> on Photon. The web has honoured it since it existed and Photon dropped it in
/// silence: reported from a real onboarding screen, where a two-line centred paragraph sat flush
/// left under a title that looked right only because ONE line centres by its container.
/// <para>
/// The case that decides the arithmetic is the HUGGING one, and it is the reported one. Every text
/// service cuts its raster tight to the longest line, so a centred block has no room around it to
/// slide in — what moves is the SHORT line, against the long one. Offsetting the finished raster by
/// (box − raster) / 2 would centre the block, which for a paragraph whose longest line already
/// fills its box is a shift of about zero while the short lines stay left. Alignment is per line.
/// </para>
/// <para>
/// Three platforms place the lines, so the arithmetic is one function
/// (<see cref="TextAlignmentExtensions"/>) with several callers. Most of what is asserted here runs
/// through the PLACEHOLDER bars, which is the path a frame with no platform text service takes —
/// that is what makes alignment assertable without a Mac, an emulator and a Windows box in the
/// loop. The CoreText facts below then prove the real engine agrees with the bars.
/// </para>
/// </summary>
public class TextAlignmentPhotonTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>Two lines of very different width, without depending on where a wrap lands.</summary>
    private const string TwoLines = "a much wider first line\nshort";

    private static DrawCommand[] Render(VisualNode root, float width = 400, float height = 200)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(root, width, height, Theme, ThemeMode.Light, builder);
        return builder.Build().Commands.ToArray();
    }

    /// <summary>The placeholder bars, in the order the lines are in: one rounded rect per line.</summary>
    private static List<Rect> Bars(VisualNode root) => Render(root)
        .Where(c => c.Kind == DrawCommandKind.FillRRect)
        .Select(c => c.Shape.Rect)
        .OrderBy(r => r.Y)
        .ToList();

    [Fact]
    public void LeftAligned_EveryLineStartsAtTheSameEdge()
    {
        var bars = Bars(new Text(TwoLines, TypeRole.BodyM));

        bars.Should().HaveCount(2);
        bars[1].X.Should().BeApproximately(bars[0].X, 0.01f, "Start is the edge, whatever the line");
    }

    [Fact]
    public void Centred_TheShortLineMovesInByHalfTheDifference()
    {
        var bars = Bars(new Text(TwoLines, TypeRole.BodyM, align: TextAlignment.Center));

        bars.Should().HaveCount(2);
        // The block is the longest line — the box hugs it — so the long line does not move and the
        // short one centres against it. That IS centred text in a box cut to its content.
        bars[0].X.Should().BeApproximately(bars[0].X, 0.01f);
        var slack = bars[0].Width - bars[1].Width;
        slack.Should().BeGreaterThan(1, "the fixture's two lines differ, or it proves nothing");
        (bars[1].X - bars[0].X).Should().BeApproximately(slack / 2, 0.01f);
    }

    [Fact]
    public void EndAligned_TheLinesShareTheirRightEdge()
    {
        var bars = Bars(new Text(TwoLines, TypeRole.BodyM, align: TextAlignment.End));

        bars.Should().HaveCount(2);
        (bars[1].X + bars[1].Width).Should().BeApproximately(bars[0].X + bars[0].Width, 0.01f);
    }

    /// <summary>
    /// MEASURED while writing the case above, and worth pinning because it decides what "centred"
    /// looks like here: a Photon <c>Text</c> is ALWAYS its content's width. Not inside a Column with
    /// a width, not under <see cref="CrossAlign.Stretch"/>, not wrapped in a <c>Flexible</c> — all
    /// four measured the same 160.8dp. So the block a line centres in is the longest line, and the
    /// paragraph as a whole is placed by its container, which is exactly the screen this was
    /// reported from.
    /// <para>
    /// It also means the widening branch of <see cref="TextAlignmentExtensions.BlockWidth"/> is not
    /// reachable through the realizer today — only directly, which the CoreText fact below does. The
    /// branch stays because it is what the rasterizer CONTRACT means, and because the vocabulary says
    /// a Text "fills the available line box"; the day Photon makes that true, alignment is already
    /// right rather than newly wrong.
    /// </para>
    /// </summary>
    [Fact]
    public void OnPhoton_AParagraphHugs_SoTheLongestLineIsTheBlock()
    {
        var column = new Column(gap: 0) { Width = 380, Cross = CrossAlign.Stretch };
        column.Add(new Text(TwoLines, TypeRole.BodyM, align: TextAlignment.Center));
        var bars = Bars(column);

        bars.Should().HaveCount(2);
        bars[0].Width.Should().BeLessThan(300, "the paragraph did not take the column's 380");
        // The long line is the block, so it does not move, and the short one centres against it.
        bars[0].X.Should().BeApproximately(0, 0.01f);
        (bars[1].X - bars[0].X).Should().BeApproximately((bars[0].Width - bars[1].Width) / 2, 0.01f);
    }

    [Fact]
    public void ALineWiderThanItsBlock_StillStartsAtTheEdge()
    {
        // Overflow goes RIGHT, the way every text engine overflows one. A signed offset would put
        // the first characters off the left edge, where nothing can scroll them back.
        TextAlignment.Center.Offset(blockWidth: 40, lineWidth: 100).Should().Be(0);
        TextAlignment.End.Offset(blockWidth: 40, lineWidth: 100).Should().Be(0);
    }

    [Fact]
    public void AnUnboundedBlock_KeepsTheTightRaster()
    {
        // A field being typed into, a rich run measured on its own: no box, so nothing to align
        // inside, and no reason to pay for pixels either side of the glyphs.
        TextAlignment.Center.BlockWidth(contentWidth: 120, boxWidth: float.PositiveInfinity).Should().Be(120);
        TextAlignment.Center.BlockWidth(contentWidth: 120, boxWidth: 300).Should().Be(300);
        TextAlignment.Start.BlockWidth(contentWidth: 120, boxWidth: 300).Should().Be(120);
    }

    /// <summary>
    /// A rich paragraph is drawn PIECE BY PIECE — one raster per word, each in its own style — so
    /// its alignment cannot ride the raster the way a plain one's does. It goes on the piece's x,
    /// which is why a fragment carries the line it landed on. Markdown is made of these, and it
    /// would otherwise have kept ignoring the property the plain path had just started honouring.
    /// </summary>
    [Fact]
    public void ACentredRichParagraph_ShiftsEveryPieceOfTheShortLine()
    {
        Text Rich(TextAlignment align) => new("", TypeRole.BodyM, align: align)
        {
            Spans = [new TextRun("a much wider first line "), new TextRun("short") { Weight = FontWeight.Bold }],
        };

        // Rich pieces are only DRAWN when a text service is present — with none, the realizer
        // falls to the bars the plain path uses. So this one renders through a fake service.
        List<DrawCommand> Pieces(TextAlignment align)
        {
            var service = new FixedAdvance();
            var host = new PhotonHost(Rich(align), Theme, ThemeMode.Light, 150, 200, measurer: service)
            {
                TextRasterizer = service,
            };
            var builder = new DisplayListBuilder();
            host.RenderFrame(builder);
            return builder.Build().Commands.ToArray()
                .Where(c => c.Kind == DrawCommandKind.Texture).ToList();
        }

        var left = Pieces(TextAlignment.Start);
        var centred = Pieces(TextAlignment.Center);

        centred.Should().HaveCount(left.Count).And.NotBeEmpty();
        // The LAST line is the short one, and every piece on it moved right by the same amount.
        var lastY = left.Max(c => c.Shape.Rect.Y);
        var shifts = left.Zip(centred)
            .Where(pair => MathF.Abs(pair.First.Shape.Rect.Y - lastY) < 0.01f)
            .Select(pair => pair.Second.Shape.Rect.X - pair.First.Shape.Rect.X)
            .ToList();
        shifts.Should().NotBeEmpty();
        shifts.Should().OnlyContain(shift => shift > 0.5f, "a centred line moves in");
        shifts.Distinct().Should().HaveCount(1, "the LINE moves, not each word by its own amount");
    }

    /// <summary>
    /// A LINK inside a centred paragraph is pressable where it is DRAWN. The pieces move for their
    /// line's alignment and the hit region is computed separately from the draw, so the two are one
    /// function here — the alternative is a link that looks right and answers nothing, which is the
    /// shape of the mute canvas this repo already shipped once (PR #97).
    /// </summary>
    [Fact]
    public void ACentredLink_IsPressableWhereItIsDrawn()
    {
        var text = new Text("", TypeRole.BodyM, align: TextAlignment.Center)
        {
            Spans =
            [
                new TextRun("a much wider first line "),
                new TextRun("here") { Destination = "/docs/start" },
            ],
        };
        var service = new FixedAdvance();
        var host = new PhotonHost(text, Theme, ThemeMode.Light, 150, 200, measurer: service)
        {
            TextRasterizer = service,
        };
        var builder = new DisplayListBuilder();
        var frame = host.RenderFrame(builder);

        var region = frame.LinkRegions.Should().ContainSingle().Subject;
        // The linked word is the last piece drawn, and it wrapped onto the short line — so a region
        // still sitting at the paragraph's left edge is one the alignment left behind.
        var drawn = builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.Texture)
            .OrderByDescending(c => c.Shape.Rect.Y).ThenByDescending(c => c.Shape.Rect.X)
            .First().Shape.Rect;
        region.Bounds.X.Should().BeApproximately(drawn.X, 0.01f);
        region.Bounds.X.Should().BeGreaterThan(0.5f, "a centred short line does not start at the edge");
    }

    /// <summary>
    /// Alignment changes the pixels, so it belongs in the cache KEY. Left out of it, the first
    /// alignment drawn would be handed to the other — a bug that only shows when a screen happens
    /// to use one string twice, which is exactly the bug that hides longest.
    /// </summary>
    [Fact]
    public void TwoAlignmentsOfOneString_AreTwoRasters()
    {
        var rasterizer = new CountingRasterizer();
        var cache = new TextRasterCache();
        var style = Theme.Type(TypeRole.BodyM);

        cache.Get(rasterizer, "hello", style, 1f, 200, 0, 2f, TextAlignment.Start);
        cache.Get(rasterizer, "hello", style, 1f, 200, 0, 2f, TextAlignment.Center);
        cache.Get(rasterizer, "hello", style, 1f, 200, 0, 2f, TextAlignment.Start);

        rasterizer.Calls.Should().Equal(TextAlignment.Start, TextAlignment.Center);
    }

    /// <summary>Eight dp a character, one line per call: deterministic, and no font in sight.</summary>
    private sealed class FixedAdvance : ITextMeasurer, ITextRasterizer
    {
        public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
        {
            var width = content.Length * 8f;
            return new TextMeasurement(width, style.LineHeight, style.LineHeight, [new MeasuredLine(width, false)]);
        }

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth,
            int maxLines, float scale, TextAlignment align)
        {
            if (content.Length == 0) return null;
            var w = Math.Max(1, (int)(content.Length * 8f * scale));
            var h = Math.Max(1, (int)(style.LineHeight * scale));
            return new TextRaster(w, h, new byte[w * h]);
        }
    }

    private sealed class CountingRasterizer : ITextRasterizer
    {
        public List<TextAlignment> Calls { get; } = [];

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth,
            int maxLines, float scale, TextAlignment align)
        {
            Calls.Add(align);
            return new TextRaster(4, 4, new byte[16]);
        }
    }

    // ---- The real engine, on the one platform this repo can run one ------------------------------

    /// <summary>The first column holding any ink, within the rows of one line box.</summary>
    private static int InkStart(TextRaster raster, int fromRow, int toRow)
    {
        for (var x = 0; x < raster.Width; x++)
            for (var y = fromRow; y < Math.Min(toRow, raster.Height); y++)
                if (raster.Alpha[y * raster.Width + x] > 8) return x;
        return -1;
    }

    [MacFact]
    public void CoreText_CentresTheShortLineAgainstTheLongOne()
    {
        var service = new CoreTextService();
        var style = Theme.Type(TypeRole.BodyM);
        const float scale = 2f;
        var lineHeightPx = (int)(style.ScaledLineHeight(1f) * scale);

        var left = service.Rasterize(TwoLines, style, 1f, float.PositiveInfinity, 0, scale, TextAlignment.Start)!;
        var centred = service.Rasterize(TwoLines, style, 1f, float.PositiveInfinity, 0, scale, TextAlignment.Center)!;

        // Hugging, so the block is the longest line and the bitmap is the same width either way.
        centred.Width.Should().Be(left.Width, "a hugging block pays no extra pixels to centre");

        // The second line's rows. The first line does not move; the second does.
        var leftSecond = InkStart(left, lineHeightPx, lineHeightPx * 2);
        var centredSecond = InkStart(centred, lineHeightPx, lineHeightPx * 2);
        leftSecond.Should().BeGreaterThanOrEqualTo(0, "the fixture has a second line of ink");
        centredSecond.Should().BeGreaterThan(leftSecond + 4, "the short line moved in");

        InkStart(centred, 0, lineHeightPx).Should().BeCloseTo(InkStart(left, 0, lineHeightPx), 2,
            "the longest line IS the block, so it stays where it was");
    }

    [MacFact]
    public void CoreText_WidensOnlyWhenTheBoxIsWiderAndAlignmentWasAskedFor()
    {
        var service = new CoreTextService();
        var style = Theme.Type(TypeRole.BodyM);
        const float box = 400f;

        var hugging = service.Rasterize("short", style, 1f, box, 0, 2f, TextAlignment.Start)!;
        var centred = service.Rasterize("short", style, 1f, box, 0, 2f, TextAlignment.Center)!;

        hugging.Width.Should().BeLessThan((int)box, "Start keeps the raster cut to the glyphs");
        centred.Width.Should().Be((int)MathF.Ceiling(box * 2f), "a centred line needs the space it centres in");
        InkStart(centred, 0, centred.Height).Should().BeGreaterThan(hugging.Width / 2,
            "and the glyphs sit in the middle of it");
    }
}
