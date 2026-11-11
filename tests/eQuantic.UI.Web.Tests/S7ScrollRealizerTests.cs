using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec S7 on the web realizer: Pinned → position:sticky, Positioned.Layer → z-index,
/// ScrollAxis.Both → auto on both axes. Literal strings the TS lowering mirrors.</summary>
public class S7ScrollRealizerTests
{
    [Fact]
    public void Pinned_LowersToPositionSticky_AtTheOffset()
    {
        var style = WebRealizer.Lower(
            new Pinned(new Primitives.Box(new BoxStyle { Height = 32 }), offset: 8),
            PhotonTheme.Instance).Style!.ToCssString();

        // Chrome sits on its own plane, above anything the CONTENT can reach — see
        // ElevationStackingTests. It was 1, which a merely raised card now out-stacks.
        style.Should().Contain("position: sticky").And.Contain("top: 8px").And.Contain("z-index: 100");
    }

    [Fact]
    public void PositionedLayer_RidesTheAnchor()
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }));
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }), top: 0) { Layer = 3 });

        var element = WebRealizer.Lower(stack, PhotonTheme.Instance);
        ((HtmlElement)element.Children[1]).Style!.ToCssString().Should().Contain("z-index: 3");
    }

    [Fact]
    public void ScrollBoth_AutoOnBothAxes()
    {
        var style = WebRealizer.Lower(
            new ScrollView(new Primitives.Box(new BoxStyle { Width = 900, Height = 900 }), ScrollAxis.Both),
            PhotonTheme.Instance).Style!.ToCssString();

        style.Should().Contain("overflow-x: auto").And.Contain("overflow-y: auto");
    }

    /// <summary>
    /// A size fixed on the line's axis is not squeezed, as a box's is not: `min-width: 0` alone let a
    /// line shrink a scroller the app gave a size (#1186). On the other axis the line has nothing to
    /// take, and a hugging scroller still gives way.
    /// CROSS-PIN: <c>s7-scroll.spec.ts</c> asserts the same on the twin.
    /// </summary>
    [Fact]
    public void AScrollerFixedOnTheLinesAxis_IsNotSqueezed()
    {
        static string Style(FlexNode line, ScrollView scroller)
        {
            line.Add(scroller);
            return WebRealizer.Lower(line, PhotonTheme.Instance).Render().Children[0].Attributes["style"]!;
        }
        static ScrollView Scroller(SizeValue width, SizeValue height) =>
            new(new Primitives.Box(new BoxStyle()), ScrollAxis.Both) { Width = width, Height = height };

        Style(new Row(gap: 0), Scroller(540, SizeValue.Hug)).Should().Contain("flex-shrink: 0");
        Style(new Column(gap: 0), Scroller(SizeValue.Hug, 540)).Should().Contain("flex-shrink: 0");
        Style(new Row(gap: 0), Scroller(SizeValue.Hug, 540)).Should().NotContain("flex-shrink");
        Style(new Column(gap: 0), Scroller(540, SizeValue.Hug)).Should().NotContain("flex-shrink");
        Style(new Row(gap: 0), Scroller(SizeValue.Fill, SizeValue.Hug)).Should().NotContain("flex-shrink");
    }
}
