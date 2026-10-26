using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A gliding mark as the server writes it (#984, <see cref="TableViewCell.Glide"/>): placed as its cell is, the
/// scroll in its left and top, so a far row's mark and its cell round alike, and nothing of the glide in it.
/// The glide is the runtime's after-pass, which glides a mark in the grid from where it stood
/// (`table-viewport-web.spec.ts`), and the server's first pass has nothing to glide from.
/// </summary>
public class TableViewportGlideWebTests
{
    /// <summary>The styles of a cell and of a gliding mark over (<paramref name="row"/>, 2), in a grid of
    /// 28-point rows and 96-point columns, one of each pinned, scrolled to
    /// (<paramref name="across"/>, <paramref name="down"/>).</summary>
    private static (string Cell, string Mark) Lowered(int row, double across, double down)
    {
        var rows = new UniformTableSpans(1_048_576, 28);
        var columns = new UniformTableSpans(40, 96);
        var scroll = new TableScroll();
        scroll.Arrange(new Size(900, 600), columns.Total, rows.Total, 96, 28, reduceMotion: true);
        if (across != 0 || down != 0)
            scroll.Apply(new TableScrollRequest(1) { Offset = new TableOffset(across, down) }, rows, columns, 1, 1);
        var viewport = new TableViewport(rows, columns, scroll) { PinnedRows = 1, PinnedColumns = 1 };
        viewport.Add(new TableViewCell(row, 2, new Box(new BoxStyle())));
        viewport.Add(new TableViewCell(row, 2, new Box(new BoxStyle()), mark: true)
        {
            Key = "cursor",
            Glide = new MotionSpec(200, Curve.Standard),
        });
        var root = WebRealizer.Lower(viewport, PhotonTheme.Instance).Render();
        // The cells' band is the first, and the bands' marks come after the four bands of cells.
        string StyleIn(int band) => root.Children.OfType<HtmlNode>().ElementAt(band).Children.OfType<HtmlNode>()
            .First().Children.OfType<HtmlNode>().First().Attributes["style"] ?? "";
        return (StyleIn(0), StyleIn(4));
    }

    [Fact]
    public void AGlidingMark_IsPlacedAsItsCellIs_TheScrollInItsLeft()
    {
        var (cell, mark) = Lowered(2, 96, 0);

        mark.Should().Be(cell).And.Contain("left: 0;").And.NotContain("transform").And.NotContain("transition");
    }

    [Fact]
    public void AFarRowsMark_RoundsAsItsCellDoes()
    {
        // Review of #1102: a mark took the scroll in a transform of its own, rounded apart from its place, so at
        // row 1 000 001 scrolled 28 000 001 points down its cell stood at -1px and the mark at 0.
        var (cell, mark) = Lowered(1_000_001, 0, 28_000_001);

        cell.Should().Contain("top: -1px");
        mark.Should().Be(cell);
    }
}
