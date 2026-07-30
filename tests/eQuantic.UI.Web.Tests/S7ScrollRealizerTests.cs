using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec S7 on the web realizer: Sticky → position:sticky, Positioned.ZIndex → z-index,
/// ScrollAxis.Both → auto on both axes. Literal strings the TS lowering mirrors.</summary>
public class S7ScrollRealizerTests
{
    [Fact]
    public void Sticky_LowersToPositionSticky_AtTheOffset()
    {
        var style = WebRealizer.Lower(
            new Sticky(new Primitives.Box(new BoxStyle { Height = 32 }), offset: 8),
            PhotonTheme.Instance).Style!.ToCssString();

        style.Should().Contain("position: sticky").And.Contain("top: 8px").And.Contain("z-index: 1");
    }

    [Fact]
    public void PositionedZIndex_RidesTheAnchor()
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }));
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }), top: 0) { ZIndex = 3 });

        var element = WebRealizer.Lower(stack, PhotonTheme.Instance);
        ((Core.HtmlElement)element.Children[1]).Style!.ToCssString().Should().Contain("z-index: 3");
    }

    [Fact]
    public void ScrollBoth_AutoOnBothAxes()
    {
        var style = WebRealizer.Lower(
            new ScrollView(new Primitives.Box(new BoxStyle { Width = 900, Height = 900 }), ScrollAxis.Both),
            PhotonTheme.Instance).Style!.ToCssString();

        style.Should().Contain("overflow-x: auto").And.Contain("overflow-y: auto");
    }
}
