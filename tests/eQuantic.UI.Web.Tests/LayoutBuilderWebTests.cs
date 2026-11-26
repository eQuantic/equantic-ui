using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the server writes for a <see cref="LayoutBuilder"/> (#1555): the box with nothing in it. The
/// server has no box to measure, so it never asks the builder; the browser's first lowering writes the
/// same box, so hydration adopts it, and the browser builds the child once it has measured the room.
/// </summary>
public class LayoutBuilderWebTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme).Render());

    [Fact]
    public void TheServerNeverRunsTheBuilder()
    {
        var ran = false;
        Html(new LayoutBuilder(_ =>
        {
            ran = true;
            return new Text("built", TypeRole.BodyM);
        }));

        ran.Should().BeFalse("the server has no box to measure, and a guess would draw a child the browser replaces");
    }

    /// <summary>The box is exactly a box that fills on both axes and holds nothing: what the browser's
    /// lowering writes until it has measured, so the two producers agree on the markup it hydrates.</summary>
    [Fact]
    public void TheServerWritesTheBuildersBoxEmpty_AsAFillingBoxThatHoldsNothing()
    {
        var builder = Html(new LayoutBuilder(_ => new Text("built", TypeRole.BodyM)));
        var filling = Html(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));

        builder.Should().Be(filling);
        builder.Should().NotContain("built");
    }

    /// <summary>Its floor is zero on both axes, as a filling box's is, so a room narrower than what the
    /// builder built last is still the room the browser measures.</summary>
    [Fact]
    public void TheBuildersBox_HasNoFloorOfItsOwn()
    {
        var html = Html(new LayoutBuilder(_ => new Text("built", TypeRole.BodyM)));

        html.Should().Contain("min-width: 0").And.Contain("min-height: 0");
    }

    /// <summary>A held value applied on the web: the box's maximum and minimum, in points, and nothing
    /// written for an unbounded maximum.</summary>
    [Fact]
    public void AConstrainedBox_WritesTheValuesBoundsAsItsBoxs()
    {
        var html = Html(new eQuantic.UI.Components.ConstrainedBox(
            new BoxConstraints(minWidth: 100, maxWidth: 320, minHeight: 20),
            new Text("held", TypeRole.BodyM)));

        html.Should().Contain("max-width: 320px").And.Contain("min-width: 100px").And.Contain("min-height: 20px");
        html.Should().NotContain("max-height");
    }
}
