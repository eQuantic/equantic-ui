using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The table viewport's scroll, decided once for every target: the axes' arithmetic at Excel's extent,
/// the window and its scrollbars, the wheel, a finger, the scrollbars' gestures and the requests. The
/// same numbers are asserted against the transpiled model in <c>table-scroll.spec.ts</c>, so a
/// movement cannot mean one distance here and another in a browser.
/// </summary>
public class TableScrollTests
{
    private const int ExcelRows = 1_048_576;
    private const int ExcelColumns = 16_384;

    private static readonly TableSpans Rows = new UniformTableSpans(ExcelRows, 28);
    private static readonly TableSpans Columns = new UniformTableSpans(ExcelColumns, 96);

    /// <summary>A scroll arranged in a 900 × 600 window over Excel's grid, one row and one column
    /// pinned, as a header strip and a row-number column would be.</summary>
    private static TableScroll Arranged(bool reduceMotion = true, int rows = ExcelRows)
    {
        var scroll = new TableScroll();
        scroll.Arrange(new Size(900, 600), Columns.Total, rows * 28.0, 96, 28, reduceMotion);
        return scroll;
    }

    // ---- the axes ------------------------------------------------------------------------------

    [Fact]
    public void AUniformAxis_ReachesExcelsLastRowExactly()
    {
        Rows.Total.Should().Be(29_360_128);
        Rows.OffsetOf(ExcelRows - 1).Should().Be(29_360_100);
        Rows.IndexAt(29_360_127.5).Should().Be(ExcelRows - 1);
        Rows.IndexAt(-5).Should().Be(0);
        Rows.IndexAt(1e12).Should().Be(ExcelRows - 1);
    }

    [Fact]
    public void ASparseAxis_AddsItsOverridesAtBothEnds()
    {
        var spans = new SparseTableSpans(ExcelRows, 28, new Dictionary<int, double>
        {
            [0] = 40,
            [10] = 100,
            [ExcelRows - 1] = 60,
            [ExcelRows + 5] = 999,   // past the axis: ignored
        });

        spans.ExtentOf(0).Should().Be(40);
        spans.ExtentOf(1).Should().Be(28);
        spans.ExtentOf(10).Should().Be(100);
        spans.OffsetOf(1).Should().Be(40);
        spans.OffsetOf(11).Should().Be(40 + 9 * 28 + 100);
        spans.Total.Should().Be(ExcelRows * 28.0 + 12 + 72 + 32);
        spans.IndexAt(spans.OffsetOf(11)).Should().Be(11);
        spans.IndexAt(spans.OffsetOf(11) - 0.5).Should().Be(10);
        spans.IndexAt(spans.Total - 1).Should().Be(ExcelRows - 1);
    }

    // ---- the window and its scrollbars ---------------------------------------------------------

    [Fact]
    public void AGridThatOverflowsBothAxes_TakesAStripForEachScrollbar()
    {
        var scroll = Arranged();

        scroll.Viewport.Should().Be(new Size(900 - TableScroll.Thickness, 600 - TableScroll.Thickness));
        scroll.TrackOf(ScrollAxis.Vertical).Should().Be(new Rect(888, 0, 12, 588));
        scroll.TrackOf(ScrollAxis.Horizontal).Should().Be(new Rect(0, 588, 888, 12));
    }

    [Fact]
    public void AGridThatFits_DrawsNoScrollbar()
    {
        var scroll = new TableScroll();
        scroll.Arrange(new Size(900, 600), 500, 300, 0, 0, true);

        scroll.Viewport.Should().Be(new Size(900, 600));
        scroll.TrackOf(ScrollAxis.Vertical).Should().BeNull();
        scroll.ThumbOf(ScrollAxis.Horizontal).Should().BeNull();
    }

    [Fact]
    public void AMillionRows_StillLeaveAThumbAPointerCanGrab()
    {
        Arranged().ThumbOf(ScrollAxis.Vertical)!.Value.Height.Should().Be(TableScroll.MinThumb);
    }

    // ---- the wheel ------------------------------------------------------------------------------

    [Fact]
    public void AWheel_ScrollsDown_AndShiftTurnsItAcross()
    {
        var scroll = Arranged();

        scroll.Wheel(0, 84, stepped: false, KeyModifiers.None);
        scroll.Offset.Should().Be(new TableOffset(0, 84));

        // Three lines with Shift held: right by three lines, and not down.
        scroll.Wheel(0, 48, stepped: true, KeyModifiers.Shift);
        scroll.Offset.Should().Be(new TableOffset(48, 84));
    }

    [Fact]
    public void AShiftedWheelAtTheLastColumn_LeavesItsMovementOnTheAxisItCameOn()
    {
        var scroll = Arranged();
        scroll.Wheel(Columns.Total, 0, stepped: false, KeyModifiers.None);

        // Nothing is taken, and the caller is told so on its own axes: the page under the grid gets it.
        var left = scroll.Wheel(0, 100, stepped: false, KeyModifiers.Shift);

        left.Should().Be(new TableOffset(0, 100));
        scroll.Offset.Across.Should().Be(Columns.Total - scroll.Viewport.Width);
    }

    [Fact]
    public void AnAreaOutOfView_IsRevealedClearOfThePinnedBands_OnTheAxesAsked()
    {
        var scroll = Arranged();
        scroll.Wheel(300, 0, stepped: false, KeyModifiers.None);

        // Below the window's 588 points: down by what it overhangs. Hidden under the 96-point pinned
        // column, but asked to move down only: across stays where it was.
        scroll.Reveal(new Rect(50, 700, 96, 28), across: false, down: true).Should().BeTrue();
        scroll.Offset.Should().Be(new TableOffset(300, 140));

        // Asked across too, it comes out from under the pinned column.
        scroll.Reveal(new Rect(50, 300, 96, 28), across: true, down: true).Should().BeTrue();
        scroll.Offset.Should().Be(new TableOffset(254, 140));

        // In view already: nothing moves.
        scroll.Reveal(new Rect(400, 300, 96, 28), across: true, down: true).Should().BeFalse();
    }

    [Fact]
    public void ATrackpad_MovesBothAxesByItsMovement()
    {
        var scroll = Arranged();

        scroll.Wheel(12.5, 30.25, stepped: false, KeyModifiers.None);

        scroll.Offset.Should().Be(new TableOffset(12.5, 30.25));
    }

    [Fact]
    public void AtTheEndOfTheGrid_TheWheelLeavesWhatItCouldNotTake()
    {
        var scroll = Arranged();
        var end = ExcelRows * 28.0 - scroll.Viewport.Height;
        scroll.Wheel(0, end, stepped: false, KeyModifiers.None);

        var left = scroll.Wheel(0, 100, stepped: false, KeyModifiers.None);

        scroll.Offset.Down.Should().Be(end);
        left.Should().Be(new TableOffset(0, 100));
    }

    [Fact]
    public void AWheelsNotch_Glides_AndUnderReducedMotionLandsAtOnce()
    {
        var smooth = Arranged(reduceMotion: false);
        smooth.Wheel(0, 100, stepped: true, KeyModifiers.None);
        smooth.Offset.Down.Should().Be(0, "a notch glides from where it stands");

        smooth.Advance(1000).Should().BeTrue();
        smooth.Advance(1016).Should().BeTrue();
        smooth.Offset.Down.Should().BeInRange(1, 99);
        for (var t = 1032; smooth.Advance(t); t += 16) { }
        smooth.Offset.Down.Should().Be(100);

        var still = Arranged(reduceMotion: true);
        still.Wheel(0, 100, stepped: true, KeyModifiers.None);
        still.Offset.Down.Should().Be(100);
    }

    // ---- a finger -------------------------------------------------------------------------------

    [Fact]
    public void AFinger_CarriesTheGrid_AndFlingsWhenItLiftsMoving()
    {
        var scroll = Arranged(reduceMotion: false);

        scroll.Pointer(PointerPhase.Down, new Point(400, 400), PointerKind.Touch, 0).Should().BeTrue();
        scroll.Pointer(PointerPhase.Move, new Point(390, 360), PointerKind.Touch, 16);
        scroll.Pointer(PointerPhase.Move, new Point(380, 320), PointerKind.Touch, 32);
        scroll.Offset.Should().Be(new TableOffset(20, 80));

        scroll.Pointer(PointerPhase.Up, new Point(380, 320), PointerKind.Touch, 40).Should().BeTrue();
        var lifted = scroll.Offset.Down;
        scroll.Advance(100).Should().BeTrue();
        for (var t = 116; scroll.Advance(t); t += 16) { }

        scroll.Offset.Down.Should().BeGreaterThan(lifted, "the grid keeps moving after the finger lifts");
        // Where it lands is the release speed's reach, the number the transpiled twin asserts too.
        scroll.Offset.Down.Should().Be(296);
        scroll.Offset.Down.Should().BeLessThanOrEqualTo(ExcelRows * 28.0 - scroll.Viewport.Height);
    }

    [Fact]
    public void AFingerThatRestedBeforeLifting_DoesNotFling()
    {
        var scroll = Arranged(reduceMotion: false);
        scroll.Pointer(PointerPhase.Down, new Point(400, 400), PointerKind.Touch, 0);
        scroll.Pointer(PointerPhase.Move, new Point(400, 300), PointerKind.Touch, 16);

        scroll.Pointer(PointerPhase.Up, new Point(400, 300), PointerKind.Touch, 400);

        scroll.Advance(500).Should().BeFalse();
        scroll.Offset.Down.Should().Be(100);
    }

    [Fact]
    public void AMousePressOnTheCells_IsTheCells()
    {
        Arranged().Pointer(PointerPhase.Down, new Point(400, 300), PointerKind.Mouse, 0).Should().BeFalse();
    }

    // ---- the scrollbars -------------------------------------------------------------------------

    [Fact]
    public void AThumbDraggedToTheEnd_PutsTheLastRowAtTheBottom()
    {
        var scroll = Arranged();
        var thumb = scroll.ThumbOf(ScrollAxis.Vertical)!.Value;

        scroll.Pointer(PointerPhase.Down, new Point(thumb.X + 5, thumb.Y + 5), PointerKind.Mouse, 0).Should().BeTrue();
        scroll.Pointer(PointerPhase.Move, new Point(thumb.X + 5, 5000), PointerKind.Mouse, 16);
        scroll.Pointer(PointerPhase.Up, new Point(thumb.X + 5, 5000), PointerKind.Mouse, 32);

        (scroll.Offset.Down + scroll.Viewport.Height).Should().Be(ExcelRows * 28.0);
    }

    [Fact]
    public void APressOnTheTrack_MovesTheWindowByItsScrollingHeight()
    {
        var scroll = Arranged();
        var track = scroll.TrackOf(ScrollAxis.Vertical)!.Value;

        scroll.Pointer(PointerPhase.Down, new Point(track.X + 5, 500), PointerKind.Mouse, 0).Should().BeTrue();

        scroll.Offset.Down.Should().Be(scroll.Viewport.Height - 28);
    }

    // ---- a held drag ------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADragHeldBelowTheCells_ScrollsDownByItsDistance_UnderReducedMotionToo(bool reduceMotion)
    {
        var scroll = Arranged(reduceMotion);

        // 60 points below the 588 the cells scroll in: 1.2 points a millisecond.
        scroll.AutoScroll(new Point(400, 588 + 60));
        scroll.Advance(0).Should().BeTrue();
        scroll.Advance(500).Should().BeTrue();

        scroll.Offset.Down.Should().BeApproximately(600, 0.001);
        scroll.Offset.Across.Should().Be(0);
    }

    [Fact]
    public void ADragHeldOverThePinnedRow_ScrollsUp()
    {
        var scroll = Arranged();
        scroll.Wheel(0, 500, stepped: false, KeyModifiers.None);

        // 18 points above the cells' top edge, the pinned row's bottom at 28.
        scroll.AutoScroll(new Point(400, 10));
        scroll.Advance(0);
        scroll.Advance(500);

        scroll.Offset.Down.Should().BeApproximately(500 - 18 * 0.02 * 500, 0.001);
    }

    [Fact]
    public void ADragHeldFurtherPast_ScrollsFaster()
    {
        var near = Arranged();
        near.AutoScroll(new Point(400, 588 + 30));
        near.Advance(0);
        near.Advance(1000);
        var far = Arranged();
        far.AutoScroll(new Point(400, 588 + 120));
        far.Advance(0);
        far.Advance(1000);

        far.Offset.Down.Should().BeGreaterThan(near.Offset.Down);
    }

    [Fact]
    public void ADragHeldAtTheGridsEnd_StopsThere_AndALetGoStopsIt()
    {
        var scroll = Arranged(rows: 30);
        scroll.AutoScroll(new Point(400, 588 + 60));
        scroll.Advance(0);

        scroll.Advance(1000).Should().BeFalse("the grid's end is reached");
        scroll.Offset.Down.Should().Be(30 * 28 - 588);

        scroll.Wheel(0, -100, stepped: false, KeyModifiers.None);
        scroll.AutoScroll(null);
        scroll.Advance(2000).Should().BeFalse();
        scroll.Advance(3000).Should().BeFalse();
        scroll.Offset.Down.Should().Be(30 * 28 - 588 - 100, "a drag that ended moves nothing more");
    }

    // ---- requests -------------------------------------------------------------------------------

    [Fact]
    public void ARevealThatCrossesThePinnedBands_BringsInThePartThatScrolls()
    {
        var scroll = Arranged();
        scroll.Wheel(500, 500, stepped: false, KeyModifiers.None);

        // The pinned row and column with the first row and column under them: those come out from
        // under the bands, on each axis.
        scroll.Apply(new TableScrollRequest(1) { Reveal = new TableRange(0, 0, 2, 2) }, Rows, Columns, 1, 1)
            .Should().BeTrue();
        scroll.Offset.Should().Be(TableOffset.Zero);

        // A range wholly pinned on an axis moves nothing on it.
        scroll.Wheel(500, 500, stepped: false, KeyModifiers.None);
        scroll.Apply(new TableScrollRequest(2) { Reveal = new TableRange(0, 0, 1, 1) }, Rows, Columns, 1, 1)
            .Should().BeFalse();
        scroll.Offset.Should().Be(new TableOffset(500, 500));
    }

    [Fact]
    public void ARevealOfARowBelowTheWindow_BringsItsBottomEdgeToTheWindowsBottom()
    {
        var scroll = Arranged();

        scroll.Apply(new TableScrollRequest(1) { Reveal = new TableRange(40, 3) }, Rows, Columns, 1, 1)
            .Should().BeTrue();

        (scroll.Offset.Down + scroll.Viewport.Height).Should().Be(41 * 28);
        scroll.Offset.Across.Should().Be(0);
    }

    [Fact]
    public void ARevealOfACellUnderThePinnedRow_BringsItJustBelowTheBand()
    {
        var scroll = Arranged();
        scroll.Wheel(0, 2800, stepped: false, KeyModifiers.None);

        scroll.Apply(new TableScrollRequest(1) { Reveal = new TableRange(50, 3) }, Rows, Columns, 1, 1);

        (Rows.OffsetOf(50) - scroll.Offset.Down).Should().Be(28);
    }

    [Fact]
    public void ARequestAlreadyHonoured_MovesNothing()
    {
        var scroll = Arranged();
        var request = new TableScrollRequest(7) { Reveal = new TableRange(40, 3) };
        scroll.Apply(request, Rows, Columns, 1, 1);
        scroll.Wheel(0, -500, stepped: false, KeyModifiers.None);
        var away = scroll.Offset;

        scroll.Apply(request, Rows, Columns, 1, 1).Should().BeFalse();

        scroll.Offset.Should().Be(away);
    }

    [Fact]
    public void ARequestToAnOffset_IsClampedToTheGrid()
    {
        var scroll = Arranged(rows: 100);

        scroll.Apply(new TableScrollRequest(1) { Offset = new TableOffset(-10, 1e9) }, Rows, Columns, 1, 1);

        scroll.Offset.Should().Be(new TableOffset(0, 100 * 28.0 - scroll.Viewport.Height));
    }
}
