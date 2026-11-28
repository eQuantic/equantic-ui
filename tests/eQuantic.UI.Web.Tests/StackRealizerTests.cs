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
        node.Children[1].Attributes["style"].Should().Be("position: absolute; top: -4px; inset-inline-end: -4px; z-index: 2");
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

    private sealed class Builds(VisualNode built) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => built;
    }

    /// <summary>A component a stack expands itself keeps its key, bookmark and origin, on the element
    /// of what it built, as the funnel writes them on any other component's (#940).
    /// CROSS-PIN: stack.spec.ts asserts the same on the twin.</summary>
    [Fact]
    public void AComponentAStackExpands_KeepsItsBookmark()
    {
        var stack = new Stack();
        stack.Add(new Builds(new Positioned(new Primitives.Box(new BoxStyle { Width = 16, Height = 16 }), top: 0, end: 0))
        {
            Bookmark = "corner",
        });
        stack.Add(new Builds(new Primitives.Box(new BoxStyle { Width = 16, Height = 16 })) { Bookmark = "layer" });

        var lowered = WebRealizer.Lower(stack, PhotonTheme.Instance);

        ((HtmlElement)lowered.Children[0]).Id.Should().Be("corner");
        ((HtmlElement)((HtmlElement)lowered.Children[1]).Children[0]).Id.Should().Be("layer");
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

        // …and a size the layer fixed bounds the track already, so it keeps counting.
        var fixedHolder = CellOf(new Primitives.Box(new BoxStyle { Width = 44 }, column));
        fixedHolder.Should().NotContain("min-width").And.Contain("min-height: 0");
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
            "position: absolute; top: 0; inset-inline-end: -28px; z-index: 1; scroll-margin-top: var(--eq-anchor-offset, 0px)");
    }

    // -- A decoration past its surface (#1938, the layout spec's "A decoration past its surface") ----------------

    private static OpacityMask Field(float size) => new(
        new RadialOpacityGradient(new Point(0.5f, 0.5f), RadialExtent.ClosestSide, [new(0, 1), new(1, 0)]),
        new Primitives.Box(new BoxStyle { Width = size, Height = size }));

    /// <summary>
    /// A slab's corner field: the stack's FIRST child, a Positioned at negative offsets, lowers to an anchor whose
    /// z-index is BELOW the later child's, so it paints behind the content and above the surface's background, and the
    /// surface's Clip writes <c>overflow: hidden</c> beside its radius, which cuts what the anchor leaves past the
    /// surface at the rounded corner. CROSS-PINNED by <c>stack.spec.ts</c>, which asserts the same literals.
    /// </summary>
    [Fact]
    public void AFieldAsTheFirstChild_PaintsBehindTheContent_InsideASurfaceThatClips()
    {
        var content = new Primitives.Box(new BoxStyle { Padding = EdgeInsets.All(16) }, new Primitives.Text("slab", TypeRole.BodyM));
        var stack = new Stack();
        stack.Add(new Positioned(Field(240), top: -60, end: -60));
        stack.Add(content);
        var surface = new Primitives.Box(
            new BoxStyle { Background = PhotonTheme.Instance.Surface, CornerRadius = new CornerRadii(20), Clip = true }, stack);

        var rendered = Render(surface);

        Style(rendered).Should().Contain("overflow: hidden").And.Contain("border-radius: 20px");
        var cells = rendered.Children.Should().ContainSingle().Which.Children;
        cells.Should().HaveCount(2);
        Style(cells[0]).Should().Be("position: absolute; top: -60px; inset-inline-end: -60px; z-index: 1");
        Style(cells[1]).Should().Contain("z-index: 2", "the content is drawn after the field, so above it");
    }

    [Fact]
    public void ASurfaceThatDoesNotClip_WritesNoOverflow()
    {
        var stack = new Stack();
        stack.Add(new Positioned(Field(240), top: -60, end: -60));
        var surface = new Primitives.Box(new BoxStyle { CornerRadius = new CornerRadii(20) }, stack);

        Style(Render(surface)).Should().NotContain("overflow");
    }

    /// <summary>
    /// The hero's field of design decision 10: past the stack's top by 64 dp and its end by 80, spanning the rest.
    /// <c>StartFraction = 0.08, Start = 80</c> and <c>BottomFraction = 0.04, Bottom = 64</c> write the edges
    /// <c>calc(8% + 80px)</c> and <c>calc(4% + 64px)</c>, which leave 100% - (8% + 80px) + 80px = 92% of the stack's
    /// width and 100% - (-64px) - (4% + 64px) = 96% of its height.
    /// </summary>
    [Fact]
    public void TheHerosField_WritesTheEdgesThatGive92And96Percent()
    {
        var stack = new Stack { Width = SizeValue.Fill };
        stack.Add(new Positioned(
            new OpacityMask(new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]),
                new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill })),
            top: -64, end: -80, bottom: 64, start: 80)
        { StartFraction = 0.08f, BottomFraction = 0.04f });
        stack.Add(new Primitives.Text("profile", TypeRole.BodyM));

        var cells = Render(stack).Children;

        Style(cells[0]).Should().Be(
            "position: absolute; top: -64px; bottom: calc(4% + 64px); inset-inline-start: calc(8% + 80px); "
            + "inset-inline-end: -80px; z-index: 1");
        // The wrapper the mask makes fills the anchor, so the field is the 92% by 96% the edges leave.
        Style(cells[0].Children.Should().ContainSingle().Which).Should().Contain("width: 100%").And.Contain("height: 100%");
        Style(cells[1]).Should().Contain("z-index: 2");
    }

    private static string Style(HtmlNode node) => node.Attributes.GetValueOrDefault("style") ?? "";
}
