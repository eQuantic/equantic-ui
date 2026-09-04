using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The gradient + shimmer web half (animation slice 2): BoxStyle.Gradient lowers to
/// <c>background-image: linear-gradient(…)</c> with light-dark() stops (mode-free DOM, same as solid
/// fills), and the Skeleton's decorative glint carries <c>eq-loop-rest-hidden</c> so
/// prefers-reduced-motion yields the spec's plain placeholder. Cross-pinned byte-for-byte with the
/// TypeScript lowering (gradient-shimmer.spec.ts).
/// </summary>
public class GradientShimmerRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void GradientBox_LowersToBackgroundImage_AfterBackgroundColor()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Background = Theme.SurfaceSubtle,
            Gradient = new LinearGradient(new ColorToken(Color.Transparent), Theme.SurfaceHighlight),
        }));

        node.Attributes["style"].Should().Contain(
            $"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}; " +
            $"background-image: linear-gradient(to right, #00000000, {TokenCss.Value(Theme.SurfaceHighlight)})",
            "the gradient composes OVER the solid, in the HtmlStyle property order");
    }

    [Fact]
    public void GradientDirection_ToBottom_EmitsTheKeyword()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent), GradientDirection.ToBottom),
        }));

        node.Attributes["style"].Should().Contain("background-image: linear-gradient(to bottom, ");
    }

    /// <summary>
    /// The DIAGONAL axes (the "tinted tile" gradient every icon chip in the eQuantic site uses).
    /// Native runs the same <c>Paint.Linear</c> primitive with corner endpoints — the axis was
    /// always a pair of points, so the diagonals needed no engine or shader change.
    /// </summary>
    [Theory]
    [InlineData(GradientDirection.ToBottomRight, "to bottom right")]
    [InlineData(GradientDirection.ToBottomLeft, "to bottom left")]
    public void GradientDirection_Diagonals_EmitTheKeyword(GradientDirection direction, string keyword)
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 32,
            Height = 32,
            Gradient = new LinearGradient(Theme.Colors(Variant.Primary).Base, Theme.Colors(Variant.Tertiary).Base, direction),
        }));

        node.Attributes["style"].Should().Contain($"background-image: linear-gradient({keyword}, ");
    }

    /// <summary>
    /// GRADIENT TEXT: the background paints through the glyphs. `color` survives as the readable
    /// FALLBACK for browsers without background-clip:text — an invisible headline is a far worse
    /// failure than a solid one — and the prefixed twins ship for Safari.
    /// </summary>
    [Fact]
    public void GradientText_ClipsTheBackgroundToTheGlyphs_KeepingAColorFallback()
    {
        var gradient = new LinearGradient(Theme.TextPrimary, Theme.Colors(Variant.Primary).Base);
        var node = Render(new Text("Headline", TypeRole.Display) { Gradient = gradient });

        var style = node.Attributes["style"];
        style.Should().Contain($"color: {TokenCss.Value(Theme.TextPrimary)}",
            "the solid color remains the fallback when background-clip is unsupported");
        style.Should().Contain($"background-image: {TokenCss.Gradient(gradient)}");
        style.Should().Contain("-webkit-background-clip: text; background-clip: text");
        style.Should().Contain("-webkit-text-fill-color: transparent");
    }

    /// <summary>
    /// The GRID pattern lowers to the two repeating hairline layers plus the matching
    /// <c>background-size</c> — the "graph paper" backdrop, realizable on both targets with
    /// primitives that already exist (native emits the same rules as ordinary fills).
    /// </summary>
    [Fact]
    public void GridPattern_EmitsBothHairlineLayers_AndTheCellSize()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 200,
            Height = 200,
            Pattern = new GridPattern(56, Theme.Border),
        }));

        var line = TokenCss.Value(Theme.Border);
        node.Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, {line} 1px, transparent 1px), " +
            $"linear-gradient(to bottom, {line} 1px, transparent 1px)");
        node.Attributes["style"].Should().Contain("background-size: 56px 56px, 56px 56px");
    }

    /// <summary>
    /// The elliptical spotlight lowers to a CSS <c>radial-gradient()</c> with an explicit ellipse
    /// extent and a PERCENTAGE center — the exact shape the vocabulary models, so it survives a
    /// resize without recomputation.
    /// </summary>
    [Fact]
    public void Glow_LowersToAnEllipticalRadialGradient_WithAPercentageCenter()
    {
        var glow = new RadialGradient(Theme.Colors(Variant.Info).Base,
            new ColorToken(Color.Transparent), CenterX: 0.2f, CenterY: 0f, RadiusX: 800, RadiusY: 500);
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 400, Glow = glow }));

        node.Attributes["style"].Should().Contain(
            $"background-image: radial-gradient(800px 500px at 20% 0%, "
            + $"{TokenCss.Value(Theme.Colors(Variant.Info).Base)}, #00000000)");
    }

    /// <summary>All three background layers stack in CSS paint order (gradient on top, then the
    /// glow, then the grid) — the same back-to-front order the native realizer draws them.</summary>
    [Fact]
    public void AllThreeBackgroundLayers_StackInPaintOrder_WithAlignedSizes()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 300,
            Height = 300,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent)),
            Glow = new RadialGradient(Theme.Colors(Variant.Info).Base, new ColorToken(Color.Transparent),
                0.5f, 0.5f, 200, 120),
            Pattern = new GridPattern(56, Theme.Border),
        }));

        var style = node.Attributes["style"];
        var linear = style.IndexOf("linear-gradient(to right", StringComparison.Ordinal);
        var radial = style.IndexOf("radial-gradient(", StringComparison.Ordinal);
        var grid = style.IndexOf("transparent 1px", StringComparison.Ordinal);
        linear.Should().BeLessThan(radial, "the linear gradient is the topmost layer");
        radial.Should().BeLessThan(grid, "the glow sits above the grid");
        style.Should().Contain("background-size: auto, auto, 56px 56px, 56px 56px",
            "one size entry per layer — the grid contributes two");
    }

    /// <summary>With BOTH set the gradient must paint ABOVE the grid — in CSS the first layer wins,
    /// and background-size needs the `auto` placeholder so the entries stay aligned.</summary>
    [Fact]
    public void GridPattern_UnderAGradient_KeepsLayerOrderAndSizeAlignment()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 200,
            Height = 200,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent)),
            Pattern = new GridPattern(56, Theme.Border),
        }));

        var style = node.Attributes["style"];
        style.Should().Contain("background-image: linear-gradient(to right, ");
        style.Should().Contain("background-size: auto, 56px 56px, 56px 56px");
        style.IndexOf(TokenCss.Gradient(new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent))), StringComparison.Ordinal)
            .Should().BeLessThan(style.IndexOf("transparent 1px", StringComparison.Ordinal),
                "the gradient layer is listed first, so it paints on top of the grid");
    }

    [Fact]
    public void SkeletonShimmer_ClippedTrack_RestHiddenGlint_MirroredGradients()
    {
        var track = Render(new Skeleton(SkeletonShape.Line, 160));

        track.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        // The two properties, not the two ADJACENT: a box now declares its hit-target state
        // between them, and an assertion that spanned the boundary was asserting the order as
        // much as the values.
        track.Attributes["style"].Should().Contain("border-radius: 999px");
        track.Attributes["style"].Should().Contain("overflow: hidden");

        var layer = track.Children[0];
        layer.Attributes["class"].Should().Be("eq-loop eq-loop-rest-hidden",
            "the decorative glint hides entirely under prefers-reduced-motion (spec B16)");
        layer.Attributes["style"].Should().Contain("animation: eq-slide-x 1400ms linear infinite");
        layer.Attributes["style"].Should().EndWith("--eq-loop-from: -100%; --eq-loop-to: 100%");

        var glint = layer.Children[0];
        var highlight = TokenCss.Value(Theme.SurfaceHighlight);
        glint.Children[0].Children[0].Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, #00000000, {highlight})");
        glint.Children[1].Children[0].Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, {highlight}, #00000000)");
    }

    [Fact]
    public void GeneratedStylesheet_HidesRestHiddenLoops_AndCarriesTheHighlightToken()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-loop { animation: none; } .eq-loop-rest-hidden { visibility: hidden; } }");
        css.Should().Contain($"--eq-color-surface-highlight: {TokenCss.Value(Theme.SurfaceHighlight)};");
    }
}
