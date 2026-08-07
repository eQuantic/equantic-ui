using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The write-once Spreadsheet component: headers, the virtualized row window, and — the part the
/// design hinges on — the surface living INSIDE the scroll, so a click on a scrolled grid selects
/// the cell actually under the pointer and the marks stay aligned at any offset.
/// </summary>
public class SpreadsheetComponentTests
{
    private static (PhotonHost Host, SheetController Sheet) Open(int rows = 1000)
    {
        var sheet = new SheetController(rows, cols: 6);
        for (var r = 0; r < rows; r++)
            sheet.Document.SetCell(new CellRef(r, 0), $"row {r}");
        var host = new PhotonHost(
            new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);   // the viewport reported; window corrected
        return (host, sheet);
    }

    [Fact]
    public void TheWindow_MaterializesAScreenful_NotTheSheet()
    {
        // The grid's cells live inside the surface (one announced region for now), so the window
        // is proven two ways: the row headers OUTSIDE it, and the ListView ruler — a sheet twenty
        // times longer must emit exactly the same draw commands.
        var (host, _) = Open();
        // Headers are BUTTONS now — clicking one selects its band — so that is their role.
        var headers = host.Semantics().Where(s => s.Role == SemanticRole.Button).ToList();
        headers.Should().Contain(s => s.Label == "A", "column headers spell A..");
        headers.Should().Contain(s => s.Label == "1", "row headers count from 1");
        headers.Count(s => int.TryParse(s.Label, out _)).Should().BeLessThan(40,
            "row headers exist for the WINDOW, not the thousand rows");

        var big = new DisplayListBuilder();
        Open(rows: 1000).Host.RenderFrame(big, 32);
        var small = new DisplayListBuilder();
        Open(rows: 50).Host.RenderFrame(small, 32);
        big.Build().Count.Should().Be(small.Build().Count,
            "rows outside the window are a spacer, never cells");
    }

    [Fact]
    public void ColumnNames_SpellLikeExcel()
    {
        Spreadsheet.ColumnName(0).Should().Be("A");
        Spreadsheet.ColumnName(25).Should().Be("Z");
        Spreadsheet.ColumnName(26).Should().Be("AA");
        Spreadsheet.ColumnName(701).Should().Be("ZZ");
    }

    [Fact]
    public void AClickOnTheGrid_SelectsTheCellUnderIt()
    {
        var (host, sheet) = Open();

        // First grid cell: past the row-header column (44) and the fixed header row (26).
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        sheet.ActiveCell.Should().Be(new CellRef(0, 0));
    }

    [Fact]
    public void AfterScrolling_AClickStillSelectsTheCellUnderThePointer()
    {
        var (host, sheet) = Open();

        // Scroll 10 rows down (280dp), settle the window, click the first visible cell.
        host.ScrollBy(300, 200, 10 * 28f);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        sheet.ActiveCell.Row.Should().Be(10,
            "the surface scrolled WITH the content, so the arithmetic and the pixels agree");
        sheet.ActiveCell.Col.Should().Be(0);
    }

    [Fact]
    public void DraggingAColumnSeparator_ResizesLive_AndLandsAsOneUndo()
    {
        var (host, sheet) = Open();
        var edge = Spreadsheet.HeaderWidth + sheet.Document.ColWidth(0);   // the A|B separator
        var y = Spreadsheet.HeaderHeight / 2;

        host.PressDown(edge - 2f, y);
        host.PointerMove(edge - 2f + 20f, y);
        host.RenderFrame(new DisplayListBuilder(), 100);
        sheet.Document.ColWidth(0).Should().Be(116f, "the drag previews live, every move");

        host.PointerMove(edge - 2f + 40f, y);
        host.PressUp(edge - 2f + 40f, y);

        sheet.Document.ColWidth(0).Should().Be(136f);
        sheet.Undo();
        sheet.Document.ColWidth(0).Should().Be(96f,
            "however many moves previewed, the gesture lands as ONE inverse");
        sheet.Redo();
        sheet.Document.ColWidth(0).Should().Be(136f);
    }

    [Fact]
    public void DraggingARowSeparator_InsideTheScroll_ResizesInsteadOfScrolling()
    {
        var (host, sheet) = Open();
        // Row 1's grip: riding the bottom edge of the first row header, inside the ScrollView.
        var gripY = Spreadsheet.HeaderHeight + sheet.Document.RowHeight(0) - 2f;

        host.PressDown(20f, gripY);
        host.PointerMove(20f, gripY + 16f);   // past the 12dp arming slop
        host.PressUp(20f, gripY + 16f);

        sheet.Document.RowHeight(0).Should().Be(44f,
            "a vertical drag on the grip is a resize, not a scroll");
        sheet.Undo();
        sheet.Document.RowHeight(0).Should().Be(28f);
    }

    [Fact]
    public void TheResizeFloor_HoldsTheColumnGrabbable()
    {
        var (host, sheet) = Open();
        var edge = Spreadsheet.HeaderWidth + sheet.Document.ColWidth(0);
        var y = Spreadsheet.HeaderHeight / 2;

        host.PressDown(edge - 2f, y);
        host.PointerMove(edge - 2f - 300f, y);   // far past zero
        host.PressUp(edge - 2f - 300f, y);

        sheet.Document.ColWidth(0).Should().Be(Spreadsheet.MinColWidth,
            "Excel lets a column vanish; we keep a grabbable sliver");
    }

    [Fact]
    public void ClickingAColumnHeader_SelectsTheWholeColumn()
    {
        var (host, sheet) = Open();

        // Column B's header: past the corner (44) and column A (96), centred.
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight / 2);
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight / 2);

        sheet.Selection.LeftCol.Should().Be(1);
        sheet.Selection.RightCol.Should().Be(1);
        sheet.Selection.TopRow.Should().Be(0);
        sheet.Selection.BottomRow.Should().Be(sheet.Document.Rows - 1, "the band runs the sheet");
    }

    [Fact]
    public void ClickingARowHeader_SelectsTheWholeRow_AndTheCornerSelectsAll()
    {
        var (host, sheet) = Open();

        host.PressDown(20f, Spreadsheet.HeaderHeight + 28f + 10f);   // row 2's header
        host.PressUp(20f, Spreadsheet.HeaderHeight + 28f + 10f);
        sheet.Selection.TopRow.Should().Be(1);
        sheet.Selection.BottomRow.Should().Be(1);
        sheet.Selection.RightCol.Should().Be(sheet.Document.Cols - 1);

        host.PressDown(20f, Spreadsheet.HeaderHeight / 2);           // the corner
        host.PressUp(20f, Spreadsheet.HeaderHeight / 2);
        sheet.Selection.BottomRow.Should().Be(sheet.Document.Rows - 1);
        sheet.Selection.RightCol.Should().Be(sheet.Document.Cols - 1);
    }

    [Fact]
    public void ShiftClick_StretchesTheSelection_FromItsAnchor()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);   // A1
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        // Shift+click C3: the anchor stands, the rectangle stretches.
        host.PressDown(Spreadsheet.HeaderWidth + 2 * 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f,
            modifiers: KeyModifiers.Shift);
        host.PressUp(Spreadsheet.HeaderWidth + 2 * 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 0), new CellRef(2, 2)));
        sheet.ActiveCell.Should().Be(new CellRef(0, 0));
    }

    [Fact]
    public void DraggingTheFillHandle_PoursTheValue_AsOneUndo()
    {
        var (host, sheet) = Open();
        // Select A1 (seeded "row 0"); the handle sits on its bottom-right corner.
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        var cornerX = Spreadsheet.HeaderWidth + 96f;
        var cornerY = Spreadsheet.HeaderHeight + 28f;

        host.PressDown(cornerX - 2f, cornerY - 2f);
        host.PointerMove(cornerX - 2f, cornerY - 2f + 2 * 28f);      // two rows down
        sheet.FillTarget.Should().NotBeNull("the drag previews live");
        host.PressUp(cornerX - 2f, cornerY - 2f + 2 * 28f);

        sheet.Document.GetCell(new CellRef(1, 0)).Should().Be("row 0");
        sheet.Document.GetCell(new CellRef(2, 0)).Should().Be("row 0");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 0), new CellRef(2, 0)));

        sheet.Undo();
        sheet.Document.GetCell(new CellRef(1, 0)).Should().Be("row 1", "one drag, one inverse");
    }

    [Fact]
    public void ThePointer_AnnouncesTheGrips_AndTheFillHandle()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);   // select A1
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.RenderFrame(new DisplayListBuilder(), 100);   // the handle paints on the new frame

        host.CursorAt(Spreadsheet.HeaderWidth + 96f - 2f, Spreadsheet.HeaderHeight / 2)
            .Should().Be(CursorShape.ColResize, "a column grip says col-resize");
        host.CursorAt(20f, Spreadsheet.HeaderHeight + 28f - 2f)
            .Should().Be(CursorShape.RowResize, "a row grip says row-resize");
        host.CursorAt(Spreadsheet.HeaderWidth + 96f - 2f, Spreadsheet.HeaderHeight + 28f - 2f)
            .Should().Be(CursorShape.Crosshair, "the fill handle says crosshair");
    }

    [Fact]
    public void TheKeyboard_ReachesTheSheet_ThroughTheComponent()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        host.KeyDown("ArrowDown", KeyModifiers.None);
        host.KeyDown("ArrowRight", KeyModifiers.None);

        sheet.ActiveCell.Should().Be(new CellRef(1, 1));
    }
}
