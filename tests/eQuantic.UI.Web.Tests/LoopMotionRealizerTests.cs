using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The animation system's web half (spec §06, slice 1): LoopMotion lowers to a layout-transparent
/// div driving the GENERATED <c>eq-slide-x</c> keyframes — endpoints ride custom properties at the
/// style tail (fractions of the element's own width, the same base as the native realizer's offset
/// math), duration rides the animation shorthand, and <c>prefers-reduced-motion</c> statically
/// disables it in the generated stylesheet. Every string here is cross-pinned byte-for-byte with the
/// TypeScript lowering (loop-motion.spec.ts).
/// </summary>
public class LoopMotionRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void LoopMotion_LowersToTheGeneratedKeyframeAnimation()
    {
        var node = Render(new LoopMotion(
            new Box(new BoxStyle { Width = 60, Height = 4 }),
            LoopEffect.SlideX, -0.35f, 1.05f, 1200));

        node.Tag.Should().Be("div");
        node.Attributes["class"].Should().Be("eq-loop");
        node.Attributes["style"].Should().Be(
            "animation: eq-slide-x 1200ms linear infinite; --eq-loop-from: -35%; --eq-loop-to: 105%",
            "endpoints ride custom properties at the style TAIL (the TS cross-pin)");
        node.Children.Should().HaveCount(1);
    }

    [Fact]
    public void ClippingBox_LowersToOverflowHidden()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 200,
            Height = 4,
            Background = Theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Full),
            Clip = true,
        }));

        // The two properties, not the two ADJACENT — a box declares its hit-target state between
        // them now, so spanning the boundary asserted the ORDER as much as the values.
        node.Attributes["style"].Should().Contain("border-radius: 999px",
            "clip follows border-radius in the HtmlStyle property order");
        node.Attributes["style"].Should().Contain("overflow: hidden");
    }

    [Fact]
    public void GeneratedStylesheet_CarriesTheKeyframes_AndTheReduceMotionFence()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain(
            "@keyframes eq-slide-x { 0% { transform: translateX(var(--eq-loop-from)); } 100% { transform: translateX(var(--eq-loop-to)); } }");
        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-loop { animation: none; } .eq-loop-rest-hidden { visibility: hidden; } }",
            "Reduce Motion statically replaces movement — the browser twin of PhotonHost.ReducedMotion");
    }

    [Fact]
    public void IndeterminateProgressBar_SweepsAFlexSegmentInsideTheClippedTrack()
    {
        var track = Render(new ProgressBar());

        // Track: SurfaceSubtle, Radius.Full, clipping.
        track.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        track.Attributes["style"].Should().Contain("overflow: hidden");

        // Layer: the animated full-width wrapper.
        var layer = track.Children[0];
        layer.Attributes["class"].Should().Be("eq-loop");
        layer.Attributes["style"].Should().Contain("animation: eq-slide-x 1200ms linear infinite");
        layer.Attributes["style"].Should().EndWith("--eq-loop-from: -35%; --eq-loop-to: 105%");

        // Segment: 30% by flex weight against the 70% spacer.
        var row = layer.Children[0];
        row.Children[0].Attributes["style"].Should().Contain("flex: 300 1 0%");
        row.Children[1].Attributes["style"].Should().Contain("flex: 700 1 0%");
    }

    [Fact]
    public void DeterminateProgressBar_KeepsTheFlexWeightSplit()
    {
        var node = Render(new ProgressBar(0.64f));
        node.Children[0].Attributes["style"].Should().Contain("flex: 640 1 0%");
        node.Children[1].Attributes["style"].Should().Contain("flex: 360 1 0%");
        node.Attributes["style"].Should().NotContain("overflow", "a determinate track never clips");
    }
}
