using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <see cref="BoxStyle.InsetHighlight"/> on both targets (#1036). The web lowers it to an <c>inset 0 1px 0</c>
/// box-shadow entry, which paints one pixel row along the top of the padding box, inside the rounded corners. Photon
/// read the member nowhere, so a box that set it had its highlight in the browser and none on Photon. Both are pinned
/// here, on the same nodes: the web's shadow entry and the Photon pixels, row by row.
/// </summary>
public class InsetHighlightParityTests
{
    private const int Width = 200;
    private const int Height = 120;

    private static readonly Color Page = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Color Fill = Color.FromRgb(0x20, 0x40, 0x80);
    private static readonly Color Rule = Color.FromRgb(0xC0, 0x20, 0x20);
    private static readonly Color Gloss = Color.FromRgb(0xFF, 0xFF, 0xFF);

    /// <summary>A plain rounded box with the highlight, top edge at y 20, and one with a 2dp border, top edge at y 70.</summary>
    private static BoxStyle Plain() => new()
    {
        Width = 120,
        Height = 40,
        Background = new ColorToken(Fill),
        CornerRadius = new CornerRadii(8),
        InsetHighlight = new ColorToken(Gloss),
    };

    private static BoxStyle Bordered() => Plain() with { BorderWidth = 2, BorderColor = new ColorToken(Rule) };

    private static VisualNode Scene()
    {
        var stack = new Stack();
        stack.Add(new Box(new BoxStyle { Width = Width, Height = Height, Background = new ColorToken(Page) }));
        stack.Add(new Positioned(new Box(Plain()), top: 20, start: 20));
        stack.Add(new Positioned(new Box(Bordered()), top: 70, start: 20));
        return stack;
    }

    private static byte[] Render()
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(Width, Height);
        var host = new PhotonHost(Scene(), PhotonTheme.Instance, ThemeMode.Light, Width, Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        var pixels = new byte[Width * Height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    private static void ShouldBe(byte[] pixels, int x, int y, Color expected, string because)
    {
        var at = (y * Width + x) * 4;
        var actual = (pixels[at], pixels[at + 1], pixels[at + 2]);
        var wanted = (expected.R, expected.G, expected.B);
        Math.Abs(actual.Item1 - wanted.Item1).Should().BeLessThanOrEqualTo(8, $"({x}, {y}) red: {because}, read {actual}");
        Math.Abs(actual.Item2 - wanted.Item2).Should().BeLessThanOrEqualTo(8, $"({x}, {y}) green: {because}, read {actual}");
        Math.Abs(actual.Item3 - wanted.Item3).Should().BeLessThanOrEqualTo(8, $"({x}, {y}) blue: {because}, read {actual}");
    }

    [Fact]
    public void TheWeb_LowersTheHighlightToAnInsetShadowOfOnePixel()
    {
        foreach (var style in new[] { Plain(), Bordered() })
        {
            var sink = new StyleSink();
            WebRealizer.Lower(new Box(style), PhotonTheme.Instance, 1f, sink);
            sink.Css.Should().Contain($"inset 0 1px 0 {TokenCss.Value(new ColorToken(Gloss))}");
        }
    }

    [Fact]
    public void Photon_DrawsTheTopRowOfThePaddingBox_InsideTheRoundedCorners()
    {
        var pixels = Render();

        ShouldBe(pixels, 80, 20, Gloss, "the plain box's top row is the highlight, as the web's inset shadow paints it");
        ShouldBe(pixels, 80, 21, Fill, "one pixel, and the fill below it");
        ShouldBe(pixels, 20, 20, Page, "the corner pixel is outside the rounded corner, where nothing of the box paints");

        ShouldBe(pixels, 80, 70, Rule, "the bordered box's border keeps its two rows");
        ShouldBe(pixels, 80, 71, Rule, "the bordered box's border keeps its two rows");
        ShouldBe(pixels, 80, 72, Gloss, "the highlight is the padding box's top row, inside the border, where CSS paints an inset shadow");
        ShouldBe(pixels, 80, 73, Fill, "one pixel, and the fill below it");
    }

    [Fact]
    public void Photon_MatchesItsGolden()
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(Width, Height);
        var host = new PhotonHost(Scene(), PhotonTheme.Instance, ThemeMode.Light, Width, Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, "inset-highlight");
    }
}
