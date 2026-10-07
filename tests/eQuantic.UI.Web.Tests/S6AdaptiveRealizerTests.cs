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
        ((HtmlElement)element.Children[0]).ClassName.Should().StartWith(AdaptiveGates.CompactUntil(600));
        ((HtmlElement)element.Children[1]).ClassName.Should().StartWith(AdaptiveGates.MediumFrom(600, 840));
        ((HtmlElement)element.Children[2]).ClassName.Should().StartWith(AdaptiveGates.ExpandedFrom(840));

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
        ((HtmlElement)element.Children[0]).ClassName.Should().StartWith(AdaptiveGates.CompactUntil(840),
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

    /// <summary>
    /// A threshold is a NUMBER, and a design whose sizes are fluid switches where a clamp() crosses
    /// a value that is rarely whole. The gate's NAME carries it, and a dot in a selector opens a
    /// second class: <c>.eq-vc703.7037</c> is the class <c>eq-vc703</c> followed by <c>.7037</c>,
    /// which cannot be one, so the browser dropped every rule of the gate and each arm showed at
    /// every width — measured in Chromium (#669). The point is an underscore in the name and a dot
    /// in the media condition. CROSS-PIN: s6-adaptive.spec.ts asserts the same literals.
    /// </summary>
    [Fact]
    public void AFractionalThreshold_NamesAGateASelectorCanReach()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(
            new AdaptiveNode(Marker(), Marker(), Marker()) { ExpandedFrom = 703.7037f },
            PhotonTheme.Instance, 1f, sink);

        var gates = element.Children.Cast<HtmlElement>().Select(gate => gate.ClassName).ToList();
        gates.Should().Equal("eq-vc600", "eq-vm600-703_7037", "eq-vx703_7037");
        gates.Should().OnlyContain(gate => CssIdentifier.IsMatch(gate!),
            "a gate is reached through a class selector, so its name has to be one identifier");
        sink.Css.Should()
            .Contain(".eq-vm600-703_7037{display:none}@media (min-width: 600px) and (max-width: 703.6837px)"
                + "{.eq-vm600-703_7037{display:contents}}")
            .And.Contain(".eq-vx703_7037{display:none}@media (min-width: 703.7037px){.eq-vx703_7037{display:contents}}");
    }

    /// <summary>
    /// …and spelled the SAME by both producers. The name was C#'s <c>"0.####"</c> of a float, which
    /// rounds to seven significant digits before it rounds to four decimals: 1066.6667 came out
    /// <c>1066.667</c> here and <c>1066.6667</c> in the TypeScript twin, two classes for one gate,
    /// so the server's DOM and the client's disagreed past a thousand dp. CROSS-PIN:
    /// s6-adaptive.spec.ts asserts the same literals.
    /// </summary>
    [Theory]
    [InlineData(1066.6667f, "eq-vx1066_6667", "@media (min-width: 1066.6667px)")]
    [InlineData(2133.3333f, "eq-vx2133_3333", "@media (min-width: 2133.3333px)")]
    [InlineData(1279.9999f, "eq-vx1279_9999", "@media (min-width: 1279.9999px)")]
    public void AThresholdPastAThousand_KeepsItsFourDecimals(float threshold, string gate, string media)
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(
            new AdaptiveNode(Marker(), null, Marker()) { ExpandedFrom = threshold }, PhotonTheme.Instance, 1f, sink);

        ((HtmlElement)element.Children[1]).ClassName.Should().Be(gate);
        sink.Css.Should().Contain($"{media}{{.{gate}{{display:contents}}}}");
    }

    /// <summary>The CSS identifier grammar, which is what a class selector can name.</summary>
    private static readonly System.Text.RegularExpressions.Regex CssIdentifier =
        new("^-?[_a-zA-Z][_a-zA-Z0-9-]*$");
}
