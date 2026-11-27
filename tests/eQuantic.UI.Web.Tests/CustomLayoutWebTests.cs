using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the server writes for a <see cref="CustomMultiChildLayout"/> (#1556): every child, each in a wrapper at the
/// box's origin, inside the box a builder would write, positioned and hidden until the browser runs the delegate.
/// The server never runs the delegate: it has no box to measure. Its children's content is in the page, so
/// hydration adopts them and their server data.
/// </summary>
public class CustomLayoutWebTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme).Render());

    private sealed class Counting(List<string> ran) : MultiChildLayoutDelegate
    {
        public override Size Layout(BoxConstraints constraints)
        {
            ran.Add("layout");
            return new Size(10, 10);
        }
    }

    [Fact]
    public void TheServerWritesEveryChild_AndNeverRunsTheDelegate()
    {
        var ran = new List<string>();
        var html = Html(new CustomMultiChildLayout(new Counting(ran),
            [new LayoutId("a", new Text("first", TypeRole.BodyM)), new LayoutId("b", new Text("second", TypeRole.BodyM))]));

        ran.Should().BeEmpty("the server has no box to measure, and a guess would place children the browser moves");
        html.Should().Contain("first").And.Contain("second");
        html.IndexOf("first", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("second", StringComparison.Ordinal),
            "the children are written in their order, the order a reader reaches them in");
    }

    [Fact]
    public void TheBoxIsHiddenAndPositioned_AndEachChildStandsAtItsOrigin()
    {
        var html = Html(new CustomMultiChildLayout(new Counting([]), [new LayoutId(0, new Text("only", TypeRole.BodyM))]));

        html.Should().Contain("visibility: hidden").And.Contain("position: relative");
        html.Should().Contain("position: absolute").And.Contain("top: 0").And.Contain("left: 0");
        html.Should().Contain("pointer-events: none").And.Contain("pointer-events: auto",
            "the wrapper passes the pointer through to what it does not cover, and the child's box takes it");
    }

    [Fact]
    public void ASingleChildLayout_IsWrittenAsALayoutOfOne()
    {
        var single = Html(new CustomSingleChildLayout(new Plain(), new Text("only", TypeRole.BodyM)));
        var multi = Html(new CustomMultiChildLayout(new Counting([]), [new LayoutId(0, new Text("only", TypeRole.BodyM))]));

        single.Should().Be(multi);
    }

    private sealed class Plain : SingleChildLayoutDelegate;
}
