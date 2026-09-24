using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A box with a height CAP and no decided height bounds its child, as the layout engine does:
/// lowered as a column, a scroller inside takes the capped height and scrolls, and any other child
/// keeps the content height a block gave it. As a block, the scroller's <c>height: 100%</c>
/// resolved against no height, it grew with its content inside a box that clipped it, and nothing
/// scrolled: the code editor's MaxHeight on the web (defect 4 of docs/CODE-EDITOR-PLAN.md, measured
/// at 2844px inside 520). The lowering the TypeScript twin writes is pinned by the parity fixture's
/// <c>capped-scroller</c>.
/// </summary>
public class CappedBoxTests
{
    private static HtmlStyle? StyleOf(VisualNode node) =>
        WebRealizer.Lower(node, PhotonTheme.Instance).Style;

    private static ScrollView Scroller() => new(new Text("line", TypeRole.BodyM));

    [Fact]
    public void ACapWithNoDecidedHeight_LaysItsChildOutAsAColumn()
    {
        var style = StyleOf(new Box(new BoxStyle { MaxHeight = 120 }, Scroller()));

        style!.Display.Should().Be(Display.Flex);
        style.FlexDirection.Should().Be(FlexDirection.Column);
    }

    [Fact]
    public void ADecidedHeight_OrNoCap_OrNoChild_LeavesTheBoxABlock()
    {
        StyleOf(new Box(new BoxStyle { Height = 80, MaxHeight = 120 }, Scroller()))!.Display.Should().BeNull(
            "a decided height already reaches the child as 100%");
        StyleOf(new Box(new BoxStyle { Width = SizeValue.Fill }, Scroller()))!.Display.Should().BeNull(
            "no cap, nothing to bound");
        StyleOf(new Box(new BoxStyle { MaxHeight = 120 }))!.Display.Should().BeNull("no child to bound");
    }
}
