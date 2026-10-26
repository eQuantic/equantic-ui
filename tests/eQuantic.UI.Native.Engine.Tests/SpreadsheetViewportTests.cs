using eQuantic.UI.Tests;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet on the two-axis viewport, through the host: only the cells in view are built at
/// Excel's extent, the headers stand over their columns at every width and offset, the row numbers
/// widen with their digits, the active cell is kept in view, a held drag scrolls, the colours come
/// from the theme's slot, and a finger taps or pans.
/// </summary>
public class SpreadsheetViewportTests
{
    private static float _clock;

    private static (PhotonHost Host, SheetController Sheet) Open(int rows, int cols, float width = 900,
        float height = 600, IAppTheme? theme = null)
    {
        var sheet = new SheetController(rows, cols);
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            theme ?? PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            SmoothScroll = false,
            ReducedMotion = true,
        };
        _clock = 0;
        Settle(host);
        return (host, sheet);
    }

    /// <summary>Frames until the table's window and the sheet follow the viewport's reports.</summary>
    private static void Settle(PhotonHost host, int frames = 4)
    {
        for (var i = 0; i < frames; i++) host.RenderFrame(new DisplayListBuilder(), _clock += 16);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var below in Walk(child))
                yield return below;
    }

    private static List<(TableViewCell Cell, LayoutNode Node)> Cells(PhotonHost host) =>
        Walk(host.LastFrame!.Root)
            .Where(n => n.Source is TableViewCell { Mark: false })
            .Select(n => ((TableViewCell)n.Source, n))
            .ToList();

    private static TableRegion Region(PhotonHost host) => host.LastFrame!.TableRegions.Single();

    /// <summary>Where the table draws the sheet's cell, one past the headers on each axis.</summary>
    private static Rect BoundsOf(PhotonHost host, CellRef cell) =>
        Cells(host).Single(c => c.Cell.Row == cell.Row + 1 && c.Cell.Column == cell.Col + 1).Node.Bounds;

    /// <summary>The row-number column's width, as the first row number built draws it.</summary>
    private static float NumbersWidth(PhotonHost host) =>
        Cells(host).First(c => c.Cell.Column == 0 && c.Cell.Row > 0).Node.Bounds.Width;

    /// <summary>Whether the cell lies in the part of the window the headers leave uncovered.</summary>
    private static bool InView(PhotonHost host, CellRef cell)
    {
        var region = Region(host);
        var view = region.Viewport.Scroll.Viewport;
        var bounds = BoundsOf(host, cell);
        return bounds.X >= region.Bounds.X + NumbersWidth(host) - 0.5f
            && bounds.Right <= region.Bounds.X + view.Width + 0.5f
            && bounds.Y >= region.Bounds.Y + Spreadsheet.HeaderHeight - 0.5f
            && bounds.Bottom <= region.Bounds.Y + view.Height + 0.5f;
    }

    private static Point CenterOf(PhotonHost host, CellRef cell)
    {
        var bounds = BoundsOf(host, cell);
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    private static void Click(PhotonHost host, CellRef cell)
    {
        var at = CenterOf(host, cell);
        host.PressDown(at.X, at.Y);
        host.PressUp(at.X, at.Y);
        Settle(host, 1);
    }

    // ---- Excel's extent ----------------------------------------------------------------------------

    [Fact]
    public void AGridOfExcelsExtent_BuildsOnlyTheCellsInView_AndReachesItsLastCell()
    {
        var (host, sheet) = Open(1_048_576, 16_384, 1440, 900);

        Cells(host).Count.Should().BeLessThan(1_500, "a screenful and its margin, never the sheet");

        Click(host, new CellRef(0, 0));
        host.KeyDown("ArrowDown", KeyModifiers.Command);
        host.KeyDown("ArrowRight", KeyModifiers.Command);
        Settle(host);

        sheet.ActiveCell.Should().Be(new CellRef(1_048_575, 16_383));
        InView(host, sheet.ActiveCell).Should().BeTrue("the last cell is revealed under its headers");
        var labels = host.Semantics().Select(s => s.Label).ToList();
        labels.Should().Contain("XFD", "its column's header is drawn");
        labels.Should().Contain("1048576", "and its row's number");
        Cells(host).Count.Should().BeLessThan(1_500);
    }

    [Fact]
    public void TheCellsInView_OfAHundredThousandByAHundred_AreBuiltAndNothingElse()
    {
        var (host, _) = Open(100_000, 100, 1440, 900);

        var built = Cells(host).Where(c => c.Cell.Row > 0 && c.Cell.Column > 0).ToList();
        built.Should().NotBeEmpty();
        built.Max(c => c.Cell.Row).Should().BeLessThan(40, "the rows in view and the overscan");
        built.Max(c => c.Cell.Column).Should().BeLessThan(20, "the columns in view and the overscan");
    }

    // ---- the headers stand over their columns ---------------------------------------------------

    [Theory]
    [InlineData(400)]
    [InlineData(900)]
    [InlineData(1440)]
    public void EveryHeader_StandsOverItsColumn_AtEveryWidthAndOffset(float width)
    {
        var (host, _) = Open(200, 26, width, 600);
        var region = Region(host);
        host.ScrollBy(region.Bounds.X + 200, region.Bounds.Y + 200, 300, 500, stepped: false);
        Settle(host);
        Region(host).Viewport.Scroll.Offset.Should().Be(new TableOffset(300, 500));

        var cells = Cells(host);
        var headers = cells.Where(c => c.Cell.Row == 0 && c.Cell.Column > 0).ToList();
        headers.Should().NotBeEmpty();
        foreach (var header in headers)
        {
            var below = cells.Where(c => c.Cell.Column == header.Cell.Column && c.Cell.Row > 0).ToList();
            below.Should().NotBeEmpty();
            foreach (var cell in below)
            {
                cell.Node.Bounds.X.Should().BeApproximately(header.Node.Bounds.X, 0.5f,
                    $"column {header.Cell.Column} at {width} points wide");
                cell.Node.Bounds.Width.Should().BeApproximately(header.Node.Bounds.Width, 0.5f);
            }
        }
        foreach (var number in cells.Where(c => c.Cell.Column == 0 && c.Cell.Row > 0))
            foreach (var cell in cells.Where(c => c.Cell.Row == number.Cell.Row && c.Cell.Column > 0))
                cell.Node.Bounds.Y.Should().BeApproximately(number.Node.Bounds.Y, 0.5f, $"row {number.Cell.Row}");
    }

    [Fact]
    public void TheRowNumbers_WidenPastRow9999_AndTheCellsStartAtTheirEdge()
    {
        var (host, _) = Open(100_000, 8);
        var before = NumbersWidth(host);

        var region = Region(host);
        host.ScrollBy(region.Bounds.X + 200, region.Bounds.Y + 200, 0, 10_000 * 28, stepped: false);
        Settle(host, 6);

        var after = NumbersWidth(host);
        after.Should().BeGreaterThan(before, "row 10 000 needs a digit row 10 does not");
        var number = Cells(host).First(c => c.Cell.Column == 0 && c.Cell.Row > 0);
        var first = Cells(host).Where(c => c.Cell.Row == number.Cell.Row && c.Cell.Column > 0)
            .OrderBy(c => c.Node.Bounds.X).First();
        first.Node.Bounds.X.Should().BeApproximately(number.Node.Bounds.Right, 0.5f, "the cells start at its edge");
    }

    [Fact]
    public void RowsAndColumnsDeleted_LeaveNoCellOfThemBuilt()
    {
        // The document's axes shrink in place: the table is handed the same spans, with fewer rows.
        var (host, sheet) = Open(30, 12, 1440, 1000);
        Cells(host).Max(c => c.Cell.Row).Should().Be(30, "the whole sheet fits the window");

        sheet.Delete(SheetAxis.Rows, 20, 10);
        sheet.Delete(SheetAxis.Cols, 8, 4);
        host.Invalidate();
        Settle(host);

        Cells(host).Should().OnlyContain(c => c.Cell.Row <= 20 && c.Cell.Column <= 8,
            "twenty rows and eight columns are left, behind the headers");
    }

    // ---- the active cell kept in view -----------------------------------------------------------------

    [Fact]
    public void FortyRowsDown_TheActiveCellLiesInsideTheWindow()
    {
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(0, 0));

        for (var i = 0; i < 40; i++)
        {
            host.KeyDown("ArrowDown");
            Settle(host, 1);
        }
        Settle(host);

        sheet.ActiveCell.Should().Be(new CellRef(40, 0), "A41");
        InView(host, sheet.ActiveCell).Should().BeTrue();
    }

    [Fact]
    public void APageDown_MovesByTheRowsShown_AndKeepsTheActiveCellInView()
    {
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(0, 0));
        var shown = (int)((Region(host).Viewport.Scroll.Viewport.Height - Spreadsheet.HeaderHeight) / 28);

        host.KeyDown("PageDown");
        Settle(host);

        sheet.ActiveCell.Should().Be(new CellRef(shown, 0));
        InView(host, sheet.ActiveCell).Should().BeTrue();
    }

    [Fact]
    public void AnUndoOutOfView_BringsTheCellBackIntoTheWindow()
    {
        var (host, sheet) = Open(400, 8);
        Click(host, new CellRef(1, 1));
        host.TextInput("x");
        host.KeyDown("Enter");
        Settle(host);
        var region = Region(host);
        host.ScrollBy(region.Bounds.X + 200, region.Bounds.Y + 200, 0, 3_000, stepped: false);
        Settle(host);
        Cells(host).Should().NotContain(c => c.Cell.Row == 2 && c.Cell.Column == 2, "B2 left the window");

        host.KeyDown("z", KeyModifiers.Command);
        Settle(host);

        sheet.Document.GetCell(new CellRef(1, 1)).Should().BeEmpty();
        sheet.ActiveCell.Should().Be(new CellRef(1, 1));
        InView(host, sheet.ActiveCell).Should().BeTrue();
    }

    [Fact]
    public void ADragInsideTheGrid_AfterAKey_ScrollsNothing()
    {
        // The key's reveal is held until the person moves the grid. A drag that then reaches the row
        // half under the bottom edge moves the active cell there, and a request built from the active
        // cell as it stands revealed it: the grid scrolled under a pointer still inside the cells.
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(0, 0));
        host.KeyDown("ArrowDown");
        Settle(host);
        var region = Region(host);
        var start = CenterOf(host, new CellRef(1, 1));
        var edge = region.Bounds.Y + region.Viewport.Scroll.Viewport.Height - 6;

        host.PressDown(start.X, start.Y);
        host.PointerMove(start.X, edge);
        Settle(host);

        sheet.Selection.Focus.Row.Should().BeGreaterThan(15, "the drag reached the row at the bottom edge");
        Region(host).Viewport.Scroll.Offset.Down.Should().Be(0, "a drag inside the cells moves no grid");
        host.PressUp(start.X, edge);
    }

    [Fact]
    public void ADragHeldBelowTheGrid_ScrollsAndKeepsExtending_UntilTheRelease()
    {
        var (host, sheet) = Open(400, 8);
        var start = CenterOf(host, new CellRef(1, 1));
        var region = Region(host);
        var below = region.Bounds.Y + region.Viewport.Scroll.Viewport.Height + 60;

        host.PressDown(start.X, start.Y);
        host.PointerMove(start.X, below);
        Settle(host, 63);   // a second of frames

        Region(host).Viewport.Scroll.Offset.Down.Should().BeGreaterThan(0, "the grid scrolled down");
        sheet.Selection.Anchor.Should().Be(new CellRef(1, 1));
        sheet.Selection.Focus.Row.Should().BeGreaterThan(25, "the selection reached rows past the ones first shown");

        host.PressUp(start.X, below);
        var stopped = Region(host).Viewport.Scroll.Offset;
        Settle(host, 10);
        Region(host).Viewport.Scroll.Offset.Should().Be(stopped, "the release stops the scroll");
    }

    // ---- the marks -----------------------------------------------------------------------------------

    [Fact]
    public void ASelectionChange_LeavesEveryCellAsItWas_AndMovesOnlyTheMarks()
    {
        var (host, sheet) = Open(50, 8);
        sheet.Document.SetCell(new CellRef(2, 2), "42");
        host.Invalidate();
        Settle(host);
        Dictionary<(int, int), BoxStyle> Looks() => Cells(host)
            .Where(c => c.Cell.Row > 0 && c.Cell.Column > 0)
            .ToDictionary(c => (c.Cell.Row, c.Cell.Column), c => Walk(c.Node).Select(n => n.Source).OfType<Box>().First().Style);
        Rect Ring() => Walk(host.LastFrame!.Root).Where(n => n.Source is TableViewCell { Mark: true })
            .Select(n => n.Bounds).Last(b => b.Width > 10);
        var before = Looks();
        var ringBefore = Ring();

        sheet.Selection = new SheetRange(new CellRef(1, 1), new CellRef(4, 3));
        host.Invalidate();
        Settle(host);

        Looks().Should().Equal(before, "a cell is drawn from its value alone");
        Ring().Should().NotBe(ringBefore, "the marks moved with the selection");
    }

    // ---- the theme's slot -------------------------------------------------------------------------------

    private static Box CellBox(PhotonHost host, int tableRow, int tableColumn) =>
        Walk(Cells(host).First(c => c.Cell.Row == tableRow && c.Cell.Column == tableColumn).Node).Select(n => n.Source).OfType<Box>().First();

    [Fact]
    public void ATheme_ThatNamesAGridline_DrawsTheGridlinesInIt()
    {
        var gridline = new ColorToken(Color.FromRgb(0x12, 0x34, 0x56), Color.FromRgb(0x65, 0x43, 0x21));
        var (host, _) = Open(50, 8, theme: new SheetSlotTheme(PhotonTheme.Instance, new SheetTheme { Gridline = gridline }));

        CellBox(host, 2, 2).Style.BorderColor.Should().Be(gridline);
        CellBox(host, 2, 2).Style.Background.Should().Be(PhotonTheme.Instance.Surface, "what the slot does not name is the default");
    }

    [Fact]
    public void ATheme_ThatNamesTheRangeOutline_OutlinesEveryRangeInIt()
    {
        var outline = new ColorToken(Color.FromRgb(0x12, 0x80, 0x34), Color.FromRgb(0x34, 0x80, 0x12));
        var (host, sheet) = Open(50, 8, theme: new SheetSlotTheme(PhotonTheme.Instance, new SheetTheme { RangeOutline = outline }));
        // The keyboard on the sheet, B2 to C3 selected, then D5:D6 added: two ranges, each outlined.
        var origin = (X: sheet.Grid.HeaderWidth, Y: sheet.Grid.HeaderHeight);
        host.PressDown(origin.X + 96 + 48, origin.Y + 28 + 14);
        host.PressUp(origin.X + 96 + 48, origin.Y + 28 + 14);
        host.PressDown(origin.X + 2 * 96 + 48, origin.Y + 2 * 28 + 14, modifiers: KeyModifiers.Shift);
        host.PressUp(origin.X + 2 * 96 + 48, origin.Y + 2 * 28 + 14);
        host.PressDown(origin.X + 3 * 96 + 48, origin.Y + 4 * 28 + 14, modifiers: KeyModifiers.Command);
        host.PointerMove(origin.X + 3 * 96 + 48, origin.Y + 5 * 28 + 14);
        host.PressUp(origin.X + 3 * 96 + 48, origin.Y + 5 * 28 + 14);
        Settle(host);

        sheet.Ranges.Should().HaveCount(2);
        Walk(host.LastFrame!.Root).Select(n => n.Source).OfType<Box>()
            .Count(box => box.Style.BorderWidth == 1.5f && box.Style.BorderColor == outline)
            .Should().Be(2, "every range is outlined in the colour the theme names");
    }

    [Fact]
    public void ATheme_ThatNamesNothingForSheets_DrawsInItsFoundationColours()
    {
        var (host, _) = Open(50, 8);

        CellBox(host, 2, 2).Style.BorderColor.Should().Be(PhotonTheme.Instance.Border);
        var header = Cells(host).First(c => c.Cell.Row == 0 && c.Cell.Column == 3).Node;
        Walk(header).Select(n => n.Source).OfType<Box>().First().Style.Background
            .Should().Be(PhotonTheme.Instance.SurfaceSubtle, "the headers in its subtle surface");
    }

    // ---- a finger --------------------------------------------------------------------------------------

    [Fact]
    public void AFingersTap_SelectsTheCell_WhenItLifts()
    {
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(1, 1));
        var at = CenterOf(host, new CellRef(4, 3));

        host.PressDown(at.X, at.Y, kind: PointerKind.Touch);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)), "nothing until it lifts");
        host.PressUp(at.X, at.Y, PointerKind.Touch);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(4, 3)), "D5");
    }

    [Fact]
    public void AStylusDrag_SelectsAsAMouseDoes_AndScrollsNothing()
    {
        // A pen on the cells is the sheet's, as on the web, where the surface captures it: its press
        // selects at once and its drag stretches the selection, and the table's candidate pan lets go.
        var (host, sheet) = Open(200, 8);
        var from = CenterOf(host, new CellRef(1, 1));
        var to = CenterOf(host, new CellRef(4, 3));

        host.PressDown(from.X, from.Y, kind: PointerKind.Stylus);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)));
        for (var step = 1; step <= 10; step++)
        {
            host.PointerMove(from.X + (to.X - from.X) * step / 10, from.Y + (to.Y - from.Y) * step / 10, PointerKind.Stylus);
            Settle(host, 1);
        }
        host.PressUp(to.X, to.Y, PointerKind.Stylus);
        Settle(host);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(4, 3)), "B2:D5");
        Region(host).Viewport.Scroll.Offset.Should().Be(TableOffset.Zero, "the pen dragged no grid");
    }

    [Fact]
    public void AFingersPan_AfterATap_TellsTheEngineThePressWasTakenAway()
    {
        // The protocol the engine is promised: a pan the table takes is a Cancel, never an Up.
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(1, 1));
        var at = CenterOf(host, new CellRef(8, 3));

        host.PressDown(at.X, at.Y, kind: PointerKind.Touch);
        for (var step = 1; step <= 10; step++)
        {
            host.PointerMove(at.X, at.Y - 20 * step, PointerKind.Touch);
            Settle(host, 1);
        }
        host.PressUp(at.X, at.Y - 200, PointerKind.Touch);
        Settle(host);
        sheet.Follow().Should().BeFalse();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)));
    }

    [Fact]
    public void AFingersPan_ScrollsTheGrid_AndSelectsNothing()
    {
        var (host, sheet) = Open(200, 8);
        Click(host, new CellRef(1, 1));
        var at = CenterOf(host, new CellRef(4, 3));

        host.PressDown(at.X, at.Y, kind: PointerKind.Touch);
        for (var step = 1; step <= 10; step++)
        {
            host.PointerMove(at.X, at.Y - 20 * step, PointerKind.Touch);
            Settle(host, 1);
        }
        host.PressUp(at.X, at.Y - 200, PointerKind.Touch);
        Settle(host);

        Region(host).Viewport.Scroll.Offset.Down.Should().BeGreaterThan(0, "the grid scrolled down");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)), "the selection is still B2");
    }
}
