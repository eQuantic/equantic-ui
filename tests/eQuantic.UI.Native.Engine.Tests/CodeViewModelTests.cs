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

        var band = editor.SelectionBands.Should().ContainSingle().Subject;
        band.X.Should().Be(8);
        band.Width.Should().Be(16);
    }
}
