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

    /// <summary>
    /// An arm is laid out by the adaptive node's PARENT — the gates are display:contents — so it is
    /// lowered on the axis the parent gave the node. A Spacer needs that axis to know whether it is
    /// a width or a height; lowered on none, it lowered to NOTHING, the arm was skipped, and the
    /// space was gone at every width it served (#670). CROSS-PIN: s6-adaptive.spec.ts.
    /// </summary>
    [Fact]
    public void ASpacerArm_KeepsItsSpaceOnTheParentsAxis()
    {
        var column = new Column();
        column.Add(new Primitives.Text("above", TypeRole.BodyM));
        column.Add(new AdaptiveNode(Spacer.Fixed(24), null, Spacer.Fixed(64)) { ExpandedFrom = 980 });
        column.Add(new Primitives.Text("below", TypeRole.BodyM));
        var row = new Row();
        row.Add(new AdaptiveNode(Spacer.Fixed(24), null, Spacer.Fixed(64)) { ExpandedFrom = 980 });

        var down = WebRealizer.Lower(column, PhotonTheme.Instance).Render().Children[1].Children;
        var across = WebRealizer.Lower(row, PhotonTheme.Instance).Render().Children[0].Children;

        down.Should().HaveCount(2, "both arms are mounted, each behind its gate");
        down[0].Children.Single().Attributes["style"].Should().Be("flex-shrink: 0; height: 24px");
        down[1].Children.Single().Attributes["style"].Should().Be("flex-shrink: 0; height: 64px");
        across[0].Children.Single().Attributes["style"].Should().Be("flex-shrink: 0; width: 24px",
            "the axis is the parent's: across a row, the same arm is a width");
    }

    /// <summary>
    /// A Stack decides whether a child is positioned by looking at it, through any component it
    /// builds, and an AdaptiveNode is neither: the stack made it one ordinary layer, so each arm's
    /// Positioned reached the realizer as a Positioned outside a Stack, degraded to its child and
    /// joined the flow (#671). The gates are display:contents, so each ARM is a layer of the stack,
    /// placed as a direct child would be: the Positioned at its anchor, anything else in its cell.
    /// CROSS-PIN: s6-adaptive.spec.ts.
    /// </summary>
    [Fact]
    public void APositionedArm_IsAnchoredInItsStack()
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle { Width = 400, Height = 300 }));
        stack.Add(new AdaptiveNode(new Primitives.Box(), null,
            new Positioned(new Primitives.Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0))
        {
            ExpandedFrom = 980,
        });

        var layer = WebRealizer.Lower(stack, PhotonTheme.Instance).Render().Children[1];

        layer.Attributes["style"].Should().Be("display: contents", "an adaptive node is no layer of its own");
        layer.Children[0].Children.Single().Attributes["style"].Should().Contain("grid-area: 1 / 1",
            "an ordinary arm takes the cell a direct child would");
        layer.Children[1].Children.Single().Attributes["style"].Should().Be(
            "position: absolute; top: 0; right: 0; z-index: 2",
            "the positioned arm is anchored at its offsets, at the depth of the node it stands in for");
    }

    /// <summary>
    /// The same rule for the rest of what a parent reads off a direct child: an arm aligns itself
    /// in its line and spans the columns of its grid. Lowered as a node of its own, an arm's
    /// align-self and span were never written. CROSS-PIN: s6-adaptive.spec.ts.
    /// </summary>
    [Fact]
    public void AnArm_AlignsItselfInItsLine_AndSpansItsGrid()
    {
        var row = new Row(cross: CrossAlign.Start);
        row.Add(new AdaptiveNode(Marker(), null, new Primitives.Box(new BoxStyle { Width = 10, Height = 10 })
        {
            AlignSelf = CrossAlign.End,
        }));
        var grid = new Grid([GridTrack.Flex(), GridTrack.Flex()]);
        grid.Add(new AdaptiveNode(Marker(), null, new Primitives.Box(new BoxStyle { Height = 10 }) { GridSpan = 2 }));

        var line = WebRealizer.Lower(row, PhotonTheme.Instance).Render().Children[0].Children;
        var cells = WebRealizer.Lower(grid, PhotonTheme.Instance).Render().Children[0].Children;

        line[1].Children.Single().Attributes["style"].Should().Contain("align-self: flex-end");
        line[0].Children.Single().Attributes["style"].Should().NotContain("align-self");
        cells[1].Children.Single().Attributes["style"].Should().Contain("grid-column: span 2");
        cells[0].Children.Single().Attributes["style"].Should().NotContain("grid-column");
    }

    /// <summary>The CSS identifier grammar, which is what a class selector can name.</summary>
    private static readonly System.Text.RegularExpressions.Regex CssIdentifier =
        new("^-?[_a-zA-Z][_a-zA-Z0-9-]*$");
}
