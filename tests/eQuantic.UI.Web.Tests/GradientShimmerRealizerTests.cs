using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
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

    [Fact]
    public void SkeletonShimmer_ClippedTrack_RestHiddenGlint_MirroredGradients()
    {
        var track = Render(new Skeleton(SkeletonShape.Line, 160));

        track.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        track.Attributes["style"].Should().Contain("border-radius: 999px; overflow: hidden");

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
