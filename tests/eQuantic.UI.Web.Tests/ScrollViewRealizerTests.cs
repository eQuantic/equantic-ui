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
}
