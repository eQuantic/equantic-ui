using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec A6 web lowering: native browser scrolling (overflow auto on the axis, hidden cross).</summary>
public class ScrollViewRealizerTests
{
    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    [Fact]
    public void Vertical_LowersToOverflowYAuto()
    {
        var node = Render(new ScrollView(new Primitives.Text("content")) { Height = SizeValue.Fixed(100) });
        var style = node.Attributes["style"]!;
        style.Should().Contain("height: 100px");
        // CROSS-PIN: the same fragment the TS lowering emits (canonical order overflow-x before -y).
        style.Should().Contain("overflow-x: hidden; overflow-y: auto");
    }

    [Fact]
    public void Horizontal_LowersToOverflowXAuto()
    {
        Render(new ScrollView(new Primitives.Text("row"), ScrollAxis.Horizontal))
            .Attributes["style"].Should().Contain("overflow-x: auto; overflow-y: hidden");
    }

    /// <summary>
    /// Content that fills its viewport (#1143) is a flex line on the scroller's axis: the child grows to the
    /// scroller's content box, which leaves a scrollbar that takes space out, never shrinks under its content,
    /// and is stretched across. CROSS-PIN with the TS twin (scroll-viewport.spec.ts).
    /// </summary>
    [Fact]
    public void AViewportFilledByItsChild_LaysItOutAsAGrowingFlexItem()
    {
        var vertical = Render(new ScrollView(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill }))
        {
            Height = SizeValue.Fixed(100),
            FillViewport = true,
        });
        vertical.Attributes["style"].Should().Contain("display: flex").And.Contain("flex-direction: column");
        vertical.Children[0].Attributes["style"].Should().Contain("flex-grow: 1").And.Contain("flex-shrink: 0");

        var sideways = Render(new ScrollView(new Primitives.Box(new BoxStyle { Height = 20 }), ScrollAxis.Horizontal)
        {
            FillViewport = true,
        });
        sideways.Attributes["style"].Should().Contain("display: flex").And.Contain("flex-direction: row");
        sideways.Children[0].Attributes["style"].Should().Contain("flex-grow: 1").And.Contain("flex-shrink: 0");
    }

    /// <summary>A length the child states on the scroll axis stands, as on Photon: it never shrinks, and it
    /// does not grow either (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AViewportFilledByItsChild_KeepsALengthTheChildStates()
    {
        var node = Render(new ScrollView(new Primitives.Box(new BoxStyle { Width = 40 }), ScrollAxis.Horizontal)
        {
            FillViewport = true,
        });

        node.Children[0].Attributes["style"].Should().Contain("flex-shrink: 0").And.NotContain("flex-grow");
    }

    /// <summary>Each arm of an adaptive child is the line's item, since the gates are display:contents and a
    /// grow on one does nothing (found by Copilot reviewing #1158). CROSS-PIN with the TS twin.</summary>
    [Fact]
    public void AViewportFilledByAnAdaptiveChild_GrowsEachArm()
    {
        var node = Render(new ScrollView(new AdaptiveNode(
            new Primitives.Box(new BoxStyle { Width = SizeValue.Fill }),
            expanded: new Primitives.Box(new BoxStyle { Width = SizeValue.Fill })))
        {
            Height = SizeValue.Fixed(100),
            FillViewport = true,
        });

        var arms = Descendants(node).Where(n => n.Attributes.TryGetValue("style", out var style)
            && style!.Contains("width: 100%")).ToList();
        arms.Should().HaveCount(2);
        arms.Should().OnlyContain(arm => arm.Attributes["style"]!.Contains("flex-grow: 1"));
    }

    /// <summary>Through a wrapper that draws no box, an InView's, the item is the box inside, since a grow on a
    /// display:contents element does nothing (found by Copilot reviewing #1158). CROSS-PIN with the TS twin.</summary>
    [Fact]
    public void AViewportFilledThroughAWrapperThatDrawsNoBox_GrowsTheBoxInside()
    {
        var node = Render(new ScrollView(new InView(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill }), _ => { }))
        {
            Height = SizeValue.Fixed(100),
            FillViewport = true,
        });

        var box = Descendants(node).Single(n => n.Attributes.TryGetValue("style", out var style) && style!.Contains("width: 100%"));
        box.Attributes["style"].Should().Contain("flex-grow: 1").And.Contain("flex-shrink: 0");
    }

    private static IEnumerable<HtmlNode> Descendants(HtmlNode node) =>
        node.Children.SelectMany(child => new[] { child }.Concat(Descendants(child)));

    [Fact]
    public void AViewportNotFilled_LaysItsChildOutAsABlock()
    {
        var node = Render(new ScrollView(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill })) { Height = SizeValue.Fixed(100) });

        node.Attributes["style"].Should().NotContain("display: flex");
        node.Children[0].Attributes["style"].Should().NotContain("flex-grow");
    }
}
