using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A table and a grid on Photon (#1081, <see cref="TableViewport.Role"/>): the viewport announces its role
/// with the whole grid's extent, and each built cell its own role and place, named by what it holds, the
/// selection and the current cell in a grid; the corner heads the row headers' column; a group stays what it was. The web's
/// twin is <c>table-viewport-web.spec.ts</c>.
/// </summary>
public class TableSemanticsTests
{
    /// <summary>A table of Excel's extent, its first row and column pinned and their headers, the corner
    /// among the headers of the columns, rows 2 to 4 of column 2 selected and the one of row 3 current.</summary>
    private static PhotonHost Open(TableRole role)
    {
        var table = new TableView(new UniformTableSpans(1_048_577, 28), new UniformTableSpans(16_385, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Label = "Ledger",
            Role = role,
            CellRole = (row, column) => row == 0 ? TableCellRole.ColumnHeader
                : column == 0 ? TableCellRole.RowHeader
                : TableCellRole.Cell,
            Selected = (row, column) => column == 2 && row >= 2 && row <= 4,
            Current = new TableRange(3, 2),
        };
        var host = new PhotonHost(table, PhotonTheme.Instance, ThemeMode.Light, 900, 600) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static SemanticNode At(PhotonHost host, int row, int column) =>
        host.Semantics().Single(node => node.Place == new TableRange(row, column));

    [Fact]
    public void AGrid_CarriesItsExtent_AndEachCellItsRolePlaceAndState()
    {
        var host = Open(TableRole.Grid);

        var grid = host.Semantics().Single(node => node.Role == SemanticRole.Grid);
        grid.Label.Should().Be("Ledger");
        grid.Extent.Should().Be(new TableRange(0, 0, 1_048_577, 16_385), "the whole grid, not the cells built");
        At(host, 3, 2).Should().Match<SemanticNode>(node =>
            node.Role == SemanticRole.GridCell && node.Label == "3:2" && node.Selected == true && node.Current);
        At(host, 5, 2).Selected.Should().BeFalse();
        At(host, 0, 2).Should().Match<SemanticNode>(node => node.Role == SemanticRole.ColumnHeader && node.Label == "0:2");
        At(host, 3, 0).Should().Match<SemanticNode>(node => node.Role == SemanticRole.RowHeader && node.Label == "3:0");
        At(host, 0, 0).Should().Match<SemanticNode>(node => node.Role == SemanticRole.ColumnHeader && node.Label == "0:0");
        host.Semantics().Should().NotContain(node => node.Role == SemanticRole.StaticText && node.Label == "3:2",
            "a cell's text is its name, not a paragraph after it");
        host.Semantics().Count(node => node.Current).Should().Be(1);
    }

    [Fact]
    public void AnEmptyCellOfATable_IsStillACell_ReadAsBlank()
    {
        // The web keeps it so that no cell after it in its row moves onto the column before (TableCellRole);
        // Photon holds the same cell, so a reader meets a blank cell on both.
        var table = new TableView(new UniformTableSpans(4, 28), new UniformTableSpans(4, 96),
            (row, column) => row == 2 && column == 1 ? null : new Text($"{row}:{column}", TypeRole.BodyM))
        {
            Label = "Ledger",
            Role = TableRole.Table,
        };
        var host = new PhotonHost(table, PhotonTheme.Instance, ThemeMode.Light, 900, 600) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);

        At(host, 2, 1).Should().Match<SemanticNode>(node => node.Role == SemanticRole.Cell && node.Label.Length == 0);
        At(host, 2, 2).Label.Should().Be("2:2");
    }

    [Fact]
    public void ATable_ReadsAsCells_WithNoSelection()
    {
        var host = Open(TableRole.Table);

        host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.Table);
        At(host, 3, 2).Should().Match<SemanticNode>(node =>
            node.Role == SemanticRole.Cell && node.Selected == null && !node.Current);
    }

    [Fact]
    public void ACellThatSpans_SaysWhatItSpans()
    {
        var table = TableViewport(new UniformTableSpans(3, 28), new UniformTableSpans(4, 96), new TableScroll(),
            label: "Totals", role: TableRole.Table, children:
            [
                TableViewCell(0, 0, new Text("Quarter", TypeRole.BodyM), columns: 2, role: TableCellRole.ColumnHeader),
                TableViewCell(1, 0, new Text("Q1", TypeRole.BodyM), rows: 2),
                TableViewCell(1, 1, new Text("14,02", TypeRole.BodyM)),
            ]);
        var host = new PhotonHost(table, PhotonTheme.Instance, ThemeMode.Light, 900, 600) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder(), 0);

        host.Semantics().Single(node => node.Role == SemanticRole.Table).Extent
            .Should().Be(new TableRange(0, 0, 3, 4));
        host.Semantics().Where(node => node.Place != null).Select(node => (node.Label, node.Place))
            .Should().BeEquivalentTo(new (string, TableRange?)[]
            {
                ("Quarter", new TableRange(0, 0, 1, 2)),
                ("Q1", new TableRange(1, 0, 2, 1)),
                ("14,02", new TableRange(1, 1)),
            }, "the shells set a cell's row and column ranges from it, as AppKit and Android take them");
    }

    [Fact]
    public void AGroup_StaysAGroupOfWhatItsCellsHold()
    {
        var host = Open(TableRole.Group);

        host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.Group && node.Label == "Ledger");
        host.Semantics().Should().NotContain(node => node.Place != null);
        host.Semantics().Should().Contain(node => node.Role == SemanticRole.StaticText && node.Label == "3:2");
    }
}
