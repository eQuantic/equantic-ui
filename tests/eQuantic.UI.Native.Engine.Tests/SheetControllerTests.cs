using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet, minus the pixels — the contract both surfaces will render: Excel's selection
/// and navigation semantics, structural edits that carry their own inverse, and the TSV dialect
/// that makes copy/paste with Excel/Sheets just work. Conducted here first (a path nobody runs is
/// a path that does not work); the same source transpiles and runs on web.
/// </summary>
public class SheetControllerTests
{
    private static SheetController Sheet(int rows = 100, int cols = 26) => new(rows, cols);

    private static SheetController WithBlock(int fromRow, int fromCol, int rows, int cols)
    {
        var sheet = Sheet();
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                sheet.Document.SetCell(new CellRef(fromRow + r, fromCol + c), $"r{fromRow + r}c{fromCol + c}");
        return sheet;
    }

    // ---- addressing ------------------------------------------------------------------------------

    [Fact]
    public void A1_SpeaksSpreadsheet()
    {
        new CellRef(0, 0).ToA1().Should().Be("A1");
        new CellRef(11, 1).ToA1().Should().Be("B12");
        new CellRef(0, 26).ToA1().Should().Be("AA1");
        new CellRef(99, 701).ToA1().Should().Be("ZZ100");

        CellRef.ParseA1("A1").Should().Be(new CellRef(0, 0));
        CellRef.ParseA1("AA10").Should().Be(new CellRef(9, 26));
        CellRef.ParseA1("1A").Should().BeNull();
        CellRef.ParseA1("").Should().BeNull();
    }

    [Fact]
    public void TheKey_PacksAndUnpacks()
    {
        var cell = new CellRef(123, 45);
        CellRef.FromKey(cell.Key).Should().Be(cell);
    }

    // ---- navigation ------------------------------------------------------------------------------

    [Fact]
    public void ArrowMoves_AndShiftExtends()
    {
        var sheet = Sheet();

        sheet.Move(1, 0);
        sheet.ActiveCell.Should().Be(new CellRef(1, 0));

        sheet.Move(0, 2, extend: true);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 0), new CellRef(1, 2)));

        // A plain move collapses the selection — the arrow always lands somewhere single.
        sheet.Move(1, 0);
        sheet.Selection.IsSingleCell.Should().BeTrue();
    }

    [Fact]
    public void CtrlArrow_JumpsLikeExcel()
    {
        // Data in B2:D2; active on B2. Ctrl+Right → end of the block (D2); again → sheet edge.
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(1, 1), "b");
        sheet.Document.SetCell(new CellRef(1, 2), "c");
        sheet.Document.SetCell(new CellRef(1, 3), "d");
        sheet.Selection = new SheetRange(new CellRef(1, 1));

        sheet.Move(0, 1, SheetMotion.DataEdge);
        sheet.ActiveCell.Should().Be(new CellRef(1, 3), "from inside a block it runs to the block's edge");

        sheet.Move(0, 1, SheetMotion.DataEdge);
        sheet.ActiveCell.Should().Be(new CellRef(1, 25), "past the block there is nothing, so it lands on the sheet edge");

        // From an empty cell it jumps ACROSS the gap to the next data.
        sheet.Selection = new SheetRange(new CellRef(1, 0));
        sheet.Document.SetCell(new CellRef(1, 20), "far");
        sheet.Move(0, 1, SheetMotion.DataEdge);
        sheet.ActiveCell.Should().Be(new CellRef(1, 1), "the next populated cell is where the jump lands");
    }

    [Fact]
    public void TabAndEnter_WalkAndWrapTheSelection()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(0, 0), new CellRef(1, 1));   // A1:B2, focus B2

        sheet.Step(0, 1);   // Tab from B2 wraps to A1... focus was B2 → right wraps to left, next row wraps to top
        sheet.ActiveCell.Should().Be(new CellRef(0, 0));
        sheet.Selection.RowCount.Should().Be(2, "the rectangle survives the walk");

        sheet.Step(0, 1);
        sheet.ActiveCell.Should().Be(new CellRef(0, 1));

        // Single cell: Enter just moves down.
        sheet.Selection = new SheetRange(new CellRef(3, 3));
        sheet.Step(1, 0);
        sheet.ActiveCell.Should().Be(new CellRef(4, 3));
    }

    [Fact]
    public void HeaderSelections_TakeWholeBands()
    {
        var sheet = Sheet(rows: 50, cols: 10);

        sheet.SelectRows(2, 4);
        sheet.Selection.RowCount.Should().Be(3);
        sheet.Selection.ColCount.Should().Be(10);

        sheet.SelectCols(1, 1);
        sheet.Selection.ColCount.Should().Be(1);
        sheet.Selection.RowCount.Should().Be(50);
    }

    // ---- cell edits ------------------------------------------------------------------------------

    [Fact]
    public void SetCell_Changes_AndUndoRestores()
    {
        var sheet = Sheet();
        var edits = 0;
        sheet.Changed = _ => edits++;

        sheet.SetCell(new CellRef(2, 2), "hello").Should().BeTrue();
        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("hello");
        sheet.SetCell(new CellRef(2, 2), "hello").Should().BeFalse("writing the same value is not an edit");

        sheet.Undo().Should().BeTrue();
        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("");
        sheet.Redo().Should().BeTrue();
        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("hello");
        edits.Should().Be(3, "set + undo + redo each announced themselves");
    }

    [Fact]
    public void Delete_ClearsTheRectangle_AsOneUndoStep()
    {
        var sheet = WithBlock(0, 0, 3, 3);
        sheet.Selection = new SheetRange(new CellRef(0, 0), new CellRef(2, 2));

        sheet.ClearSelection().Should().BeTrue();
        sheet.Document.Cells.Should().BeEmpty();

        sheet.Undo();
        sheet.Document.Cells.Should().HaveCount(9, "one undo puts the whole rectangle back");
    }

    // ---- structure -------------------------------------------------------------------------------

    [Fact]
    public void InsertRows_ShiftsDataAndSizes()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(5, 0), "below");
        sheet.Resize(SheetAxis.Rows, 5, 44f);

        sheet.Insert(SheetAxis.Rows, 2, count: 2);

        sheet.Document.GetCell(new CellRef(7, 0)).Should().Be("below", "data below the insert moved down");
        sheet.Document.RowHeight(7).Should().Be(44f, "its custom height moved with it");
        sheet.Document.Rows.Should().Be(102);
    }

    [Fact]
    public void DeleteCols_RemovesTheBand_AndUndoResurrectsIt()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(0, 1), "goes");
        sheet.Document.SetCell(new CellRef(0, 3), "stays");

        sheet.Delete(SheetAxis.Cols, 1, count: 2);

        sheet.Document.GetCell(new CellRef(0, 1)).Should().Be("stays", "the survivor slid left over the removed band");
        sheet.Document.Cols.Should().Be(24);

        sheet.Undo();
        sheet.Document.GetCell(new CellRef(0, 1)).Should().Be("goes", "undo resurrects the deleted band's cells");
        sheet.Document.GetCell(new CellRef(0, 3)).Should().Be("stays");
        sheet.Document.Cols.Should().Be(26);
    }

    [Fact]
    public void Resize_IsUndoable()
    {
        var sheet = Sheet();

        sheet.Resize(SheetAxis.Cols, 0, 150f);
        sheet.Document.ColWidth(0).Should().Be(150f);

        sheet.Undo();
        sheet.Document.ColWidth(0).Should().Be(SheetDocument.DefaultColWidth);
    }

    // ---- clipboard -------------------------------------------------------------------------------

    [Fact]
    public void CopyTsv_SpeaksExcel()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(0, 0), "name");
        sheet.Document.SetCell(new CellRef(0, 1), "price");
        sheet.Document.SetCell(new CellRef(1, 0), "has\ttab");
        sheet.Document.SetCell(new CellRef(1, 1), "say \"hi\"");
        sheet.Selection = new SheetRange(new CellRef(0, 0), new CellRef(1, 1));

        sheet.CopyTsv().Should().Be("name\tprice\n\"has\ttab\"\t\"say \"\"hi\"\"\"");
    }

    [Fact]
    public void PasteTsv_LandsAtTheSelection_SelectsTheRectangle_AndUndoesAsOne()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(2, 2), "old");
        sheet.Selection = new SheetRange(new CellRef(2, 2));

        sheet.PasteTsv("a\tb\nc\td").Should().BeTrue();

        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("a");
        sheet.Document.GetCell(new CellRef(3, 3)).Should().Be("d");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(2, 2), new CellRef(3, 3)),
            "the pasted rectangle is what ends up selected — exactly what Sheets does");

        sheet.Undo();
        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("old");
        sheet.Document.GetCell(new CellRef(3, 3)).Should().Be("");
    }

    [Fact]
    public void PasteTsv_RoundTripsWithCopy()
    {
        var source = WithBlock(0, 0, 2, 3);
        source.Selection = new SheetRange(new CellRef(0, 0), new CellRef(1, 2));
        var tsv = source.CopyTsv();

        var target = Sheet();
        target.Selection = new SheetRange(new CellRef(10, 5));
        target.PasteTsv(tsv);

        target.Document.GetCell(new CellRef(10, 5)).Should().Be("r0c0");
        target.Document.GetCell(new CellRef(11, 7)).Should().Be("r1c2");
    }

    [Fact]
    public void PasteTsv_GrowsTheSheetWhenItMust()
    {
        var sheet = Sheet(rows: 3, cols: 2);
        sheet.Selection = new SheetRange(new CellRef(2, 1));

        sheet.PasteTsv("x\ty\nz\tw");

        sheet.Document.Rows.Should().Be(4);
        sheet.Document.Cols.Should().Be(3);
        sheet.Document.GetCell(new CellRef(3, 2)).Should().Be("w");
    }
}
