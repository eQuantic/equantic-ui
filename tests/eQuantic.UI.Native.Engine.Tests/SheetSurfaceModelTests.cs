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

    private static ISheetSurfaceModel Press(SheetController sheet, Point at,
        KeyModifiers modifiers = KeyModifiers.None, int clicks = 1)
    {
        ISheetSurfaceModel model = sheet;
        model.HandlePointer(PointerPhase.Down, at, modifiers, clicks);
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
        engine.GetReferencedAssemblies().Select(name => name.Name)
            .Should().Contain(vocabulary.GetName().Name, "the engine is written against the vocabulary")
            .And.NotContain(name => name!.StartsWith("eQuantic.UI.") && name != vocabulary.GetName().Name,
                "and against nothing else of the SDK");
    }

    // ---- the pointer ---------------------------------------------------------------------------

    [Fact]
    public void APress_InAScrolledWindow_LandsOnTheCellTheWindowDrawsThere()
    {
        var sheet = new SheetController(rows: 200, cols: 8) { Grid = new SheetGrid(Point.Zero, 40, 0) };

        Press(sheet, CenterOf(2, 1));

        sheet.Selection.Should().Be(new SheetRange(new CellRef(42, 1)));
        sheet.ActiveCell.Should().Be(new CellRef(42, 1));
    }

    [Fact]
    public void TheGridsOrigin_PlacesTheFirstCellPastWhatTheChildDrawsBeforeIt()
    {
        var origin = new Point(40, 24);
        var sheet = new SheetController(rows: 50, cols: 8) { Grid = new SheetGrid(origin, 0, 0) };

        sheet.CellAt(CenterOf(0, 0, origin)).Should().Be(new CellRef(0, 0));
        sheet.CellAt(new Point(39, 23)).Should().Be(new CellRef(0, 0), "a point before the first cell holds the edge");
        sheet.CellAt(new Point(100_000, 100_000)).Should().Be(new CellRef(49, 7), "and one past the last holds the far edge");
    }

    [Fact]
    public void ADrag_FromAPlainPress_MovesTheFarCorner_AndTheReleaseChangesNothing()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        var model = Press(sheet, CenterOf(1, 1));

        model.HandlePointer(PointerPhase.Move, CenterOf(3, 3), KeyModifiers.None, 1).Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 3), KeyModifiers.None, 1)
            .Should().BeFalse("the same cell again changes nothing");
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 3), KeyModifiers.None, 1).Should().BeFalse();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(3, 3)));
        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
        model.HandlePointer(PointerPhase.Move, CenterOf(5, 5), KeyModifiers.None, 1)
            .Should().BeFalse("once the press lifts, a move is a hover");
    }

    [Fact]
    public void APressWithShift_StretchesFromTheAnchor_AndStartsNoDrag()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        Press(sheet, CenterOf(1, 1));
        ((ISheetSurfaceModel)sheet).HandlePointer(PointerPhase.Up, CenterOf(1, 1), KeyModifiers.None, 1);

        var model = Press(sheet, CenterOf(4, 3), KeyModifiers.Shift);
        model.HandlePointer(PointerPhase.Move, CenterOf(6, 6), KeyModifiers.Shift, 1).Should().BeFalse();

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
        sheet.Draft.Should().Be("42");
        sheet.ActiveCell.Should().Be(new CellRef(2, 2));
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
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f - 5, 2 * 28f + 5), KeyModifiers.None, 1)
            .Should().BeTrue();
        sheet.Filling.Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1).Should().BeTrue();
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 1), KeyModifiers.None, 1).Should().BeTrue();

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

        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1);
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1);
        model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1).Should().BeTrue();

        sheet.Filling.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("", "the system took the press away");
        sheet.History.CanUndo.Should().BeFalse();

        Press(sheet, CenterOf(4, 4));
        model.HandlePointer(PointerPhase.Move, CenterOf(6, 6), KeyModifiers.None, 1);
        model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1).Should().BeFalse();
        sheet.Selection.Should().Be(new SheetRange(new CellRef(4, 4), new CellRef(6, 6)));
        model.HandlePointer(PointerPhase.Move, CenterOf(8, 8), KeyModifiers.None, 1)
            .Should().BeFalse("a cancelled drag follows the pointer no further");
    }

    [Fact]
    public void APressThatFindsAFillStillInFlight_DropsIt()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(1, 1), "7");
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1);

        // Its release never came: the next press selects, and its own release pours nothing.
        Press(sheet, CenterOf(5, 5));
        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1);
        model.HandlePointer(PointerPhase.Up, CenterOf(3, 1), KeyModifiers.None, 1);

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
        model.HandlePointer(PointerPhase.Down, new Point(2 * 96f, 2 * 28f), KeyModifiers.None, 1);

        model.HandlePointer(PointerPhase.Move, CenterOf(3, 1), KeyModifiers.None, 1).Should().BeTrue();
        model.HandlePointer(PointerPhase.Move, new Point(CenterOf(3, 1).X + 10, CenterOf(3, 1).Y), KeyModifiers.None, 1)
            .Should().BeFalse("the same cell again is the same preview");
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
        sheet.Draft.Should().Be("42");

        model.HandleKey("Enter", KeyModifiers.None, null).Should().BeTrue();
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

        model.HandleKey("c", KeyModifiers.Command, clipboard).Should().BeTrue();
        clipboard.Held.Should().Be("1\t2\n3\t4");

        model.HandleKey("x", KeyModifiers.Command, clipboard).Should().BeTrue();
        sheet.Document.GetCell(new CellRef(1, 1)).Should().Be("");

        sheet.Selection = new SheetRange(new CellRef(5, 5));
        model.HandleKey("v", KeyModifiers.Command, clipboard).Should().BeTrue();
        sheet.Document.GetCell(new CellRef(6, 6)).Should().Be("4");
    }

    [Fact]
    public void WithoutAClipboard_TheCopyKeysAreLeftForThePlatformsEvents()
    {
        var sheet = new SheetController(rows: 50, cols: 8);
        sheet.Document.SetCell(new CellRef(0, 0), "7");
        ISheetSurfaceModel model = sheet;

        model.HandleKey("c", KeyModifiers.Command, null).Should().BeFalse();
        model.HandleKey("x", KeyModifiers.Command, null).Should().BeFalse();
        model.HandleKey("v", KeyModifiers.Command, null).Should().BeFalse();

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

        model.HandleKey("Escape", KeyModifiers.None, null).Should().BeFalse();
        model.HandleText("a");
        model.HandleKey("Escape", KeyModifiers.None, null).Should().BeTrue("inside an edit it cancels the draft");
        sheet.Editing.Should().BeFalse();
    }
}
