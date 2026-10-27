using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A table's cells as the server's first pass writes them (#1081, <see cref="TableViewport.Role"/>): what a
/// cell spans, which Photon says through its place's ranges and the web through <c>aria-rowspan</c> and
/// <c>aria-colspan</c>. The runtime's twin is <c>table-viewport-web.spec.ts</c>, and the parity fixture pins
/// the rest of a table and a grid on both sides.
/// </summary>
public class TableSemanticsWebTests
{
    [Fact]
    public void ACellThatSpans_SaysWhatItSpans()
    {
        var rows = new UniformTableSpans(3, 28);
        var columns = new UniformTableSpans(4, 96);
        var scroll = new TableScroll();
        scroll.Arrange(new Size(900, 600), columns.Total, rows.Total, 96, 28, reduceMotion: true);
        var table = TableViewport(rows, columns, scroll, label: "Totals", role: TableRole.Table, children:
        [
            TableViewCell(0, 0, new Text("Quarter", TypeRole.BodyM), columns: 2, role: TableCellRole.ColumnHeader),
            TableViewCell(1, 0, new Text("Q1", TypeRole.BodyM), rows: 2),
            TableViewCell(1, 1, new Text("14,02", TypeRole.BodyM)),
        ]);

        var root = WebRealizer.Lower(table, PhotonTheme.Instance).Render();

        root.Attributes["role"].Should().Be("table");
        Cell(root, 0, 0).Attributes.Should().Contain("aria-colspan", "2").And.NotContainKey("aria-rowspan");
        Cell(root, 1, 0).Attributes.Should().Contain("aria-rowspan", "2").And.NotContainKey("aria-colspan");
        Cell(root, 1, 1).Attributes.Should().NotContainKey("aria-rowspan").And.NotContainKey("aria-colspan");
    }

    /// <summary>The element of the cell a viewport placed at a row and a column, wherever its band is, by the
    /// id its row's <c>aria-owns</c> names.</summary>
    private static HtmlNode Cell(HtmlNode root, int row, int column) => Walk(root)
        .Single(node => node.Attributes.TryGetValue("id", out var id)
            && id is { } named && named.StartsWith("eq-tcell-", StringComparison.Ordinal)
            && named.EndsWith($"-{row}-{column}", StringComparison.Ordinal));

    private static IEnumerable<HtmlNode> Walk(HtmlNode node) =>
        new[] { node }.Concat(node.Children.OfType<HtmlNode>().SelectMany(Walk));
}
