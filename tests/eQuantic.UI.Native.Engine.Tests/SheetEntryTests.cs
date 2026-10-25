using System.Globalization;
using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A cell's entry is the code engine's text document (#983, D15): a caret anywhere, a selection, word
/// moves, its own undo, Excel's Enter and Edit modes, the entry's keys, Backspace that edits, the editor
/// that grows over its neighbours, a press inside it, the clipboard while editing, compositions, and
/// Escape releasing Tab (#732). Asked of the engine with no host at all, through the surface's protocol.
/// </summary>
public class SheetEntryTests
{
    private static readonly CellRef B2 = new(1, 1);

    /// <summary>Ten points a character: enough to tell a caret placed by measure from one placed by a count.</summary>
    private static float Measure(string text) => text.Length * 10f;

    private const float Line = 20f;

    /// <summary>A sheet in a 900 × 586 viewport with the default 96 × 28 cells, measuring its cells' type, B2
    /// active.</summary>
    private static SheetController Sheet(int rows = 200, int cols = 30)
    {
        var sheet = new SheetController(rows, cols);
        SheetScrolls.Place(sheet);
        sheet.Grid = sheet.Grid with { Measure = Measure, LineHeight = Line, CellPadding = EdgeInsets.Symmetric(6, 4) };
        sheet.Selection = new SheetRange(B2);
        return sheet;
    }

    private static bool Key(ISheetSurfaceModel sheet, string key, KeyModifiers modifiers = KeyModifiers.None,
        KeyboardConvention convention = KeyboardConvention.Standard, ITextClipboard? clipboard = null) =>
        sheet.HandleKey(key, modifiers, convention, clipboard);

    private static void Type(ISheetSurfaceModel sheet, string text)
    {
        foreach (var c in text) sheet.HandleText(c.ToString());
    }

    /// <summary>B2's left edge and top in the surface's coordinates: behind the 44-point row numbers and the
    /// 26-point column headers, one default column and one default row in.</summary>
    private const float B2Left = SheetScrolls.HeaderWidth + 96f;
    private const float B2Top = SheetScrolls.HeaderHeight + 28f;

    private static string Text(SheetController sheet) => sheet.Entry.Document.Text;

    private sealed class FakeClipboard : ITextClipboard
    {
        public string? Held;
        public void Write(string text) => Held = text;
        public string? Read() => Held;
    }

    // ---- the entry is a text document ------------------------------------------------------------------

    [Fact]
    public void F2_ThenTheArrows_MoveTheCaretInsideTheCell()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "hello");

        Key(sheet, "F2");
        Key(sheet, "ArrowLeft");
        Key(sheet, "ArrowLeft");
        sheet.HandleText("X");

        Text(sheet).Should().Be("helXlo");
        sheet.EditMode.Should().Be(SheetEditMode.Edit);
        sheet.Document.GetCell(B2).Should().Be("hello", "the cell changes on commit");
    }

    [Fact]
    public void HomeAndAWordMove_SelectInsideTheEntry()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "one two three");
        Key(sheet, "F2");

        Key(sheet, "Home");
        Key(sheet, "ArrowRight", KeyModifiers.Command | KeyModifiers.Shift);

        sheet.Entry.Selection.Start.Should().Be(CodePosition.Start);
        var selected = sheet.Entry.Document.TextIn(sheet.Entry.Selection);
        selected.TrimEnd().Should().Be("one", "a word move with Shift selects the first word");
        sheet.HandleText("1");
        Text(sheet).Should().StartWith("1");
    }

    [Fact]
    public void UndoWhileEditing_IsTheEntrys_AndNeverReachesTheSheet()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "x");
        Key(sheet, "F2");
        Type(sheet, "yz");

        Key(sheet, "z", KeyModifiers.Command).Should().BeTrue();

        Text(sheet).Should().Be("x");
        sheet.Editing.Should().BeTrue();
        sheet.History.CanUndo.Should().BeFalse("nothing was committed");
        Key(sheet, "z", KeyModifiers.Command).Should().BeTrue("with nothing left to undo, the key is still the entry's");
        sheet.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void ACommit_IsOneStepOfTheSheetsUndo()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "x");
        Key(sheet, "F2");
        Type(sheet, "yz");
        Key(sheet, "Enter");

        sheet.Document.GetCell(B2).Should().Be("xyz");
        Key(sheet, "z", KeyModifiers.Command);
        sheet.Document.GetCell(B2).Should().Be("x");
    }

    [Fact]
    public void ACellsEntry_PairsNothing()
    {
        var sheet = Sheet();
        Type(sheet, "(\"[");

        Text(sheet).Should().Be("(\"[", "a cell keeps the character a person typed and adds none");
    }

    // ---- Enter mode and Edit mode ------------------------------------------------------------------------

    [Fact]
    public void AnArrowInEnterMode_CommitsAndMoves()
    {
        var sheet = Sheet();
        Type(sheet, "7");
        sheet.EditMode.Should().Be(SheetEditMode.Enter);

        Key(sheet, "ArrowRight");

        sheet.Document.GetCell(B2).Should().Be("7");
        sheet.ActiveCell.Should().Be(new CellRef(1, 2));
        sheet.Editing.Should().BeFalse();
    }

    [Fact]
    public void AnArrowInEditMode_MovesTheCaret()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "78");
        Key(sheet, "F2");

        Key(sheet, "ArrowLeft");

        sheet.Editing.Should().BeTrue();
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 1));
    }

    [Fact]
    public void F2_TogglesTheMode()
    {
        var sheet = Sheet();
        Type(sheet, "7");

        Key(sheet, "F2");
        Key(sheet, "ArrowLeft");

        sheet.Editing.Should().BeTrue();
        sheet.EditMode.Should().Be(SheetEditMode.Edit);
        sheet.Entry.Caret.Should().Be(CodePosition.Start);
    }

    // ---- the keys of an entry ----------------------------------------------------------------------------

    [Fact]
    public void AltEnter_BreaksTheLine_AndTheValueCopiesQuoted()
    {
        var sheet = Sheet();
        Type(sheet, "one");
        Key(sheet, "Enter", KeyModifiers.Alt);
        Type(sheet, "two");
        Key(sheet, "Enter");

        sheet.Document.GetCell(B2).Should().Be("one\ntwo");
        sheet.Selection = new SheetRange(B2);
        sheet.CopyText().Should().Be("\"one\ntwo\"");
    }

    [Fact]
    public void CommandEnter_FillsTheSelectionWithTheEntry_AsOneStep()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(3, 2), B2);
        sheet.ActiveCell.Should().Be(B2);

        Type(sheet, "x");
        Key(sheet, "Enter", KeyModifiers.Command);

        for (var row = 1; row <= 3; row++)
            for (var col = 1; col <= 2; col++)
                sheet.Document.GetCell(new CellRef(row, col)).Should().Be("x");
        sheet.Selection.Should().Be(new SheetRange(new CellRef(3, 2), B2));
        sheet.ActiveCell.Should().Be(B2);
        sheet.Editing.Should().BeFalse();

        sheet.Undo();
        sheet.Document.GetCell(new CellRef(3, 2)).Should().BeEmpty();
        sheet.Document.GetCell(B2).Should().BeEmpty();
    }

    [Theory]
    [InlineData("en-US", KeyboardConvention.Standard)]
    [InlineData("pt-BR", KeyboardConvention.Standard)]
    [InlineData("en-US", KeyboardConvention.Apple)]
    public void TheDateAndTheTimeChords_WriteThemInTheCulture(string name, KeyboardConvention convention)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            var culture = new CultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            var sheet = Sheet();
            var moment = new DateTime(2026, 10, 25, 15, 45, 0);
            sheet.Now = () => moment;
            var (date, time) = convention == KeyboardConvention.Apple
                ? (KeyModifiers.Control, KeyModifiers.Command)
                : (KeyModifiers.Command, KeyModifiers.Command | KeyModifiers.Shift);

            Key(sheet, ";", date, convention).Should().BeTrue();
            sheet.Editing.Should().BeTrue("at rest the date begins an entry");
            sheet.EditMode.Should().Be(SheetEditMode.Enter);
            sheet.HandleText(" ");
            Key(sheet, time == (KeyModifiers.Command | KeyModifiers.Shift) ? ":" : ";", time, convention).Should().BeTrue();

            Text(sheet).Should().Be(moment.ToString("d", culture) + " " + moment.ToString("t", culture));
            // What a person reads, whatever space the culture's data puts before the half of the day.
            var read = Text(sheet).Replace(' ', ' ');
            read.Should().Be(name == "pt-BR" ? "25/10/2026 15:45" : "10/25/2026 3:45 PM");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Escape_CancelsTheEntry()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "x");
        Type(sheet, "yz");

        Key(sheet, "Escape").Should().BeTrue();

        sheet.Document.GetCell(B2).Should().Be("x");
        sheet.Editing.Should().BeFalse();
        sheet.History.CanUndo.Should().BeFalse();
    }

    // ---- Backspace clears and edits; Delete clears ---------------------------------------------------------

    [Fact]
    public void Backspace_BeginsAnEmptyEntryOverTheActiveCellOnly()
    {
        var sheet = Sheet();
        foreach (var cell in new[] { B2, new CellRef(1, 2), new CellRef(2, 1), new CellRef(2, 2) })
            sheet.Document.SetCell(cell, "old");
        sheet.Selection = new SheetRange(new CellRef(2, 2), B2);

        Key(sheet, "Backspace").Should().BeTrue();
        sheet.Editing.Should().BeTrue();
        Text(sheet).Should().BeEmpty();
        Type(sheet, "new");
        Key(sheet, "Enter");

        sheet.Document.GetCell(B2).Should().Be("new");
        sheet.Document.GetCell(new CellRef(1, 2)).Should().Be("old");
        sheet.Document.GetCell(new CellRef(2, 1)).Should().Be("old");
        sheet.Document.GetCell(new CellRef(2, 2)).Should().Be("old");
    }

    [Fact]
    public void Backspace_ThenEscape_GivesTheValueBack()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "old");

        Key(sheet, "Backspace");
        Key(sheet, "Escape");

        sheet.Document.GetCell(B2).Should().Be("old");
    }

    [Fact]
    public void Delete_ClearsTheSelectionWithoutEditing()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "1");
        sheet.Document.SetCell(new CellRef(1, 2), "2");
        sheet.Selection = new SheetRange(B2, new CellRef(1, 2));

        Key(sheet, "Delete");

        sheet.Document.GetCell(B2).Should().BeEmpty();
        sheet.Document.GetCell(new CellRef(1, 2)).Should().BeEmpty();
        sheet.Editing.Should().BeFalse();
        sheet.Undo();
        sheet.Document.GetCell(B2).Should().Be("1");
        sheet.Document.GetCell(new CellRef(1, 2)).Should().Be("2");
    }

    // ---- the editor grows over its neighbours --------------------------------------------------------------

    [Fact]
    public void AnEntryWiderThanItsCell_CoversTheColumnsItNeeds()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(new CellRef(1, 2), "kept");

        Type(sheet, new string('w', 23));   // 230 points, and 12 of padding and the caret's 2

        sheet.EditorRange.Should().Be(new SheetRange(B2, new CellRef(1, 3)));
        sheet.Document.GetCell(new CellRef(1, 2)).Should().Be("kept", "the editor covers C2, and C2 keeps its value");
    }

    [Fact]
    public void AnEntryOfTwoLinesInAShortRow_CoversTheRowsItNeeds()
    {
        var sheet = Sheet();
        Type(sheet, "one");
        Key(sheet, "Enter", KeyModifiers.Alt);
        Type(sheet, "two");

        sheet.EditorRange.Should().Be(new SheetRange(B2, new CellRef(2, 1)));
        sheet.Carets.Should().ContainSingle().Which.Y.Should().Be(B2Top + 4 + Line, "the caret is on the second line");
    }

    [Fact]
    public void AnEntryWiderThanTheView_StopsAtItsEdge_AndSlidesToKeepTheCaret()
    {
        var sheet = Sheet();

        Type(sheet, new string('w', 200));

        var range = sheet.EditorRange;
        range.LeftCol.Should().Be(1);
        range.RightCol.Should().Be(8, "column I is the last the 900-point view shows any of");
        var bounds = sheet.EditorBounds;
        var caret = sheet.Carets.Should().ContainSingle().Subject;
        caret.X.Should().BeInRange(bounds.X, bounds.Right, "the text slid so the caret stays inside the editor");
        caret.X.Should().BeGreaterThan(bounds.Right - 20, "and the caret stands at its right edge");
    }

    // ---- a press while editing ------------------------------------------------------------------------------

    [Fact]
    public void APressInsideTheEditor_PlacesTheCaret_InEditMode()
    {
        var sheet = Sheet();
        Type(sheet, "hello world");
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, new Point(B2Left + 6 + 52, B2Top + 10), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, new Point(B2Left + 6 + 52, B2Top + 10), KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.Editing.Should().BeTrue();
        sheet.EditMode.Should().Be(SheetEditMode.Edit);
        Text(sheet).Should().Be("hello world");
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 5), "52 points in is nearer the end of `hello` than of its space");
    }

    [Fact]
    public void APressOutsideTheEditor_CommitsAndSelects()
    {
        var sheet = Sheet();
        Type(sheet, "7");
        ISheetSurfaceModel model = sheet;

        var d5 = new Point(SheetScrolls.HeaderWidth + 3 * 96 + 48, SheetScrolls.HeaderHeight + 4 * 28 + 14);
        model.HandlePointer(PointerPhase.Down, d5, KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, d5, KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.Document.GetCell(B2).Should().Be("7");
        sheet.ActiveCell.Should().Be(new CellRef(4, 3));
    }

    [Fact]
    public void ADoubleClick_EditsWithTheCaretWhereItLanded()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "4200");
        ISheetSurfaceModel model = sheet;
        var between = new Point(B2Left + 6 + 21, B2Top + 10);

        model.HandlePointer(PointerPhase.Down, between, KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, between, KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Down, between, KeyModifiers.None, 2, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, between, KeyModifiers.None, 2, PointerKind.Mouse);

        sheet.Editing.Should().BeTrue();
        sheet.EditMode.Should().Be(SheetEditMode.Edit);
        Text(sheet).Should().Be("4200");
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 2));
    }

    [Fact]
    public void ADragInsideTheEditor_SelectsTheEntrysText()
    {
        var sheet = Sheet();
        Type(sheet, "hello world");
        ISheetSurfaceModel model = sheet;

        model.HandlePointer(PointerPhase.Down, new Point(B2Left + 6 + 61, B2Top + 10), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Move, new Point(B2Left + 6 + 109, B2Top + 10), KeyModifiers.None, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, new Point(B2Left + 6 + 109, B2Top + 10), KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.Entry.Document.TextIn(sheet.Entry.Selection).Should().Be("world");
        sheet.Selection.Should().Be(new SheetRange(B2), "the grid's own selection did not move");
    }

    [Fact]
    public void ThePointerOverTheEditor_IsATextBeam()
    {
        var sheet = Sheet();
        ISheetSurfaceModel model = sheet;
        var inside = new Point(B2Left + 30, B2Top + 10);
        model.CursorAt(inside).Should().Be(PointerCursor.Default);

        Type(sheet, "x");

        model.CursorAt(inside).Should().Be(PointerCursor.Text);
    }

    // ---- the clipboard while editing --------------------------------------------------------------------------

    [Fact]
    public void ACopyWhileEditing_TakesTheEntrysSelection()
    {
        var sheet = Sheet();
        Type(sheet, "hello world");
        sheet.Entry.Selection = new CodeRange(new CodePosition(0, 6), new CodePosition(0, 11));
        var clipboard = new FakeClipboard();

        Key(sheet, "c", KeyModifiers.Command, clipboard: clipboard).Should().BeTrue();

        clipboard.Held.Should().Be("world");
        sheet.Editing.Should().BeTrue();
        sheet.CopyText().Should().Be("world", "the browser's copy event reads the same");
    }

    [Fact]
    public void APasteWhileEditing_GoesInAtTheCaret_AsOneStepOfTheEntry()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "ab");
        Key(sheet, "F2");
        Key(sheet, "ArrowLeft");
        var clipboard = new FakeClipboard { Held = "x\ty" };

        Key(sheet, "v", KeyModifiers.Command, clipboard: clipboard).Should().BeTrue();

        Text(sheet).Should().Be("ax\tyb");
        sheet.Document.GetCell(new CellRef(1, 2)).Should().BeEmpty("no other cell changed");
        Key(sheet, "z", KeyModifiers.Command);
        Text(sheet).Should().Be("ab");
    }

    // ---- compositions and emoji ---------------------------------------------------------------------------------

    [Fact]
    public void ACompositionAtRest_BeginsAnEntry_AndCommitsAsOneEdit()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(B2, "old");
        ISheetSurfaceModel model = sheet;

        model.SetComposition("にほんご").Should().BeTrue();
        sheet.Editing.Should().BeTrue();
        Text(sheet).Should().Be("にほんご", "the composition shows in the cell's editor as it grows");
        sheet.Entry.Composition.Should().NotBeNull();

        model.HandleText("日本語");

        Text(sheet).Should().Be("日本語");
        sheet.Entry.Composition.Should().BeNull();
        Key(sheet, "z", KeyModifiers.Command);
        Text(sheet).Should().BeEmpty("one undo takes the committed word back");
    }

    [Fact]
    public void ACancelledComposition_LeavesTheEntryAsItWas()
    {
        var sheet = Sheet();
        Type(sheet, "ab");
        ISheetSurfaceModel model = sheet;

        model.SetComposition("x");
        model.SetComposition("");

        Text(sheet).Should().Be("ab");
        sheet.Entry.Caret.Should().Be(new CodePosition(0, 2));
    }

    [Fact]
    public void AnEmoji_IsOneCharacter()
    {
        var sheet = Sheet();
        ISheetSurfaceModel model = sheet;

        model.HandleText("\U0001F44D\U0001F3FD");
        Text(sheet).Should().Be("\U0001F44D\U0001F3FD");
        Key(sheet, "Backspace");
        Text(sheet).Should().BeEmpty("one Backspace takes the whole thumbs-up with its skin tone");

        model.HandleText("\U0001F44D\U0001F3FD");
        Key(sheet, "Enter");
        sheet.Document.GetCell(B2).Should().Be("\U0001F44D\U0001F3FD");
    }

    [Fact]
    public void TheInput_StandsAtTheActiveCell_ThenAtTheCaret()
    {
        var sheet = Sheet();
        ISheetSurfaceModel model = sheet;

        model.Carets.Should().BeEmpty();
        model.InputBounds.Should().Be(new Rect(B2Left, B2Top, 96, 28));

        Type(sheet, "abc");

        var caret = model.Carets.Should().ContainSingle().Subject;
        caret.X.Should().Be(B2Left + 6 + 30);
        caret.Y.Should().Be(B2Top + 4);
        model.InputBounds.Should().Be(caret);
    }

    [Fact]
    public void LosingTheKeyboard_CancelsAComposition_AndKeepsTheEntry()
    {
        var sheet = Sheet();
        ISheetSurfaceModel model = sheet;
        Type(sheet, "ab");
        model.SetComposition("x");

        model.FocusChanged(false);

        sheet.Editing.Should().BeTrue();
        Text(sheet).Should().Be("ab");
    }

    // ---- Escape releases Tab -----------------------------------------------------------------------------------

    [Fact]
    public void EscapeAtRest_LeavesTheNextTabToTheHost()
    {
        var sheet = Sheet();

        Key(sheet, "Escape").Should().BeFalse("nothing to cancel: the key is the host's");
        Key(sheet, "Tab").Should().BeFalse("the Tab after it leaves the sheet");
        Key(sheet, "Tab", KeyModifiers.Shift).Should().BeFalse("either way");

        sheet.ActiveCell.Should().Be(B2);
    }

    [Fact]
    public void AnotherKey_TakesTabBack()
    {
        var sheet = Sheet();

        Key(sheet, "Escape");
        Key(sheet, "ArrowRight");
        Key(sheet, "Tab").Should().BeTrue();

        sheet.ActiveCell.Should().Be(new CellRef(1, 3));
    }

    [Fact]
    public void AModifierAlone_DoesNotTakeTabBack()
    {
        var sheet = Sheet();

        Key(sheet, "Escape");
        Key(sheet, "Shift", KeyModifiers.Shift);

        Key(sheet, "Tab", KeyModifiers.Shift).Should().BeFalse();
    }

    [Fact]
    public void EscapeWhileEditing_CancelsAndKeepsTheHold()
    {
        var sheet = Sheet();
        Type(sheet, "x");

        Key(sheet, "Escape").Should().BeTrue();
        Key(sheet, "Tab").Should().BeTrue();

        sheet.Editing.Should().BeFalse();
        sheet.ActiveCell.Should().Be(new CellRef(1, 2));
    }

    [Fact]
    public void ComingBack_TakesTabBack()
    {
        var sheet = Sheet();
        ISheetSurfaceModel model = sheet;

        Key(sheet, "Escape");
        model.FocusChanged(true);

        Key(sheet, "Tab").Should().BeTrue();
    }
}
