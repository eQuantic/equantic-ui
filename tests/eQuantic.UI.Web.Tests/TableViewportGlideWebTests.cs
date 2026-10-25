using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A keyed gliding mark on the web, as the server writes it (#984, <see cref="TableViewCell.Glide"/>): it
/// stands where the cells put it with a transition on its left, top, width and height, the scroll rides a
/// transform of its own so a scroll never glides it, and the stylesheet turns the glide off under reduced
/// motion. The runtime's twin writes the same (`table-viewport-web.spec.ts`, and the parity fixture).
/// </summary>
public class TableViewportGlideWebTests
{
    private static HtmlNode Lowered(double across)
    {
        var rows = new UniformTableSpans(200, 28);
        var columns = new UniformTableSpans(40, 96);
        var scroll = new TableScroll();
        scroll.Arrange(new Size(900, 600), columns.Total, rows.Total, 96, 28, reduceMotion: true);
        if (across != 0)
            scroll.Apply(new TableScrollRequest(1) { Offset = new TableOffset(across, 0) }, rows, columns, 1, 1);
        var viewport = new TableViewport(rows, columns, scroll) { PinnedRows = 1, PinnedColumns = 1 };
        viewport.Add(new TableViewCell(2, 2, new Box(new BoxStyle()), mark: true)
        {
            Key = "cursor",
            Glide = new MotionSpec(200, Curve.Standard),
        });
        var root = WebRealizer.Lower(viewport, PhotonTheme.Instance).Render();
        // The bands' marks come after the four bands of cells; the cells' band is the first of them.
        return root.Children.OfType<HtmlNode>().ElementAt(4).Children.OfType<HtmlNode>().First()
            .Children.OfType<HtmlNode>().First();
    }

    [Fact]
    public void AGlidingMark_StandsWhereTheCellsPutIt_WithATransition()
    {
        var mark = Lowered(0);

        mark.Attributes["style"].Should().Contain("left: 96px")
            .And.Contain("transition: left 200ms cubic-bezier(0.2, 0, 0, 1), top 200ms")
            .And.NotContain("transform");
        mark.Attributes["class"].Should().Contain("eq-glide");
    }

    [Fact]
    public void AScrolledGrid_CarriesTheMarkByATransform_NeverByItsLeft()
    {
        var mark = Lowered(96);

        mark.Attributes["style"].Should().Contain("left: 96px").And.Contain("transform: translate(-96px, 0)");
    }

    [Fact]
    public void TheStylesheet_TurnsTheGlideOffUnderReducedMotion()
    {
        ThemeStylesheet.Generate(PhotonTheme.Instance).Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-glide { transition: none !important; } }");
    }
}
