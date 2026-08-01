using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec S6 on the web realizer: every declared variant renders inside a size-class GATE —
/// fixed classes whose media blobs (byte-identical to the TS twin) show it only in its range.</summary>
public class S6AdaptiveRealizerTests
{
    private static Primitives.Box Marker() => new(new BoxStyle { Width = 10, Height = 10 });

    [Fact]
    public void ThreeVariants_GateAsCompactMediumExpanded()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new AdaptiveNode(Marker(), Marker(), Marker()),
            PhotonTheme.Instance, 1f, sink);

        element.Children.Should().HaveCount(3);
        ((Core.HtmlElement)element.Children[0]).ClassName.Should().StartWith(AdaptiveGates.CompactUntil(600));
        ((Core.HtmlElement)element.Children[1]).ClassName.Should().StartWith(AdaptiveGates.MediumFrom(600, 840));
        ((Core.HtmlElement)element.Children[2]).ClassName.Should().StartWith(AdaptiveGates.ExpandedFrom(840));

        sink.Css.Should().Contain(".eq-vc600{display:contents}@media (min-width: 600px){.eq-vc600{display:none}}")
            .And.Contain("@media (min-width: 600px) and (max-width: 839.98px){.eq-vm600-840{display:contents}}")
            .And.Contain("@media (min-width: 840px){.eq-vx840{display:contents}}");
    }

    [Fact]
    public void CompactPlusExpanded_CompactServesTheMediumRange()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new AdaptiveNode(Marker(), medium: null, expanded: Marker()),
            PhotonTheme.Instance, 1f, sink);

        element.Children.Should().HaveCount(2);
        ((Core.HtmlElement)element.Children[0]).ClassName.Should().StartWith(AdaptiveGates.CompactUntil(840),
            "no Medium variant → Compact hides only at 840dp (the fallback chain, same as native Resolve)");
        sink.Css.Should().Contain("@media (min-width: 840px){.eq-vc840{display:none}}");
    }

    [Fact]
    public void LoneCompact_IsNotGatedAtAll()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new AdaptiveNode(Marker()), PhotonTheme.Instance, 1f, sink);
        sink.Css.Should().NotContain("@media", "a lone Compact IS the tree at every size");
        element.ClassName.Should().NotContain("eq-v");
    }
}
