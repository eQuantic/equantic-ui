using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The table viewport on Photon, driven through the host as a shell drives it: the window of cells a
/// table builds, the pinned bands against the cells that scroll, the unbounded axis, the wheel on two
/// axes, a finger, the scrollbars, the requests and the reports. Each scenario is one of the
/// <c>table-viewport</c> spec's.
/// </summary>
public class TableViewportTests
{
    private const int ExcelRows = 1_048_576;
    private const int ExcelColumns = 16_384;

    private sealed class Tally
    {
        public int Built;
        public int Pressed;
        public int Scrolled;
        public readonly List<string> Logged = new();
    }

    private static TableView Table(Tally tally, int rows = ExcelRows, int columns = ExcelColumns,
        bool pressable = false, TableScrollRequest? request = null, IReadOnlyList<TableViewCell>? marks = null,
        SizeValue height = default, bool canRequestFocus = true) =>
        new(new UniformTableSpans(rows, 28), new UniformTableSpans(columns, 96), (row, column) =>
        {
            tally.Built++;
            VisualNode text = new Text($"{row}:{column}", TypeRole.BodyM);
            return pressable ? new Pressable(text, () => tally.Pressed++) : text;
        })
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Request = request,
            Marks = marks,
            Height = height,
            CanRequestFocus = canRequestFocus,
            OnScrolled = _ => tally.Scrolled++,
        };

    private static PhotonHost Host(VisualNode root, float width, float height, Tally tally)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            SmoothScroll = false,
            ReducedMotion = true,
            Log = tally.Logged.Add,
        };
        Settle(host, 0);
        return host;
    }

    private static float _clock;

    /// <summary>Frames until the table's window follows the viewport's reports.</summary>
    private static void Settle(PhotonHost host, float from = -1)
    {
        if (from >= 0) _clock = from;
        for (var i = 0; i < 4; i++) host.RenderFrame(new DisplayListBuilder(), _clock += 16);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var below in Walk(child))
                yield return below;
    }

    private static List<(TableViewCell Cell, Rect Bounds)> Cells(PhotonHost host, bool marks = false) =>
        Walk(host.LastFrame!.Root)
            .Where(n => n.Source is TableViewCell cell && cell.Mark == marks)
            .Select(n => ((TableViewCell)n.Source, n.Bounds))
            .ToList();

    private static TableRegion Region(PhotonHost host) => host.LastFrame!.TableRegions.Single();

    // ---- the window --------------------------------------------------------------------------

    [Fact]
    public void AGridOfExcelsSize_BuildsOnlyTheCellsInView()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);

        // 888 × 588 once the two scrollbars take their strips: rows 1 to 20 and columns 1 to 9 show,
        // three more past each edge are built, and the pinned row and column go with them.
        var cells = Cells(host);
        cells.Should().HaveCount(1 + 12 + 23 + 23 * 12);
        cells.Should().HaveCountLessThan(1000);
        cells.Max(c => c.Cell.Row).Should().Be(23);
        cells.Max(c => c.Cell.Column).Should().Be(12);
    }

    [Fact]
    public void AScrollInsideTheOverscan_BuildsNothing()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        tally.Built = 0;

        host.ScrollBy(450, 300, 0, 28, stepped: false).Should().BeTrue();
        Settle(host);

        tally.Built.Should().Be(0);
        Region(host).Viewport.Scroll.Offset.Should().Be(new TableOffset(0, 28));
    }

    // ---- the pinned bands -----------------------------------------------------------------------

    [Theory]
    [InlineData(400)]
    [InlineData(900)]
    [InlineData(1440)]
    public void PinnedCells_StayAlignedWithTheirColumnsAndRows_AtEveryWidth(float width)
    {
        var tally = new Tally();
        var host = Host(Table(tally, request: new TableScrollRequest(1) { Offset = new TableOffset(5000, 120000) }),
            width, 600, tally);

        var cells = Cells(host);
        var headers = cells.Where(c => c.Cell.Row == 0 && c.Cell.Column > 0).ToDictionary(c => c.Cell.Column, c => c.Bounds);
        var numbers = cells.Where(c => c.Cell.Column == 0 && c.Cell.Row > 0).ToDictionary(c => c.Cell.Row, c => c.Bounds);
        var body = cells.Where(c => c.Cell.Row > 0 && c.Cell.Column > 0).ToList();
        body.Should().NotBeEmpty();
        foreach (var (cell, bounds) in body)
        {
            bounds.X.Should().Be(headers[cell.Column].X, $"column {cell.Column} sits under its pinned cell");
            bounds.Y.Should().Be(numbers[cell.Row].Y, $"row {cell.Row} sits beside its pinned cell");
        }
        headers.Values.Should().OnlyContain(b => b.Y == Region(host).Bounds.Y, "the pinned row stays at the top");
    }

    [Fact]
    public void APressOnThePinnedRow_IsThePinnedCells()
    {
        var tally = new Tally();
        var host = Host(Table(tally, pressable: true), 900, 600, tally);
        host.ScrollBy(450, 300, 0, 14, stepped: false);
        Settle(host);
        var header = Cells(host).First(c => c.Cell.Row == 0 && c.Cell.Column == 2).Bounds;

        // Half a row is scrolled up under the pinned row: the press lands where both are drawn.
        host.PressDown(header.X + 10, header.Y + 20);
        host.PressUp(header.X + 10, header.Y + 20);

        tally.Pressed.Should().Be(1);
    }

    // ---- the unbounded axis --------------------------------------------------------------------

    [Fact]
    public void ATableThatFillsAnUnboundedAxis_TakesTheVisibleExtentOfItsScroller()
    {
        var tally = new Tally();
        var content = new Column(gap: 0);
        content.Add(new Text("intro", TypeRole.BodyM));
        content.Add(Table(tally, height: SizeValue.Fill));
        var page = new ScrollView(content);
        var host = Host(page, 700, 500, tally);

        Region(host).Bounds.Height.Should().Be(500);
        Cells(host).Should().HaveCountLessThan(1000, "its own scroll engaged, so only the window is built");
        tally.Logged.Should().ContainSingle().Which.Should().Contain("scroller around it").And.Contain("500");
    }

    // ---- the wheel -------------------------------------------------------------------------------

    [Fact]
    public void ShiftWithTheWheel_ScrollsAcross_AndATrackpadScrollsBoth()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);

        host.ScrollBy(450, 300, 0, 48, stepped: false, KeyModifiers.Shift);
        Region(host).Viewport.Scroll.Offset.Should().Be(new TableOffset(48, 0));

        host.ScrollBy(450, 300, 10, 30, stepped: false);
        Region(host).Viewport.Scroll.Offset.Should().Be(new TableOffset(58, 30));
    }

    [Fact]
    public void AtItsLastRow_TheWheelPassesToThePageAroundIt()
    {
        var tally = new Tally();
        var content = new Column(gap: 0);
        content.Add(Table(tally, rows: 20, height: SizeValue.Fixed(300)));
        content.Add(new Box(new BoxStyle { Height = SizeValue.Fixed(800), Width = SizeValue.Fill }));
        var page = new ScrollView(content);
        var host = Host(page, 700, 500, tally);
        var top = Region(host).Bounds.Y;

        var end = 20 * 28 - Region(host).Viewport.Scroll.Viewport.Height;

        // More than the table can take: it goes to its last row, and the rest moves the page.
        host.ScrollBy(300, 150, 0, end + 128, stepped: false).Should().BeTrue();
        Settle(host);

        Region(host).Viewport.Scroll.Offset.Down.Should().Be(end);
        Region(host).Bounds.Y.Should().Be(top - 128, "what the table could not take moved the page");
    }

    [Fact]
    public void AHorizontalSwipe_MovesAHorizontalScrollView()
    {
        var tally = new Tally();
        var strip = new ScrollView(new Box(new BoxStyle { Width = SizeValue.Fixed(3000), Height = SizeValue.Fixed(100) }),
            ScrollAxis.Horizontal);
        var host = Host(strip, 700, 500, tally);
        var before = Walk(host.LastFrame!.Root).Single(n => n.Source is Box).Bounds.X;

        host.ScrollBy(300, 50, 120, 0, stepped: false).Should().BeTrue();
        Settle(host);

        Walk(host.LastFrame!.Root).Single(n => n.Source is Box).Bounds.X.Should().Be(before - 120);
    }

    // ---- a finger --------------------------------------------------------------------------------

    [Fact]
    public void AFingerThatDragsTheCells_ScrollsThem_AndTheCellUnderItIsNotPressed()
    {
        var tally = new Tally();
        var host = Host(Table(tally, pressable: true), 900, 600, tally);

        host.PressDown(450, 400, kind: PointerKind.Touch);
        host.PointerMove(450, 360, PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), _clock += 16);
        host.PointerMove(450, 320, PointerKind.Touch);
        host.PressUp(450, 320, PointerKind.Touch);
        Settle(host);

        Region(host).Viewport.Scroll.Offset.Down.Should().Be(80);
        tally.Pressed.Should().Be(0);
    }

    [Fact]
    public void AFingerThatTapsACell_PressesIt()
    {
        var tally = new Tally();
        var host = Host(Table(tally, pressable: true), 900, 600, tally);

        host.PressDown(450, 400, kind: PointerKind.Touch);
        host.PressUp(450, 400, PointerKind.Touch);

        tally.Pressed.Should().Be(1);
        Region(host).Viewport.Scroll.Offset.Should().Be(TableOffset.Zero);
    }

    [Fact]
    public void AFingerLiftedMoving_Flings()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        host.ReducedMotion = false;
        host.SmoothScroll = true;

        host.PressDown(450, 500, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), _clock += 16);
        host.PointerMove(450, 440, PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), _clock += 16);
        host.PointerMove(450, 380, PointerKind.Touch);
        host.PressUp(450, 380, PointerKind.Touch);
        var lifted = Region(host).Viewport.Scroll.Offset.Down;
        for (var i = 0; i < 60; i++) host.RenderFrame(new DisplayListBuilder(), _clock += 16);

        Region(host).Viewport.Scroll.Offset.Down.Should().BeGreaterThan(lifted);
    }

    // ---- the scrollbars --------------------------------------------------------------------------

    [Fact]
    public void AThumbDraggedToTheEnd_ShowsTheLastRowAtTheBottom()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        var region = Region(host);
        var thumb = region.Viewport.Scroll.ThumbOf(ScrollAxis.Vertical)!.Value;
        var x = region.Bounds.X + thumb.X + thumb.Width / 2;

        host.PressDown(x, region.Bounds.Y + thumb.Y + 4).Should().BeTrue();
        host.PointerMove(x, region.Bounds.Y + 5000);
        host.PressUp(x, region.Bounds.Y + 5000);
        Settle(host);

        var last = Cells(host).Single(c => c.Cell.Row == ExcelRows - 1 && c.Cell.Column == 1).Bounds;
        (last.Y + last.Height).Should().Be(Region(host).Bounds.Y + Region(host).Viewport.Scroll.Viewport.Height);
    }

    // ---- requests and reports --------------------------------------------------------------------

    [Fact]
    public void ARevealRequest_BringsTheRowsBottomToTheWindowsBottom()
    {
        var tally = new Tally();
        var host = Host(Table(tally, request: new TableScrollRequest(1) { Reveal = new TableRange(40, 3) }), 900, 600, tally);

        var scroll = Region(host).Viewport.Scroll;
        (scroll.Offset.Down + scroll.Viewport.Height).Should().Be(41 * 28);
        Cells(host).Should().Contain(c => c.Cell.Row == 40 && c.Cell.Column == 3);
    }

    [Fact]
    public void AStillTable_ReportsNothingMore()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        var reported = tally.Scrolled;

        Settle(host);

        tally.Scrolled.Should().Be(reported);
        reported.Should().Be(1, "the first frame said where the grid stands, once");
    }

    // ---- marks -----------------------------------------------------------------------------------

    [Fact]
    public void AMarkAcrossThePinnedRow_IsDrawnInEachBandItReaches()
    {
        var tally = new Tally();
        var outline = new TableViewCell(0, 1, new Box(new BoxStyle { BorderWidth = 2 }), mark: true)
        {
            Rows = 6,
        };
        var host = Host(Table(tally, marks: [outline]), 900, 600, tally);
        host.ScrollBy(450, 300, 0, 84, stepped: false);
        Settle(host);

        var pieces = Cells(host, marks: true);
        pieces.Should().HaveCount(2);
        var top = Region(host).Bounds.Y;
        pieces.Should().Contain(p => p.Cell.Row == 0 && p.Cell.Rows == 1 && p.Bounds.Y == top);
        pieces.Should().Contain(p => p.Cell.Row == 1 && p.Cell.Rows == 5 && p.Bounds.Y == top + 28 - 84);
        // Keyed by the mark and the piece, so each keeps its element while the cells before it change.
        pieces.Select(p => p.Cell.Key).Should().BeEquivalentTo("mark:0/0", "mark:0/1");
    }

    [Fact]
    public void AKeyedMark_NamesEachPieceByItsBand()
    {
        // A gliding mark keeps the same band's piece from build to build however many bands it crosses, so
        // a piece never glides from another band's rectangle (#984).
        var tally = new Tally();
        var cursor = new TableViewCell(0, 1, new Box(new BoxStyle { BorderWidth = 2 }), mark: true)
        {
            Rows = 6,
            Key = "cursor",
        };
        var host = Host(Table(tally, marks: [cursor]), 900, 600, tally);
        host.ScrollBy(450, 300, 0, 84, stepped: false);
        Settle(host);

        Cells(host, marks: true).Select(p => p.Cell.Key).Should().BeEquivalentTo("cursor/pb", "cursor/bb");
    }

    [Fact]
    public void ARevealInARowThatGivesTheTableLessThanItFirstMeasured_StillShowsTheCell()
    {
        var tally = new Tally();
        var table = Table(tally, rows: 200, columns: 40,
            request: new TableScrollRequest(1) { Reveal = new TableRange(1, 10) });
        var row = new Row();
        row.Add(new Box(new BoxStyle { Width = 200, Height = 600 }));
        row.Add(table);
        var host = Host(row, 900, 600, tally);

        var region = Region(host);
        var cell = Cells(host).Single(c => c.Cell.Row == 1 && c.Cell.Column == 10);
        (cell.Bounds.X + cell.Bounds.Width).Should().BeLessThanOrEqualTo(
            region.Bounds.X + region.Viewport.Scroll.Viewport.Width + 0.5f,
            "the request is honoured against the width the row finally gave the table");
        cell.Bounds.X.Should().BeGreaterThanOrEqualTo(region.Bounds.X + 96);
    }

    [Fact]
    public void ATableWiderThanItsRoom_IsHeldToIt_WithItsScrollbarInside()
    {
        var tally = new Tally();
        var table = new TableView(new UniformTableSpans(100, 28), new UniformTableSpans(20, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM))
        {
            PinnedRows = 1,
            Width = SizeValue.Fixed(1000),
            Height = SizeValue.Fixed(300),
        };
        var host = Host(table, 600, 400, tally);

        var region = Region(host);
        region.Bounds.Width.Should().Be(600, "the web holds a table to its container's width, and so does Photon");
        region.Bounds.Height.Should().Be(300, "a height is its own");
        var track = region.Viewport.Scroll.TrackOf(ScrollAxis.Vertical)!.Value;
        (region.Bounds.X + track.X + track.Width).Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public void AMouseDragOverTheCells_ScrollsNothing()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);

        host.PressDown(450, 300, kind: PointerKind.Mouse);
        host.PointerMove(450, 200, PointerKind.Mouse);
        host.PressUp(450, 200, PointerKind.Mouse);
        Settle(host);

        Region(host).Viewport.Scroll.Offset.Should().Be(TableOffset.Zero, "a mouse's drag is the cells', a finger's pans");
    }

    [Fact]
    public void ADragHeldBelowTheCells_KeepsTheFramesComingAndScrolls()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        var scroll = Region(host).Viewport.Scroll;

        scroll.AutoScroll(new Point(450, scroll.Viewport.Height + 60));
        host.RenderFrame(new DisplayListBuilder(), _clock += 16);
        host.NeedsRender.Should().BeTrue("a held drag keeps the clock turning");
        for (var i = 0; i < 30; i++) host.RenderFrame(new DisplayListBuilder(), _clock += 16);

        scroll.Offset.Down.Should().BeApproximately(30 * 16 * 60 * 0.02, 0.5);
        scroll.AutoScroll(null);
        Settle(host);
        host.NeedsRender.Should().BeFalse();
    }

    // ---- the keyboard ------------------------------------------------------------------------------

    [Fact]
    public void TabToACellPastTheWindow_ScrollsTheTableToIt()
    {
        var tally = new Tally();
        var host = Host(Table(tally, rows: 100, columns: 3, pressable: true), 400, 200, tally);
        var top = Region(host).Bounds.Y;
        var reached = 0;

        for (var i = 0; i < 30; i++)
        {
            host.KeyDown("Tab");
            var frame = host.RenderFrame(new DisplayListBuilder(), _clock += 16);
            var stop = frame.FocusStops.FirstOrDefault(s => s.Id == host.FocusedId);
            if (stop.Element is null) continue;
            if (stop.Table is not null) continue;
            var view = Region(host).Viewport.Scroll.Viewport;
            // A pressable's target is grown to a finger's 48 points around its cell: its centre is the cell's.
            var centre = stop.Bounds.Y + stop.Bounds.Height / 2;
            centre.Should().BeGreaterThanOrEqualTo(top, $"Tab {i} focused a cell above the table");
            centre.Should().BeLessThanOrEqualTo(top + view.Height,
                $"Tab {i} focused a cell below the table's window without scrolling to it");
            reached = Math.Max(reached, (int)((centre - top + Region(host).Viewport.Scroll.Offset.Down) / 28));
        }

        reached.Should().BeGreaterThan(6, "the Tab order went past the rows the window first showed");
        Region(host).Viewport.Scroll.Offset.Down.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TheTabOrder_IsTheTableItself_ThenItsCellsRowByRow()
    {
        var tally = new Tally();
        // Two by two, a pinned row and a pinned column, all of it in view: painted body first.
        var host = Host(Table(tally, rows: 2, columns: 2, pressable: true), 400, 200, tally);

        var visited = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            host.KeyDown("Tab");
            var frame = host.RenderFrame(new DisplayListBuilder(), _clock += 16);
            var stop = frame.FocusStops.First(s => s.Id == host.FocusedId);
            visited.Add(stop.Table is not null ? "table" : CellText(stop.Element));
        }

        visited.Should().Equal("table", "0:0", "0:1", "1:0", "1:1");
    }

    /// <summary>What the cell holding a stop says: the table's builder writes its row and column.</summary>
    private static string CellText(Element at)
    {
        for (Element? e = at; e is not null; e = e.Parent)
            if (e.Configuration is TableViewCell cell) return $"{cell.Row}:{cell.Column}";
        return "?";
    }

    [Fact]
    public void ATableOfTextAlone_ScrollsFromTheKeyboard()
    {
        var tally = new Tally();
        var host = Host(Table(tally), 900, 600, tally);
        host.KeyDown("Tab");
        Settle(host);
        host.LastFrame!.FocusStops.First(s => s.Id == host.FocusedId).Table.Should().NotBeNull(
            "a table with nothing to press in its cells is still a stop, or a keyboard could not scroll it");

        host.KeyDown("PageDown").Should().BeTrue();
        Settle(host);
        var scroll = Region(host).Viewport.Scroll;
        scroll.Offset.Down.Should().Be(scroll.Viewport.Height - 28, "a page of the part under the pinned row");

        host.KeyDown("ArrowRight").Should().BeTrue();
        host.KeyDown("End", KeyModifiers.Command).Should().BeTrue();
        Settle(host);
        (scroll.Offset.Down + scroll.Viewport.Height).Should().Be(ExcelRows * 28.0);
        (scroll.Offset.Across + scroll.Viewport.Width).Should().Be(ExcelColumns * 96.0);

        host.KeyDown("Home").Should().BeTrue();
        Settle(host);
        scroll.Offset.Down.Should().Be(0);
        host.KeyDown("ArrowUp").Should().BeFalse("at the first row, the key travels on");
    }

    [Fact]
    public void ATableThatDeclinesTheFocus_IsNoStop_AndItsCellsStillAre()
    {
        var tally = new Tally();
        var host = Host(Table(tally, rows: 2, columns: 2, pressable: true, canRequestFocus: false), 400, 200, tally);

        var visited = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            host.KeyDown("Tab");
            var frame = host.RenderFrame(new DisplayListBuilder(), _clock += 16);
            var stop = frame.FocusStops.First(s => s.Id == host.FocusedId);
            visited.Add(stop.Table is not null ? "table" : CellText(stop.Element));
        }

        visited.Should().Equal("0:0", "0:1", "1:0", "1:1");
        host.KeyDown("ArrowDown").Should().BeFalse("the keys are the composing component's, not the table's");
        Region(host).Viewport.Scroll.Offset.Should().Be(TableOffset.Zero);
    }

    [Fact]
    public void AControlInsideAMark_IsNoStopOfTheTabOrder()
    {
        var tally = new Tally();
        var plain = Host(Table(tally, rows: 4, columns: 3), 400, 200, tally);
        var stops = plain.LastFrame!.FocusStops.Count;

        var mark = new TableViewCell(1, 1, new Pressable(new Text("ring", TypeRole.BodyM), () => { }), mark: true);
        var marked = Host(Table(tally, rows: 4, columns: 3, marks: [mark]), 400, 200, tally);

        marked.LastFrame!.FocusStops.Count.Should().Be(stops);
    }

    // ---- a parent that builds again ---------------------------------------------------------------

    /// <summary>A screen whose grid shrinks: its builder refuses a row the grid no longer has.</summary>
    private sealed class Shrinking(Tally tally) : StatefulComponent
    {
        private int _rows = 1000;

        public void Shrink() => SetState(() => _rows = 5);

        public override VisualNode Build(ComponentContext context)
        {
            var rows = _rows;
            return new TableView(new UniformTableSpans(rows, 28), new UniformTableSpans(10, 96), (row, column) =>
            {
                if (row >= rows) throw new InvalidOperationException($"row {row} asked of a grid of {rows}");
                tally.Built++;
                return new Text($"{row}:{column}", TypeRole.BodyM);
            })
            {
                PinnedRows = 1,
            };
        }
    }

    /// <summary>An axis that changes its count in place, as a spreadsheet's document does after a
    /// delete: the same object, fewer rows.</summary>
    private sealed class LiveSpans(int count, double extent) : TableSpans
    {
        public int Size { get; set; } = count;
        public override int Count => Size;
        public override double Total => Size * extent;
        public override double ExtentOf(int index) => extent;
        public override double OffsetOf(int index) => Math.Clamp(index, 0, Size) * extent;
        public override int IndexAt(double offset) => Size == 0 ? 0 : Math.Clamp((int)(offset / extent), 0, Size - 1);
    }

    /// <summary>A screen whose axes shrink in place: its builder refuses a row or a column they no
    /// longer have.</summary>
    private sealed class ShrinkingInPlace(Tally tally) : StatefulComponent
    {
        public LiveSpans Rows { get; } = new(11, 28);
        public LiveSpans Columns { get; } = new(11, 96);

        public void Shrink() => SetState(() =>
        {
            Rows.Size = 7;
            Columns.Size = 7;
        });

        public override VisualNode Build(ComponentContext context)
        {
            var rows = Rows;
            var columns = Columns;
            return new TableView(rows, columns, (row, column) =>
            {
                if (row >= rows.Count || column >= columns.Count)
                    throw new InvalidOperationException($"cell {row}:{column} asked of a grid of {rows.Count} by {columns.Count}");
                tally.Built++;
                return new Text($"{row}:{column}", TypeRole.BodyM);
            })
            {
                PinnedRows = 1,
                PinnedColumns = 1,
            };
        }
    }

    [Fact]
    public void AGridWhoseAxesShrinkInPlace_IsAskedOnlyForTheCellsItHas()
    {
        var tally = new Tally();
        var screen = new ShrinkingInPlace(tally);
        var host = Host(screen, 900, 600, tally);
        Cells(host).Max(c => c.Cell.Row).Should().Be(10, "the whole grid fits the window and its overscan");

        // The same axis objects, four rows and four columns fewer: AdoptConfig sees the same spans.
        screen.Shrink();
        Settle(host);

        Cells(host).Should().OnlyContain(c => c.Cell.Row < 7 && c.Cell.Column < 7);
        tally.Logged.Should().NotContain(line => line.Contains("asked of a grid"));
    }

    [Fact]
    public void AGridThatShrinks_IsAskedOnlyForTheRowsItHas()
    {
        var tally = new Tally();
        var screen = new Shrinking(tally);
        var host = Host(screen, 900, 600, tally);
        Cells(host).Max(c => c.Cell.Row).Should().BeGreaterThan(5);

        // The rows still in view are inside the window built before, which reached row 23.
        screen.Shrink();
        Settle(host);

        Cells(host).Select(c => c.Cell.Row).Should().OnlyContain(row => row < 5);
    }
}
