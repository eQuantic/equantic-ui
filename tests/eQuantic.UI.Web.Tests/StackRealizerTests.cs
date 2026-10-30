using eQuantic.UI.Web;
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

    private static string CellOf(VisualNode layer)
    {
        var stack = new Stack();
        stack.Add(layer);
        return Render(stack).Children[0].Attributes["style"]!;
    }

    /// <summary>
    /// A stack caps a layer only where Photon does: on an axis the layer fills, and on one a scroller
    /// hugs. On any other axis the layer counts toward the stack's own minimum. Written on every
    /// cell, min-0 took a fixed layer out of that minimum, so a hugging stack beside a sibling wider
    /// than the line was squeezed to 0 while its content overflowed at the same edge: the
    /// spreadsheet's row-number strip measured 0 beside a 768 grid in a 726 row (#698).
    /// CROSS-PIN: the same axes are asserted by the TS lowering spec.
    /// </summary>
    [Fact]
    public void AStackCell_IsCappedOnlyOnAnAxisTheStackCapsItsLayer()
    {
        var hugging = CellOf(new ScrollView(new Primitives.Box(new BoxStyle { Width = 4000 }), ScrollAxis.Horizontal));
        hugging.Should().Contain("min-width: 0").And.Contain("min-height: 0");

        var filling = CellOf(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));
        filling.Should().Contain("min-width: 0").And.NotContain("min-height");

        var fixedBox = CellOf(new Primitives.Box(new BoxStyle { Width = 44, Height = 24 }));
        fixedBox.Should().NotContain("min-width").And.NotContain("min-height");

        var fixedScroller = CellOf(new ScrollView(new Primitives.Box(new BoxStyle { Width = 4000 }), ScrollAxis.Horizontal)
        {
            Width = 300,
            Height = 20,
        });
        fixedScroller.Should().NotContain("min-width").And.NotContain("min-height");

        // A scroller further down sizes the track as one at the top does, and Photon caps it the same.
        var column = new Column(gap: 0);
        column.Add(new ScrollView(new Primitives.Box(new BoxStyle { Width = 4000 }), ScrollAxis.Horizontal));
        var holding = CellOf(new Pressable(new Primitives.Box(new BoxStyle(), column), () => { }));
        holding.Should().Contain("min-width: 0").And.Contain("min-height: 0");
    }

    /// <summary>
    /// A Positioned's key, bookmark and origin ride on the ANCHOR the stack builds for it, the one
    /// element that is the Positioned's, as the funnel writes them on every other node's element.
    /// The stack built the anchor without dispatching the Positioned, so a link to
    /// <c>#request</c> led nowhere and a rule on the id drew nothing (#940).
    /// </summary>
    [Fact]
    public void APositionedsKeyBookmarkAndOrigin_RideOnItsAnchor()
    {
        var stack = new Stack();
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 16, Height = 16 }), top: 0, end: -28)
        {
            Bookmark = "request",
            Key = "request-card",
            Origin = "Landing.cs:42",
        });

        var anchor = (HtmlElement)WebRealizer.Lower(stack, PhotonTheme.Instance).Children[0];

        anchor.Id.Should().Be("request");
        anchor.Key.Should().Be("request-card");
        anchor.DataAttributes!["eq-origin"].Should().Be("Landing.cs:42");
        // CROSS-PIN: asserted verbatim by the TS lowering spec (hydration parity).
        anchor.Render().Attributes["style"].Should().Be(
            "position: absolute; top: 0; right: -28px; z-index: 1; scroll-margin-top: var(--eq-anchor-offset, 0px)");
    }
}
