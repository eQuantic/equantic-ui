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
        SizeValue height = default) =>
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
    }
}
