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

        var sideways = Render(new ScrollView(new Primitives.Box(new BoxStyle { Width = 40 }), ScrollAxis.Horizontal)
        {
            FillViewport = true,
        });
        sideways.Attributes["style"].Should().Contain("display: flex").And.Contain("flex-direction: row");
        sideways.Children[0].Attributes["style"].Should().Contain("flex-grow: 1").And.Contain("flex-shrink: 0");
    }

    [Fact]
    public void AViewportNotFilled_LaysItsChildOutAsABlock()
    {
        var node = Render(new ScrollView(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill })) { Height = SizeValue.Fixed(100) });

        node.Attributes["style"].Should().NotContain("display: flex");
        node.Children[0].Attributes["style"].Should().NotContain("flex-grow");
    }
}
