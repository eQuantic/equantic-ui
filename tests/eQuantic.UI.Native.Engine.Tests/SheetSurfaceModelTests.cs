using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet engine as a realizer drives it (<see cref="ISheetSurfaceModel"/>, #185): every
/// decision a host used to make for itself, a point turned into a cell, what a press means, which
/// keys the clipboard serves, is the engine's answer now, asked here without any host at all.
/// </summary>
public class SheetSurfaceModelTests
{
    // Default cells are 96×28.
    private static Point CenterOf(int row, int col, Point origin = default) =>
        new(origin.X + col * 96f + 48f, origin.Y + row * 28f + 14f);

    private sealed class FakeClipboard : ITextClipboard
    {
        public string? Held;
        public void Write(string text) => Held = text;
        public string? Read() => Held;
    }

    /// <summary>A clipboard another program holds: every write is refused.</summary>
    private sealed class RefusingClipboard : ITextClipboard
    {
        public void Write(string text) => throw new IOException("the clipboard is held by another program");
        public string? Read() => null;
    }

    private static ISheetSurfaceModel Press(SheetController sheet, Point at,
        KeyModifiers modifiers = KeyModifiers.None, int clicks = 1)
    {
        ISheetSurfaceModel model = sheet;
        model.HandlePointer(PointerPhase.Down, at, modifiers, clicks, PointerKind.Mouse);
        return model;
    }

    // ---- where the engine lives -------------------------------------------------------------------

    [Fact]
    public void TheVocabulary_DeclaresNoneOfTheEnginesTypes()
    {
        var engine = typeof(SheetController).Assembly;
        var vocabulary = typeof(VisualNode).Assembly;

        engine.Should().NotBeSameAs(vocabulary);
        vocabulary.GetExportedTypes().Select(type => type.Name)
            .Should().NotIntersectWith(engine.GetExportedTypes().Select(type => type.Name),
                "the spreadsheet's controller, document, history, keymap and cells are the engine's (#185)");
        var code = typeof(eQuantic.UI.Code.CodeEditorController).Assembly.GetName().Name;
        engine.GetReferencedAssemblies().Select(name => name.Name)
            .Should().Contain(vocabulary.GetName().Name, "the engine is written against the vocabulary")
            .And.Contain(code, "and a cell's entry is the code engine's document (#983)")
            .And.NotContain(name => name!.StartsWith("eQuantic.UI.") && name != vocabulary.GetName().Name && name != code,
                "and against nothing else of the SDK");
    }

    // ---- the pointer ---------------------------------------------------------------------------

    [Fact]
    public void APress_InAScrolledWindow_LandsOnTheCellTheWindowDrawsThere()
    {
        // Down by 40 rows and across by one column; the press is in the second column and the third row
        // shown under the headers.
        var sheet = new SheetController(rows: 200, cols: 8);
        SheetScrolls.Place(sheet, across: 96, down: 40 * 28, width: 400);

        Press(sheet, CenterOf(2, 1, new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight)));

        sheet.Selection.Should().Be(new SheetRange(new CellRef(42, 2)), "row 42 counted from 0, column C");
        sheet.ActiveCell.Should().Be(new CellRef(42, 2));
    }

    [Fact]
    public void APress_ReadsTheOffsetAtThatMoment_NotTheOneTheGridWasHandedWith()
    {
        // A scroll step inside the cells already built builds nothing, so the geometry is never handed
        // over again: the engine has to read the scroll as it stands.
        var sheet = new SheetController(rows: 200, cols: 8);
        var scroll = SheetScrolls.Place(sheet);
        scroll.Wheel(0, 28 * 5, stepped: false, KeyModifiers.None);

        Press(sheet, CenterOf(0, 0, new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight)));

        sheet.ActiveCell.Should().Be(new CellRef(5, 0));
    }

    [Fact]
    public void APointOverThePinnedHeaders_ReadsTheFirstCellShownPastThem()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        SheetScrolls.Place(sheet, across: 96, down: 40 * 28, width: 400);

        sheet.CellAt(new Point(SheetScrolls.HeaderWidth + 10, 5)).Should().Be(new CellRef(40, 1),
            "over the header row, the first row shown");
        sheet.CellAt(new Point(5, SheetScrolls.HeaderHeight + 10)).Should().Be(new CellRef(40, 1),
            "over the row numbers, the first column shown");
        sheet.CellAt(new Point(100_000, 100_000)).Should().Be(new CellRef(199, 7), "and one past the last holds the far edge");
    }

    [Fact]
    public void TheFillHandle_IsGrabbedWhereTheScrolledGridDrawsIt()
    {
        var sheet = new SheetController(rows: 200, cols: 8) { Selection = new SheetRange(new CellRef(42, 2)) };
        SheetScrolls.Place(sheet, across: 96, down: 40 * 28, width: 400);

        // The bottom-right corner of C43: past the headers, less the offset.
        Press(sheet, new Point(SheetScrolls.HeaderWidth + 3 * 96 - 96, SheetScrolls.HeaderHeight + 43 * 28 - 40 * 28));

        sheet.Filling.Should().BeTrue();
    }

    [Fact]
    public void ADrag_FromAPlainPress_MovesTheFarCorner_AndTheReleaseEndsTheRange()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        var model = Press(sheet, CenterOf(1, 1));

        model.HandlePointer(PointerPhase.Move, CenterOf(3, 3), KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 3), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeFalse("the same cell again changes nothing");
        sheet.Selecting.Should().BeTrue();
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 3), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeTrue("the range is no longer being drawn, which the name box shows");
        sheet.Selecting.Should().BeFalse();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(3, 3)));
        sheet.ActiveCell.Should().Be(new CellRef(1, 1), "the cell the drag began on stays active");
        model.HandlePointer(PointerPhase.Move, CenterOf(5, 5), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeFalse("once the press lifts, a move is a hover");
    }

    [Fact]
    public void APressWithShift_StretchesFromTheAnchor_AndStartsNoDrag()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        Press(sheet, CenterOf(1, 1));
        ((ISheetSurfaceModel)sheet).HandlePointer(PointerPhase.Up, CenterOf(1, 1), KeyModifiers.None, 1, PointerKind.Mouse);

        var model = Press(sheet, CenterOf(4, 3), KeyModifiers.Shift);
        model.HandlePointer(PointerPhase.Move, CenterOf(6, 6), KeyModifiers.Shift, 1, PointerKind.Mouse).Should().BeFalse();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(4, 3)));
        sheet.ActiveCell.Should().Be(new CellRef(1, 1), "Excel leaves the active cell standing");
    }

    [Fact]
    public void TheSecondPressOfADoubleClick_EditsTheCell_StartingFromItsValue()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(2, 2), "42");

        Press(sheet, CenterOf(2, 2));
        Press(sheet, CenterOf(2, 2), clicks: 2);

        sheet.Editing.Should().BeTrue();
        sheet.Entry.Document.Text.Should().Be("42");
        sheet.ActiveCell.Should().Be(new CellRef(2, 2));
    }

    // ---- the pointer's look ------------------------------------------------------------------

    [Fact]
    public void ThePointer_IsACrosshair_WhereAPressWouldGrabTheFillHandle()
    {
        var sheet = new SheetController(rows: 50, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        ISheetSurfaceModel model = sheet;

        model.CursorAt(new Point(2 * 96 - 3, 2 * 28 - 3)).Should().Be(PointerCursor.Crosshair);
        model.CursorAt(CenterOf(1, 1)).Should().Be(PointerCursor.Default, "a cell's middle grabs nothing");

        sheet.BeginEdit("x", SheetEditMode.Enter);
        model.CursorAt(new Point(2 * 96 - 3, 2 * 28 - 3)).Should().Be(PointerCursor.Text,
            "an edit draws no handle: the corner is the cell's editor, which takes a beam");
    }

    [Fact]
    public void ACornerScrolledUnderTheHeaders_IsNoHandle()
    {
        // B2's corner stands under the header row once the grid is two rows down: nothing draws the
        // handle there, so nothing grabs it, and the press selects the first row shown.
        var sheet = new SheetController(rows: 200, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        SheetScrolls.Place(sheet, down: 2 * 28 + 10);
        var corner = new Point(SheetScrolls.HeaderWidth + 2 * 96, SheetScrolls.HeaderHeight - 10);

        ((ISheetSurfaceModel)sheet).CursorAt(corner).Should().Be(PointerCursor.Default);
        Press(sheet, corner);
        sheet.Filling.Should().BeFalse();
    }

    // ---- a finger ---------------------------------------------------------------------------

    [Fact]
    public void AFingersTap_SelectsTheCellItBeganOn_WhenItLifts()
    {
        var sheet = new SheetController(rows: 50, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, CenterOf(4, 3), KeyModifiers.None, 1, PointerKind.Touch)
            .Should().BeFalse("a finger's press means nothing until it lifts: the grid may take it as a pan");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)));

        model.HandlePointer(PointerPhase.Up, CenterOf(4, 3), KeyModifiers.None, 1, PointerKind.Touch).Should().BeTrue();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(4, 3)), "D5");
    }

    [Fact]
    public void AFingersPan_TheGridTakes_SelectsNothing()
    {
        var sheet = new SheetController(rows: 50, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, CenterOf(4, 3), KeyModifiers.None, 1, PointerKind.Touch);
        model.HandlePointer(PointerPhase.Move, CenterOf(2, 3), KeyModifiers.None, 1, PointerKind.Touch)
            .Should().BeFalse("a finger never drags a selection");
        model.HandlePointer(PointerPhase.Cancel, CenterOf(2, 3), KeyModifiers.None, 1, PointerKind.Touch);
        model.HandlePointer(PointerPhase.Up, CenterOf(2, 3), KeyModifiers.None, 1, PointerKind.Touch)
            .Should().BeFalse("the pan's end is no tap");

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)), "the selection stays B2");
        sheet.Filling.Should().BeFalse();
    }

    [Fact]
    public void AFingerOnTheFillHandle_SelectsTheCellUnderIt_AndPoursNothing()
    {
        // Dragging the handle by touch arrives with the long press: until then a finger pans everywhere.
        var sheet = new SheetController(rows: 50, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, new Point(2 * 96, 2 * 28), KeyModifiers.None, 1, PointerKind.Touch);

        sheet.Filling.Should().BeFalse();
    }

    [Fact]
    public void AStylus_PressesAsAMouseDoes()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, CenterOf(4, 3), KeyModifiers.None, 1, PointerKind.Stylus)
            .Should().BeTrue();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(4, 3)));
    }

    // ---- a drag held past an edge ---------------------------------------------------------------

    /// <summary>Frames of the scroll's clock for <paramref name="milliseconds"/>, the engine following
    /// every one that moved the grid, as the component does from the viewport's report.</summary>
    private static void Hold(SheetController sheet, TableScroll scroll, double milliseconds, ref double clock)
    {
        for (var elapsed = 0.0; elapsed <= milliseconds; elapsed += 16)
        {
            var before = scroll.Offset;
            scroll.Advance(clock);
            clock += 16;
            if (scroll.Offset != before) sheet.Follow();
        }
    }

    [Fact]
    public void ASelectionHeldBelowTheGrid_ScrollsIt_AndKeepsExtending_UntilTheRelease()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        var scroll = SheetScrolls.Place(sheet);
        var origin = new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight);
        var model = Press(sheet, CenterOf(1, 1, origin));
        var below = new Point(origin.X + 96 + 48, scroll.Viewport.Height + 60);

        model.HandlePointer(PointerPhase.Move, below, KeyModifiers.None, 1, PointerKind.Mouse);
        var clock = 0.0;
        Hold(sheet, scroll, 1000, ref clock);

        scroll.Offset.Down.Should().BeGreaterThan(0, "the grid scrolled down");
        sheet.Selection.Anchor.Should().Be(new CellRef(1, 1));
        sheet.Selection.Focus.Row.Should().BeGreaterThan(20, "the selection reached rows past the ones first shown");

        model.HandlePointer(PointerPhase.Up, below, KeyModifiers.None, 1, PointerKind.Mouse);
        var stopped = scroll.Offset;
        Hold(sheet, scroll, 200, ref clock);
        scroll.Offset.Should().Be(stopped, "the release stops the scroll");
    }

    [Fact]
    public void FurtherPastTheEdge_ScrollsFaster()
    {
        double Scrolled(float pastTheEdge)
        {
            var sheet = new SheetController(rows: 1000, cols: 8);
            var scroll = SheetScrolls.Place(sheet);
            var model = Press(sheet, CenterOf(1, 1, new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight)));
            model.HandlePointer(PointerPhase.Move, new Point(200, scroll.Viewport.Height + pastTheEdge),
                KeyModifiers.None, 1, PointerKind.Mouse);
            var clock = 0.0;
            Hold(sheet, scroll, 1000, ref clock);
            return scroll.Offset.Down;
        }

        Scrolled(120).Should().BeGreaterThan(Scrolled(30));
    }

    [Fact]
    public void AFillHeldBelowTheGrid_ReachesTheCellsThatScrollIn_AndACancelPoursNothing()
    {
        var sheet = new SheetController(rows: 200, cols: 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        var scroll = SheetScrolls.Place(sheet);
        var model = Press(sheet, new Point(SheetScrolls.HeaderWidth + 2 * 96, SheetScrolls.HeaderHeight + 2 * 28));
        sheet.Filling.Should().BeTrue();

        model.HandlePointer(PointerPhase.Move, new Point(200, scroll.Viewport.Height + 60), KeyModifiers.None, 1,
            PointerKind.Mouse);
        var clock = 0.0;
        Hold(sheet, scroll, 1000, ref clock);

        sheet.FillTarget!.Value.BottomRow.Should().BeGreaterThan(20, "the preview follows the cells that scrolled in");

        model.HandlePointer(PointerPhase.Cancel, default, KeyModifiers.None, 1, PointerKind.Mouse);
        var stopped = scroll.Offset;
        Hold(sheet, scroll, 200, ref clock);
        scroll.Offset.Should().Be(stopped, "the cancel stops the scroll");
        sheet.Document.GetCell(new CellRef(2, 1)).Should().BeEmpty("a fill the system takes away pours nothing");
        sheet.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void AMovePastTheEdge_AsksForFrames_EvenOnTheCellItAlreadyReached()
    {
        // The host asks for frames only when a move answers true; the scroll the move arms needs them.
        var sheet = new SheetController(rows: 200, cols: 8);
        var scroll = SheetScrolls.Place(sheet);
        ISheetSurfaceModel model = sheet;
        var x = SheetScrolls.HeaderWidth + 96 + 48;
        Press(sheet, new Point(x, SheetScrolls.HeaderHeight + 28 + 14));
        model.HandlePointer(PointerPhase.Move, new Point(x, scroll.Viewport.Height - 2), KeyModifiers.None, 1, PointerKind.Mouse);

        model.HandlePointer(PointerPhase.Move, new Point(x, scroll.Viewport.Height + 3), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeTrue("the pointer is past the bottom edge, where the grid scrolls");
        model.HandlePointer(PointerPhase.Move, new Point(x, scroll.Viewport.Height + 9), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeTrue("and still past it");
    }

    [Fact]
    public void ADragPastTheEdge_HoldsTheLastCellShown_UntilTheGridScrollsMore()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        var scroll = SheetScrolls.Place(sheet);
        var x = SheetScrolls.HeaderWidth + 96 + 48;
        var model = Press(sheet, new Point(x, SheetScrolls.HeaderHeight + 28 + 14));

        model.HandlePointer(PointerPhase.Move, new Point(x, scroll.Viewport.Height + 60), KeyModifiers.None, 1, PointerKind.Mouse);

        var lastShown = sheet.Document.RowSpans.IndexAt(scroll.Offset.Down + scroll.Viewport.Height - SheetScrolls.HeaderHeight - 1);
        sheet.Selection.Focus.Row.Should().Be(lastShown, "the drag holds the edge the person sees, as Excel's does");
    }

    [Fact]
    public void ADragInsideTheCells_LeavesAGlideInFlightAlone()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        var scroll = SheetScrolls.Place(sheet);
        scroll.Arrange(new Size(900, 586), SheetScrolls.HeaderWidth + sheet.Document.ColumnSpans.Total,
            SheetScrolls.HeaderHeight + sheet.Document.RowSpans.Total, SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight,
            reduceMotion: false);
        scroll.Wheel(0, 120, stepped: true, KeyModifiers.None);
        var model = Press(sheet, CenterOf(1, 1, new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight)));

        model.HandlePointer(PointerPhase.Move, CenterOf(3, 2, new Point(SheetScrolls.HeaderWidth, SheetScrolls.HeaderHeight)),
            KeyModifiers.None, 1, PointerKind.Mouse);
        scroll.Advance(0);
        scroll.Advance(400);

        scroll.Offset.Down.Should().BeApproximately(120, 0.5, "the notch's glide went on to where it was going");
    }

    [Fact]
    public void ASelectionSetFromOutside_IsRevealed_AndAPressIsNot()
    {
        var sheet = new SheetController(rows: 2000, cols: 8);
        var version = sheet.RevealVersion;

        sheet.Selection = new SheetRange(new CellRef(500, 2));
        sheet.RevealVersion.Should().BeGreaterThan(version);
        sheet.RevealCell.Should().Be(new CellRef(500, 2));

        version = sheet.RevealVersion;
        Press(sheet, CenterOf(3, 1));
        sheet.RevealVersion.Should().Be(version);
    }

    [Fact]
    public void Follow_WithNoDragInFlight_ChangesNothing()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        SheetScrolls.Place(sheet, down: 28 * 10);

        sheet.Follow().Should().BeFalse();
        Press(sheet, CenterOf(1, 1));
        ((ISheetSurfaceModel)sheet).HandlePointer(PointerPhase.Up, CenterOf(1, 1), KeyModifiers.None, 1, PointerKind.Mouse);
        sheet.Follow().Should().BeFalse("a released press is no drag");
    }

    [Fact]
    public void APressWhileEditing_LandsTheDraftFirst()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        ((ISheetSurfaceModel)sheet).HandleText("9");

        Press(sheet, CenterOf(3, 3));

        sheet.Editing.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("9");
        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
    }

    [Fact]
    public void APressOnTheFillHandle_PoursTheSource_AsOneUndoStep()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;

        // B2's bottom-right corner, where the handle is drawn, give or take the grab.
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f - 5, 2 * 28f + 5), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeTrue();
        sheet.Filling.Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();

        sheet.Filling.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("7");
        sheet.Document.GetCell(new CellRef(3, 1)).Should().Be("7");
        sheet.Undo().Should().BeTrue();
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("");
        sheet.Document.GetCell(new CellRef(3, 1)).Should().Be("");
    }

    [Fact]
    public void ACancelledFill_PoursNothing_AndACancelledDragKeepsWhereItStood()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();

        sheet.Filling.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("", "the system took the press away");
        sheet.History.CanUndo.Should().BeFalse();

        Press(sheet, CenterOf(4, 4));
        model.HandlePointer(PointerPhase.Move, CenterOf(6, 6), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1, PointerKind.Mouse).Should().BeFalse();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(4, 4), new CellRef(6, 6)));
        model.HandlePointer(PointerPhase.Move, CenterOf(8, 8), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeFalse("a cancelled drag follows the pointer no further");
    }

    [Fact]
    public void APressThatFindsAFillStillInFlight_DropsIt()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1, PointerKind.Mouse);

        // Its release never came: the next press selects, and its own release pours nothing.
        Press(sheet, CenterOf(5, 5));
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.Filling.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(5, 5), new CellRef(3, 1)));
    }

    [Fact]
    public void AFillMove_ReportsAChangeOnlyWhenThePreviewMoves()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1, PointerKind.Mouse);

        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, new Point(CenterOf(3, 1).X + 10, CenterOf(3, 1).Y), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeFalse("the same cell again is the same preview");
        // Back into the source the preview clears, which is a change; inside it, none before and none after
        // is none (the twin's spec makes the same moves, through C#'s lift of != over two nullable ranges).
        model.HandlePointer(PointerPhase.Move, CenterOf(1, 1), KeyModifiers.None, 1, PointerKind.Mouse).Should().BeTrue();
        sheet.FillTarget.Should().BeNull();
        model.HandlePointer(PointerPhase.Move, new Point(CenterOf(1, 1).X + 10, CenterOf(1, 1).Y), KeyModifiers.None, 1, PointerKind.Mouse)
            .Should().BeFalse("no preview before the move and none after it");
    }

    [Fact]
    public void APressJustOutsideTheGrab_SelectsInsteadOfFilling()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Selection = new SheetRange(new CellRef(1, 1));

        Press(sheet, new Point(2 * 96f + SheetController.FillHandleGrab + 1, 2 * 28f));

        sheet.Filling.Should().BeFalse();
        sheet.ActiveCell.Should().Be(new CellRef(2, 2));
    }

    // ---- the keyboard and the clipboard --------------------------------------------------------

    [Fact]
    public void TypedText_ReplacesTheCellThenAppends_UntilEnterCommits()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(0, 0), "old");
        ISheetSurfaceModel model = sheet;

        model.HandleText("4").Should().BeTrue();
        model.HandleText("2").Should().BeTrue();
        model.HandleText("").Should().BeFalse("nothing typed is nothing taken");
        sheet.Entry.Document.Text.Should().Be("42");

        model.HandleKey("Enter", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("42");
        sheet.ActiveCell.Should().Be(new CellRef(1, 0));
    }

    [Fact]
    public void WithAClipboard_TheCopyKeysAreClaimed_AndServedAsTsv()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(1, 1), "1");
        sheet.Document.SetCell(new CellRef(1, 2), "2");
        sheet.Document.SetCell(new CellRef(2, 1), "3");
        sheet.Document.SetCell(new CellRef(2, 2), "4");
        sheet.Selection = new SheetRange(new CellRef(1, 1), new CellRef(2, 2));
        var clipboard = new FakeClipboard();
        ISheetSurfaceModel model = sheet;

        model.HandleKey("c", KeyModifiers.Command, KeyboardConvention.Standard, clipboard).Should().BeTrue();
        clipboard.Held.Should().Be("1\t2\n3\t4");

        model.HandleKey("x", KeyModifiers.Command, KeyboardConvention.Standard, clipboard).Should().BeTrue();
        sheet.Document.GetCell(new CellRef(1, 1)).Should().Be("");

        sheet.Selection = new SheetRange(new CellRef(5, 5));
        model.HandleKey("v", KeyModifiers.Command, KeyboardConvention.Standard, clipboard).Should().BeTrue();
        sheet.Document.GetCell(new CellRef(6, 6)).Should().Be("4");
    }

    [Fact]
    public void ACut_WhoseClipboardRefusesTheText_EmptiesNothing()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(0, 0), "7");
        ISheetSurfaceModel model = sheet;

        FluentActions.Invoking(() => model.HandleKey("x", KeyModifiers.Command, KeyboardConvention.Standard, new RefusingClipboard()))
            .Should().Throw<IOException>();

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("7", "the copy reaches the clipboard before anything is emptied");
        sheet.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void WithoutAClipboard_TheCopyKeysAreLeftForThePlatformsEvents()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(0, 0), "7");
        ISheetSurfaceModel model = sheet;

        model.HandleKey("c", KeyModifiers.Command, KeyboardConvention.Standard, null).Should().BeFalse();
        model.HandleKey("x", KeyModifiers.Command, KeyboardConvention.Standard, null).Should().BeFalse();
        model.HandleKey("v", KeyModifiers.Command, KeyboardConvention.Standard, null).Should().BeFalse();

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("7", "a cut with nowhere to go takes nothing");
        model.CopyText().Should().Be("7", "the platform's copy event asks the model for the text");
        model.Cut().Should().Be("7");
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("");
        model.Paste("x\ty").Should().BeTrue();
        sheet.Document.GetCell(new CellRef(0, 1)).Should().Be("y");
    }

    [Fact]
    public void EscapeOutsideAnEdit_IsLeftToTheHost()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        ISheetSurfaceModel model = sheet;

        model.HandleKey("Escape", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeFalse();
        model.HandleText("a");
        model.HandleKey("Escape", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue("inside an edit it cancels the draft");
        sheet.Editing.Should().BeFalse();
    }
}
