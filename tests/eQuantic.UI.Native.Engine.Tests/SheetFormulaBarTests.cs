using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The formula bar's model (#983): the second view of the sheet's one entry. A press or text there edits
/// the active cell from the bar, in Edit mode, with the bar's own geometry; Enter, Tab and Escape do what
/// they do in the cell and hand the keyboard back to the grid; only the view that holds the caret draws it.
/// </summary>
public class SheetFormulaBarTests
{
    private static readonly CellRef B2 = new(1, 1);

    private static float Measure(string text) => text.Length * 10f;

    /// <summary>A sheet measured as the cells' type is, with B2 active, and a bar whose text begins 8 points
    /// in and 5 down, ten points a character.</summary>
    private static (SheetController Sheet, ICodeSurfaceModel Bar) Barred()
    {
        var sheet = new SheetController(200, 8);
        SheetScrolls.Place(sheet);
        sheet.Grid = sheet.Grid with { Measure = Measure, LineHeight = 20, CellPadding = EdgeInsets.Symmetric(6, 4) };
        sheet.Selection = new SheetRange(B2);
        sheet.FormulaBar.Grid = new CodeGrid(new Point(8, 5), new Size(4, 20), Measure: Measure);
        return (sheet, sheet.FormulaBar);
    }

    private static Point AfterCharacter(int column) => new(8 + column * 10 + 1, 10);

    [Fact]
    public void KeystrokeByKeystroke_TheBarAndTheCellReadOneDocument()
    {
        var (sheet, bar) = Barred();
        ISheetSurfaceModel grid = sheet;

        foreach (var (typed, shown) in new[] { ("a", "a"), ("b", "ab"), ("c", "abc") })
        {
            grid.HandleText(typed);
            sheet.Entry.Document.Text.Should().Be(shown);
            sheet.FormulaBar.HoldsTheCaret.Should().BeFalse("the grid typed, so the cell's editor holds the caret");
            bar.Carets.Should().BeEmpty();
            grid.Carets.Should().ContainSingle();
        }
    }

    [Fact]
    public void APressInTheBar_EditsTheActiveCellFromThere()
    {
        var (sheet, bar) = Barred();
        sheet.Document.SetCell(B2, "10");
        var focus = sheet.FocusVersion;

        bar.HandlePointer(PointerPhase.Down, AfterCharacter(2), KeyModifiers.None, 1);
        bar.HandlePointer(PointerPhase.Up, AfterCharacter(2), KeyModifiers.None, 1);

        sheet.Editing.Should().BeTrue();
        sheet.EditMode.Should().Be(SheetEditMode.Edit);
        sheet.EntryView.Should().Be(SheetEntryView.FormulaBar);
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 2));
        bar.Carets.Should().ContainSingle().Which.X.Should().Be(8 + 20, "placed on the bar's own grid");
        ((ISheetSurfaceModel)sheet).Carets.Should().BeEmpty("the cell's editor shows the text alone");

        bar.HandleText("0");
        bar.HandleKey("Enter", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.Document.GetCell(B2).Should().Be("100");
        sheet.ActiveCell.Should().Be(new CellRef(2, 1));
        sheet.FocusVersion.Should().BeGreaterThan(focus, "the grid takes the keyboard back");
    }

    [Fact]
    public void EscapeInTheBar_CancelsAndHandsTheKeyboardBack()
    {
        var (sheet, bar) = Barred();
        sheet.Document.SetCell(B2, "10");
        var focus = sheet.FocusVersion;

        bar.HandlePointer(PointerPhase.Down, AfterCharacter(2), KeyModifiers.None, 1);
        bar.HandlePointer(PointerPhase.Up, AfterCharacter(2), KeyModifiers.None, 1);
        bar.HandleText("5");
        bar.HandleKey("Escape", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.Document.GetCell(B2).Should().Be("10");
        sheet.Editing.Should().BeFalse();
        sheet.FocusVersion.Should().BeGreaterThan(focus);
    }

    [Fact]
    public void TheBar_IsAlwaysInEditMode()
    {
        var (sheet, bar) = Barred();
        ISheetSurfaceModel grid = sheet;
        grid.HandleText("7");
        grid.HandleText("8");
        sheet.EditMode.Should().Be(SheetEditMode.Enter);

        bar.HandlePointer(PointerPhase.Down, AfterCharacter(2), KeyModifiers.None, 1);
        bar.HandlePointer(PointerPhase.Up, AfterCharacter(2), KeyModifiers.None, 1);
        bar.HandleKey("ArrowLeft", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.Editing.Should().BeTrue("an arrow in the bar moves its caret");
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 1));
        sheet.Document.GetCell(B2).Should().BeEmpty();
    }

    [Fact]
    public void APressInTheCellsEditor_TakesTheCaretBack()
    {
        var (sheet, bar) = Barred();
        ISheetSurfaceModel grid = sheet;
        grid.HandleText("x");
        bar.HandlePointer(PointerPhase.Down, AfterCharacter(1), KeyModifiers.None, 1);
        bar.HandlePointer(PointerPhase.Up, AfterCharacter(1), KeyModifiers.None, 1);
        sheet.EntryView.Should().Be(SheetEntryView.FormulaBar);

        var inEditor = new Point(SheetScrolls.HeaderWidth + 96 + 30, SheetScrolls.HeaderHeight + 28 + 10);
        grid.HandlePointer(PointerPhase.Down, inEditor, KeyModifiers.None, 1, PointerKind.Mouse);
        grid.HandlePointer(PointerPhase.Up, inEditor, KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.EntryView.Should().Be(SheetEntryView.Cell);
        grid.Carets.Should().ContainSingle();
        bar.Carets.Should().BeEmpty();
        sheet.Entry.Document.Text.Should().Be("x");
    }

    [Fact]
    public void AtRest_TheBarsTabAndEscapeAreTheHosts()
    {
        var (sheet, bar) = Barred();

        bar.HandleKey("Tab", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeFalse();
        bar.HandleKey("Escape", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeFalse();
        sheet.Editing.Should().BeFalse();
    }

    [Fact]
    public void TextTypedIntoTheBarAtRest_GoesAfterTheValue()
    {
        var (sheet, bar) = Barred();
        sheet.Document.SetCell(B2, "10");

        bar.HandleText("0");

        sheet.Entry.Document.Text.Should().Be("100");
        sheet.EntryView.Should().Be(SheetEntryView.FormulaBar);
    }
}
