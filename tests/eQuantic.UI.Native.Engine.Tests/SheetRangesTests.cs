using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Several ranges at once (#984): ⌘ or Ctrl adds a cell or a range, add mode adds without the modifier,
/// every plain move leaves one range, and the commands read the ranges as Excel does: copy, cut, paste
/// and fill the active one, Delete and Ctrl+Enter every one, as one undo step.
/// </summary>
public class SheetRangesTests
{
    private static readonly CellRef B2 = new(1, 1);

    // Default cells are 96×28, and a grid with no viewport has no headers in front of them.
    private static Point CenterOf(int row, int col) => new(col * 96f + 48f, row * 28f + 14f);

    private static SheetRange Range(string from, string to) =>
        new(Cell(from), Cell(to));

    private static SheetRange Range(string cell) => new(Cell(cell));

    private static CellRef Cell(string a1)
    {
        SheetReference.TryParse(a1, 1000, 26, out var range).Should().BeTrue();
        return range.TopLeft;
    }

    private static void Press(SheetController sheet, string a1, KeyModifiers modifiers = KeyModifiers.None)
    {
        ISheetSurfaceModel model = sheet;
        var cell = Cell(a1);
        model.HandlePointer(PointerPhase.Down, CenterOf(cell.Row, cell.Col), modifiers, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, CenterOf(cell.Row, cell.Col), modifiers, 1, PointerKind.Mouse);
    }

    private static bool Key(SheetController sheet, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        ((ISheetSurfaceModel)sheet).HandleKey(key, modifiers, KeyboardConvention.Standard, null);

    [Fact]
    public void APressWithCommand_AddsACell()
    {
        var sheet = new SheetController { Selection = Range("B2", "C3") };

        Press(sheet, "E5", KeyModifiers.Command);

        sheet.Ranges.Should().Equal(Range("B2", "C3"), Range("E5"));
        sheet.Selection.Should().Be(Range("E5"));
        sheet.ActiveCell.Should().Be(Cell("E5"));
    }

    [Fact]
    public void ADragWithCommand_AddsARange()
    {
        var sheet = new SheetController { Selection = Range("B2") };
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, CenterOf(3, 3), KeyModifiers.Command, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Move, CenterOf(5, 4), KeyModifiers.Command, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, CenterOf(5, 4), KeyModifiers.Command, 1, PointerKind.Mouse);

        sheet.Ranges.Should().Equal(Range("B2"), Range("D4", "E6"));
        sheet.ActiveCell.Should().Be(Cell("E6"));
    }

    [Fact]
    public void AddMode_AddsWithoutTheModifier_UntilEscape()
    {
        var sheet = new SheetController { Selection = Range("B2") };

        Key(sheet, "F8", KeyModifiers.Shift).Should().BeTrue();
        sheet.AddingRanges.Should().BeTrue();
        Press(sheet, "D4");
        sheet.Ranges.Should().Equal(Range("B2"), Range("D4"));

        Key(sheet, "Escape").Should().BeTrue("Escape leaves add mode first");
        sheet.AddingRanges.Should().BeFalse();
        sheet.TabReleased.Should().BeFalse("the Escape that left add mode released nothing");
        Press(sheet, "F6");
        sheet.Ranges.Should().Equal(Range("F6"));
    }

    [Fact]
    public void ShiftF8_LeavesAddModeToo()
    {
        var sheet = new SheetController();

        Key(sheet, "F8", KeyModifiers.Shift);
        Key(sheet, "F8", KeyModifiers.Shift);

        sheet.AddingRanges.Should().BeFalse();
    }

    [Fact]
    public void APlainMove_LeavesOneRange_AndShiftStretchesTheActiveOne()
    {
        var sheet = new SheetController { Selection = Range("B2") };
        Press(sheet, "D4", KeyModifiers.Command);

        Key(sheet, "ArrowDown", KeyModifiers.Shift);
        sheet.Ranges.Should().Equal(Range("B2"), Range("D4", "D5"));

        Key(sheet, "ArrowDown");
        sheet.Ranges.Should().Equal(Range("D6"));
    }

    [Fact]
    public void Delete_ClearsEveryRange_AsOneUndoStep()
    {
        var sheet = new SheetController();
        foreach (var a1 in new[] { "B2", "D4", "D5" }) sheet.Document.SetCell(Cell(a1), "x");
        sheet.Selection = Range("B2");
        Press(sheet, "D4", KeyModifiers.Command);
        ((ISheetSurfaceModel)sheet).HandlePointer(PointerPhase.Down, CenterOf(4, 3), KeyModifiers.Shift, 1,
            PointerKind.Mouse);

        Key(sheet, "Delete");

        foreach (var a1 in new[] { "B2", "D4", "D5" }) sheet.Document.GetCell(Cell(a1)).Should().BeEmpty();
        sheet.Undo().Should().BeTrue();
        foreach (var a1 in new[] { "B2", "D4", "D5" }) sheet.Document.GetCell(Cell(a1)).Should().Be("x");
    }

    [Fact]
    public void ACellTwoRangesShare_IsClearedOnce()
    {
        var sheet = new SheetController();
        sheet.Document.SetCell(Cell("B3"), "x");
        sheet.Selection = Range("B2", "B3");
        Press(sheet, "B3", KeyModifiers.Command);

        sheet.ClearSelection().Should().BeTrue();
        sheet.Undo();

        sheet.Document.GetCell(Cell("B3")).Should().Be("x", "undo puts back what the one clearing took");
    }

    [Fact]
    public void Copy_TakesTheActiveRange()
    {
        var sheet = new SheetController();
        sheet.Document.SetCell(B2, "1");
        sheet.Document.SetCell(Cell("D4"), "2");
        sheet.Selection = Range("B2");
        Press(sheet, "D4", KeyModifiers.Command);

        sheet.CopyText().Should().Be("2");
    }

    [Fact]
    public void CtrlEnter_FillsEveryRange_AsOneUndoStep()
    {
        var sheet = new SheetController();
        sheet.Selection = Range("B2");
        Press(sheet, "D4", KeyModifiers.Command);
        ((ISheetSurfaceModel)sheet).HandlePointer(PointerPhase.Down, CenterOf(4, 3), KeyModifiers.Shift, 1,
            PointerKind.Mouse);

        sheet.HandleText("x");
        Key(sheet, "Enter", KeyModifiers.Command);

        foreach (var a1 in new[] { "B2", "D4", "D5" }) sheet.Document.GetCell(Cell(a1)).Should().Be("x");
        sheet.Undo();
        foreach (var a1 in new[] { "B2", "D4", "D5" }) sheet.Document.GetCell(Cell(a1)).Should().BeEmpty();
    }

    [Fact]
    public void GoToAndAPaste_LeaveOneRange()
    {
        var sheet = new SheetController { Selection = Range("B2") };
        Press(sheet, "D4", KeyModifiers.Command);

        sheet.GoTo("F6");
        sheet.Ranges.Should().Equal(Range("F6"));

        Press(sheet, "B2", KeyModifiers.Command);
        sheet.Paste("1\t2");
        sheet.Ranges.Should().Equal(Range("B2", "C2"));
    }

    // ---- the document's version -----------------------------------------------------------------------

    [Fact]
    public void TheDocumentsVersion_CountsEveryChangeOfAValue()
    {
        var document = new SheetDocument(100, 10);
        var start = document.Version;

        document.SetCell(B2, "7");
        document.Version.Should().Be(start + 1);
        document.SetCell(B2, "7");
        document.Version.Should().Be(start + 1, "a write that leaves the value counts nothing");
        document.SetCell(B2, "");
        document.Version.Should().Be(start + 2);
    }

    [Fact]
    public void TheDocumentsVersion_CountsStructure_AndUndo()
    {
        var sheet = new SheetController(100, 10);
        var start = sheet.Document.Version;

        sheet.Insert(SheetAxis.Rows, 2);
        sheet.Document.Version.Should().BeGreaterThan(start);
        var inserted = sheet.Document.Version;
        sheet.SetCell(B2, "7");
        var set = sheet.Document.Version;
        set.Should().BeGreaterThan(inserted);

        sheet.Undo();
        sheet.Document.Version.Should().BeGreaterThan(set, "an undo changes a value too");
    }
}
