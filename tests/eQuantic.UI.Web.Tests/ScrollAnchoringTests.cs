using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A scroll view's offset changes only because someone changed it, as it does on Photon, where
/// nothing anchors. The browser's scroll anchoring moved it whenever a windowed list swapped the
/// rows above what was on screen: the code editor's line window did it on every scroll, and a
/// match that "next" had brought into view slid back out of it, measured on /code at one step in
/// five. The TypeScript twin writes the same declaration, pinned by the parity fixture.
/// </summary>
public class ScrollAnchoringTests
{
    [Theory]
    [InlineData(ScrollAxis.Vertical)]
    [InlineData(ScrollAxis.Horizontal)]
    [InlineData(ScrollAxis.Both)]
    public void AScrollViewAnchorsNothing(ScrollAxis axis)
    {
        var style = WebRealizer.Lower(new ScrollView(new Text("line", TypeRole.BodyM), axis), PhotonTheme.Instance).Style;

        style!.OverflowAnchor.Should().Be("none");
    }
}
