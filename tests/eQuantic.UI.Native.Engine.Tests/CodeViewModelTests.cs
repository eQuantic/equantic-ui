using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The engine places, moves and deletes by what is DRAWN (defect 12 of docs/CODE-EDITOR-PLAN.md).
/// It counted one column per UTF-16 unit and one cell per column: a caret after a tab stood one cell
/// in while the tab ran to its stop, a click on a wide character missed it by half, and a Backspace
/// on an emoji removed one half of its surrogate pair.
/// </summary>
public class CodeViewModelTests
{
    private static CodeEditorController At(string text, int line, int column)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp)
        {
            Grid = new CodeGrid(Point.Zero, new Size(8, 18)),
        };
        editor.Selection = new CodeRange(new CodePosition(line, column));
        return editor;
    }

    [Fact]
    public void ACaretAfterATabStandsAtTheTabStop()
    {
        At("\tx", 0, 1).CaretRect(new CodePosition(0, 1)).X.Should().Be(32, "the tab runs four cells");
    }

    [Fact]
    public void ACaretAfterAWideCharacterStandsTwoCellsOn()
    {
        At("\u4E2Dx", 0, 1).CaretRect(new CodePosition(0, 1)).X.Should().Be(16);
    }

    /// <summary>The pair is found across what the caret steps over whole: the scan reads the
    /// text unit by unit, and a bracket is never inside a text element.</summary>
    [Fact]
    public void ABracketFindsItsPairAcrossTextElements()
    {
        const string text = "{\n  f(\"e\u0301\ud83d\ude00\u4E2D\");\n}";

        var forward = At(text, 0, 1).BracketAtCaret();
        forward!.Value.There.Should().Be(new CodePosition(2, 0));

        var backward = At(text, 2, 1).BracketAtCaret();
        backward!.Value.There.Should().Be(new CodePosition(0, 0));

        var inner = At(text, 1, 4).BracketAtCaret();
        inner!.Value.There.Should().Be(new CodePosition(1, 11), "past the accent, the emoji and the ideograph");
    }

    [Fact]
    public void BackspaceTakesAWholeEmoji()
    {
        var editor = At("a\U0001F600", 0, 3);

        editor.DeleteBackward();

        editor.Document.Text.Should().Be("a");
    }

    [Fact]
    public void DeleteTakesAWholeEmoji_AndALetterWithItsAccent()
    {
        var emoji = At("\U0001F600b", 0, 0);
        emoji.DeleteForward();
        emoji.Document.Text.Should().Be("b");

        var accented = At("e\u0301b", 0, 0);
        accented.DeleteForward();
        accented.Document.Text.Should().Be("b");
    }

    [Fact]
    public void ArrowsStepOverAWholeElement()
    {
        var editor = At("a\U0001F468\u200D\U0001F469\u200D\U0001F467b", 0, 1);

        editor.Move(CodeMotion.Character, CodeDirection.Forward, extend: false);
        editor.Caret.Column.Should().Be(9, "the family emoji is one step");
        editor.Move(CodeMotion.Character, CodeDirection.Backward, extend: false);
        editor.Caret.Column.Should().Be(1);
    }

    [Fact]
    public void AClickLandsOnTheNearerSideOfATab()
    {
        var editor = At("a\tb", 0, 0);

        editor.PositionAt(new Point(1.3f * 8, 9)).Column.Should().Be(1, "the left half of the tab is before it");
        editor.PositionAt(new Point(3.2f * 8, 9)).Column.Should().Be(2, "the right half is after it");
        editor.PositionAt(new Point(4.2f * 8, 9)).Column.Should().Be(2, "and b begins at the stop");
    }

    [Fact]
    public void ArrowDownThroughATabKeepsItsPlaceOnScreen()
    {
        var editor = At("\tx\n    y", 0, 1);

        editor.Move(CodeMotion.Line, CodeDirection.Forward, extend: false);

        editor.Caret.Should().Be(new CodePosition(1, 4), "the caret was four cells in, after the tab");
    }

    [Fact]
    public void TypingAnEmojiInsertsItWhole_AsOneStep()
    {
        var editor = At("ab", 0, 1);

        editor.HandleText("\U0001F600");

        editor.Document.Text.Should().Be("a\U0001F600b");
        editor.Caret.Column.Should().Be(3);
        editor.Undo();
        editor.Document.Text.Should().Be("ab");
    }

    [Fact]
    public void ASelectionOverAWideCharacterCoversItsTwoCells()
    {
        var editor = At("a\u4E2Db", 0, 0);
        editor.Selection = new CodeRange(new CodePosition(0, 1), new CodePosition(0, 2));

        var band = editor.SelectionBandsIn(0, editor.Document.LineCount - 1).Should().ContainSingle().Subject;
        band.X.Should().Be(8);
        band.Width.Should().Be(16);
    }

    /// <summary>
    /// A run of ↓ remembers the cell it aims at, and anything else that places the caret forgets it:
    /// a click, an undo, the app. Only an edit and a sideways move did, so ↓ after a click went back
    /// to the column the run before the click had aimed at.
    /// </summary>
    [Fact]
    public void AClickForgetsTheCellARunOfArrowsAimedAt()
    {
        var editor = At(string.Join("\n", Enumerable.Repeat("0123456789abc", 7)), 0, 10);
        editor.Move(CodeMotion.Line, CodeDirection.Forward);
        editor.Caret.Should().Be(new CodePosition(1, 10));

        editor.Selection = new CodeRange(new CodePosition(5, 2));
        editor.Move(CodeMotion.Line, CodeDirection.Forward);

        editor.Caret.Should().Be(new CodePosition(6, 2));
    }

    /// <summary>Tab runs to the next stop ON SCREEN. It counted UTF-16 columns, so after a tab or a
    /// wide character it stopped short of the stop or ran past it.</summary>
    [Theory]
    [InlineData("\tx", 2, 3)]        // the caret is at cell 5: three spaces to cell 8
    [InlineData("\u540D", 1, 2)]     // 名 is two cells: two spaces to cell 4
    public void TabRunsToTheNextStopOnScreen(string line, int column, int spaces)
    {
        var editor = At(line, 0, column);

        editor.Indent();

        editor.Document.Line(0).Should().Be(line.Insert(column, new string(' ', spaces)));
    }

    /// <summary>Backspace in an indent steps back to the previous stop on screen, over a tab too.</summary>
    [Fact]
    public void BackspaceInAnIndentStepsBackToTheStopOnScreen()
    {
        var editor = At("\t    x", 0, 5);   // a tab, then four spaces: the caret stands at cell 8

        editor.DeleteBackward();

        editor.Document.Line(0).Should().Be("\tx");
    }

    /// <summary>
    /// A word is made of text elements: an accent written as a mark after its letter belongs to the
    /// word the letter does. ⌥→ stopped between the e of a decomposed café and its accent, a column
    /// no caret should hold, and a double click selected the word without it.
    /// </summary>
    [Fact]
    public void AWordStepsOverWholeElements()
    {
        const string line = "cafe\u0301 x";   // café, the accent a mark of its own, then x
        var editor = At(line, 0, 0);

        editor.MoveTo(new CodePosition(0, 0), CodeMotion.Word, CodeDirection.Forward).Column.Should().Be(6);
        editor.MoveTo(new CodePosition(0, 6), CodeMotion.Word, CodeDirection.Backward).Column.Should().Be(0);

        editor.SelectWord(new CodePosition(0, 1));
        editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 0), new CodePosition(0, 5)));
    }

    /// <summary>
    /// A position INSIDE a text element is one no caret may hold: an edit from there splits the
    /// element. Set from outside (the app, an IDE's command), a caret at column 1 of an emoji stood
    /// between its halves, and a Backspace took the high one and left the low one behind. A caret goes
    /// to the element's start, as it is drawn, and a range grows to take in the elements it cuts.
    /// </summary>
    [Fact]
    public void APositionInsideAnElementIsTakenToItsBoundary()
    {
        var editor = At("\U0001F600xy", 0, 1);
        editor.Caret.Should().Be(new CodePosition(0, 0), "a caret stands where the element begins");

        editor.DeleteBackward();
        editor.Document.Text.Should().Be("\U0001F600xy", "there is nothing before the emoji to delete");

        editor.Selection = new CodeRange(new CodePosition(0, 3), new CodePosition(0, 1));
        editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 3), new CodePosition(0, 0)),
            "the range takes in the whole emoji, and keeps its direction");
    }
}
