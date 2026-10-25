using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;
using eQuantic.UI.Spreadsheets;

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
        // Headers are drawn text, and the sheet's engine reads a press on them (#984); slice 2b (#1081) gives
        // them the grid's own roles.
        var headers = host.Semantics().Where(s => s.Role == SemanticRole.StaticText).ToList();
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
    public void ACellOfTwoLines_ShowsThemFromItsTop()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(new CellRef(1, 1), "one\ntwo");
        sheet.Document.SetRowHeight(1, 48);
        host.Invalidate();
        var frame = host.RenderFrame(new DisplayListBuilder(), 32);

        static IEnumerable<Framework.LayoutNode> All(Framework.LayoutNode node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var below in All(child)) yield return below;
        }
        var one = All(frame.Root).Single(node => node.Source is Text { Content: "one" }).Bounds;
        var two = All(frame.Root).Single(node => node.Source is Text { Content: "two" }).Bounds;

        two.X.Should().Be(one.X, "each line starts at the cell's padding");
        two.Y.Should().BeGreaterThanOrEqualTo(one.Bottom, "the second line stands under the first");
    }

    [Fact]
    public void ColumnNames_SpellLikeExcel()
    {
        CellRef.ColumnName(0).Should().Be("A");
        CellRef.ColumnName(25).Should().Be("Z");
        CellRef.ColumnName(26).Should().Be("AA");
        CellRef.ColumnName(701).Should().Be("ZZ");
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
        host.ScrollBy(300, 200, 0, 10 * 28f, stepped: false);
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
        sheet.Ranges.Should().Equal([new SheetRange(new CellRef(0, 0))], "the grip resizes, and selects nothing");
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
    public void AHeaderPress_CarriesItsModifiers_ThroughTheHost()
    {
        var (host, sheet) = Open();
        var columnC = (X: Spreadsheet.HeaderWidth + 2 * 96f + 48f, Y: Spreadsheet.HeaderHeight / 2);
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f);   // B3
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f);

        // ⌘ (Ctrl elsewhere) on column C's header adds the column as a range of its own.
        host.PressDown(columnC.X, columnC.Y, modifiers: KeyModifiers.Command);
        host.PressUp(columnC.X, columnC.Y);
        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(2, 1)),
            new SheetRange(new CellRef(0, 2), new CellRef(sheet.Document.Rows - 1, 2)));

        // Shift on row 6's number, from a fresh B3: the rows 3 to 6, whole, with B3 still active.
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 2 * 28f + 14f);
        host.PressDown(20f, Spreadsheet.HeaderHeight + 5 * 28f + 14f, modifiers: KeyModifiers.Shift);
        host.PressUp(20f, Spreadsheet.HeaderHeight + 5 * 28f + 14f);
        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(2, 0), new CellRef(5, sheet.Document.Cols - 1)));
        sheet.ActiveCell.Should().Be(new CellRef(2, 1));
    }

    [Fact]
    public void ADragAcrossColumnHeaders_StretchesTheBand_ThroughTheHost()
    {
        var (host, sheet) = Open();
        var y = Spreadsheet.HeaderHeight / 2;

        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, y);               // B's header
        host.PointerMove(Spreadsheet.HeaderWidth + 2 * 96f + 48f, y);
        host.PointerMove(Spreadsheet.HeaderWidth + 3 * 96f + 48f, y);         // D's header
        host.PressUp(Spreadsheet.HeaderWidth + 3 * 96f + 48f, y);

        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(0, 1), new CellRef(sheet.Document.Rows - 1, 3)));
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void ExcelsChords_ReachTheSheet_ThroughTheHost(KeyboardConvention convention)
    {
        var sheet = new SheetController(1000, 26);
        foreach (var (row, col) in new[] { (1, 1), (1, 2), (2, 1), (2, 2), (6, 4) })
            sheet.Document.SetCell(new CellRef(row, col), "x");
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
            KeyboardConvention = convention,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        var control = convention == KeyboardConvention.Apple ? KeyModifiers.Control : KeyModifiers.Command;
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);   // B2
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);

        host.KeyDown(" ", KeyModifiers.Shift).Should().BeTrue();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 0), new CellRef(1, 25)));

        // From B2 again: after Shift+Space the active range is the whole row, whose columns are all of them.
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);
        host.KeyDown(" ", control).Should().BeTrue();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 1), new CellRef(999, 1)));

        host.KeyDown("ArrowDown");
        host.KeyDown("a", KeyModifiers.Command);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(2, 2)), "the current region");

        host.KeyDown("End", KeyModifiers.Command);
        sheet.ActiveCell.Should().Be(new CellRef(6, 4), "the last cell in use");

        host.KeyDown("y", KeyModifiers.Command).Should().BeTrue("Ctrl+Y is the sheet's, with nothing to redo too");
        host.KeyDown(".", control).Should().BeTrue();
    }

    /// <summary>Where the active cell's ring is drawn across on the frame at <paramref name="timeMs"/>: the
    /// two-point stroke in the theme's focus ring.</summary>
    private static float RingX(PhotonHost host, float timeMs)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        var ring = ((IAppTheme)PhotonTheme.Instance).FocusRing.Resolve(ThemeMode.Light);
        var stroke = builder.Build().Commands.ToArray()
            .Single(command => command.Paint.Color == ring && command.StrokeWidth == 2f);
        return stroke.Transform.Transform(new Point(stroke.Shape.Rect.X, stroke.Shape.Rect.Y)).X;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheCellCursor_GlidesToItsNextCell_AndJumpsUnderReducedMotion(bool reducedMotion)
    {
        var sheet = new SheetController(100, 8);
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
            ReducedMotion = reducedMotion,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);   // B2
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);
        // The press moved the ring from A1 too; it has stood at B2 once that glide is over.
        RingX(host, 100);
        var b2 = RingX(host, 600);
        RingX(host, 700).Should().Be(b2);

        host.KeyDown("ArrowRight");
        var c2 = b2 + 96f;

        if (reducedMotion)
        {
            RingX(host, 800).Should().BeApproximately(c2, 0.5f, "under reduced motion the ring jumps");
            return;
        }
        RingX(host, 800).Should().BeApproximately(b2, 0.5f, "the glide starts where the ring stood");
        RingX(host, 900).Should().BeInRange(b2 + 1, c2 - 1, "halfway through the base motion it stands between them");
        RingX(host, 1050).Should().BeApproximately(c2, 0.5f);
    }

    [Fact]
    public void TheCellCursor_GlidesToo_WhenARangeIsAddedBeforeIt()
    {
        var sheet = new SheetController(100, 8);
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);   // B2
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);
        RingX(host, 100);
        var b2 = RingX(host, 600);

        // ⌘ adds D2 as a range of its own: the marks before the ring now include B2's, and the ring is the
        // same mark by its key, so it glides rather than appearing at D2.
        host.PressDown(Spreadsheet.HeaderWidth + 3 * 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f,
            modifiers: KeyModifiers.Command);
        host.PressUp(Spreadsheet.HeaderWidth + 3 * 96f + 48f, Spreadsheet.HeaderHeight + 28f + 14f);

        RingX(host, 700).Should().BeApproximately(b2, 0.5f);
        RingX(host, 950).Should().BeApproximately(b2 + 2 * 96f, 0.5f);
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

    /// <summary>
    /// The sheet is ONE stop of the Tab order, as Excel's grid is: the table inside declines the focus
    /// and so do the headers. Tab arrives on it, as on every surface that owns the keyboard; once a
    /// press has entered it, Tab is the sheet's own key, walking the active cell to the right.
    /// </summary>
    [Fact]
    public void TheSheet_IsTheOneStopOfTheTabOrder_AndTabWalksItsActiveCell()
    {
        var (host, sheet) = Open();
        var frame = host.RenderFrame(new DisplayListBuilder(), 32);
        frame.FocusStops.Should().ContainSingle("the headers and the table decline the focus; the surface is the stop");

        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.FocusedId.Should().Be(frame.FocusStops[0].Id);

        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.KeyDown("Tab");
        host.KeyDown("Tab");
        sheet.ActiveCell.Should().Be(new CellRef(0, 2), "Tab inside the sheet steps the active cell");
        host.FocusedId.Should().Be(frame.FocusStops[0].Id, "and the keyboard stays on the sheet");
    }

    [Fact]
    public void AHeaderPress_LeavesTheKeyboardOnTheSheet()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);   // A1: the sheet takes the keyboard
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        host.PressDown(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight / 2);   // column B's header
        host.PressUp(Spreadsheet.HeaderWidth + 96f + 48f, Spreadsheet.HeaderHeight / 2);
        sheet.Selection.LeftCol.Should().Be(1);

        host.KeyDown("ArrowRight", KeyModifiers.None);
        sheet.ActiveCell.Should().Be(new CellRef(0, 2), "the arrows go on moving the active cell");
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
