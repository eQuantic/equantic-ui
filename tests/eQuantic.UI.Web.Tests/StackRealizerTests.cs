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
        // …and the cell of a layer that paints NOTHING is intangible. It stretches to the whole
        // stack and carries the layer's z-index, so a closed Drawer — a bare Box — covered the
        // viewport with an invisible interactive rectangle and the compact shell's own menu button
        // could not be tapped. Only `element.click()` reached it, which is why the tests did not
        // notice. CROSS-PINNED: stack.spec.ts asserts the same property on the same cell.
        node.Children[0].Attributes["style"].Should().Contain("pointer-events: none");
        // CROSS-PIN: asserted verbatim by the TS lowering spec (hydration parity).
        node.Children[1].Attributes["style"].Should().Be("position: absolute; top: -4px; right: -4px; z-index: 2");
    }

    [Fact]
    public void PositionedOutsideAStack_DegradesToItsChild()
    {
        Render(new Positioned(new Primitives.Text("x", TypeRole.Caption))).Tag.Should().Be("span");
    }

    /// <summary>
    /// A layer holding a SCROLLER may not stretch the stack to the scroller's content.
    /// <para>
    /// A grid item's automatic minimum size is its min-content size, so the auto track grows to the
    /// widest thing inside it — and a horizontal scroll view is as wide as the longest line in the
    /// file. The code editor is where this showed: the stack swelled to 689px inside a 482px slab,
    /// the scrollbar vanished because the scroller was never narrower than its content, and the
    /// overflow was clipped by an ancestor nobody could scroll. Native measures a layer against the
    /// STACK's extent; min-0 is what says the same thing in CSS.
    /// </para>
    /// </summary>
    [Fact]
    public void AStackCell_CannotGrowPastTheStack()
    {
        var stack = new Stack();
        stack.Add(new ScrollView(new Primitives.Box(new BoxStyle { Width = 4000 }), ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fill,
        });

        var cell = Render(stack).Children[0].Attributes["style"]!;

        // CROSS-PIN: asserted verbatim by the TS lowering spec (hydration parity).
        cell.Should().Contain("min-width: 0");
        cell.Should().Contain("min-height: 0");
    }
}
