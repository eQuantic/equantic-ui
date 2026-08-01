using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec A3 web lowering: single-cell grid stack; Positioned = absolute anchors (signed offsets).</summary>
public class StackRealizerTests
{
    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    [Fact]
    public void Stack_LowersToASingleCellGrid_WithPlaceItems()
    {
        var stack = new Stack(Alignment.Center);
        stack.Add(new Primitives.Box(new BoxStyle { Width = 40, Height = 40 }));
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 16, Height = 16 }), top: -4, end: -4));

        var node = Render(stack);
        var style = node.Attributes["style"]!;
        style.Should().Contain("display: grid");
        style.Should().Contain("position: relative");
        // Cells stretch to the stack and align their child via flex (Fill children cover;
        // hug children anchor at the 9-point alignment — the native MeasureStack contract).
        node.Children[0].Attributes["style"].Should().Contain(
            "display: flex; grid-area: 1 / 1; z-index: 1; justify-content: center; align-items: center; width: 100%; height: 100%");
        // CROSS-PIN: asserted verbatim by the TS lowering spec (hydration parity).
        node.Children[1].Attributes["style"].Should().Be("position: absolute; top: -4px; right: -4px; z-index: 2");
    }

    [Fact]
    public void PositionedOutsideAStack_DegradesToItsChild()
    {
        Render(new Positioned(new Primitives.Text("x", TypeRole.Caption))).Tag.Should().Be("span");
    }
}
