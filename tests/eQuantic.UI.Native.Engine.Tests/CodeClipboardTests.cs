using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The CLIPBOARD the way an editor worth using treats it: with nothing selected, copy and cut take
/// the whole line, and pasting a line copied that way puts it back AS a line, above the caret's,
/// instead of splitting the line you are on in two. The clipboard carries text and nothing else, so
/// the model remembers its own whole-line copy to recognise it.
/// </summary>
public class CodeClipboardTests
{
    private static CodeEditorController At(string text, int line, int column)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(new CodePosition(line, column));
        return editor;
    }

    [Fact]
    public void CopyingWithNothingSelectedTakesTheWholeLine()
    {
        At("one\ntwo", 1, 1).CopyText().Should().Be("two\n");
    }

    [Fact]
    public void AWholeLineCopyPastesAsALineOfItsOwn_AboveTheCaret()
    {
        var editor = At("one\ntwo", 0, 1);
        var line = editor.CopyText();
        editor.Selection = new CodeRange(new CodePosition(1, 2));

        editor.Paste(line);

        editor.Document.Lines.Should().Equal("one", "one", "two");
        editor.Caret.Should().Be(new CodePosition(2, 2), "the caret stays where it was in its text");
    }

    [Fact]
    public void AnyOtherPasteGoesInAtTheCaret()
    {
        var editor = At("one", 0, 1);

        editor.Paste("x");

        editor.Document.Text.Should().Be("oxne");
    }

    [Fact]
    public void CuttingWithNothingSelectedTakesTheLine()
    {
        var editor = At("one\ntwo", 0, 1);

        editor.Cut().Should().Be("one\n");
        editor.Document.Text.Should().Be("two");
    }

    [Fact]
    public void EveryMoveOfTheCaretAsksToBeRevealed_AndNothingElseDoes()
    {
        var editor = At("one\ntwo", 0, 0);
        var start = editor.RevealVersion;

        editor.Selection = new CodeRange(new CodePosition(1, 1));
        editor.RevealVersion.Should().BeGreaterThan(start, "a moved caret is brought into view");

        var moved = editor.RevealVersion;
        editor.Type('x');
        editor.RevealVersion.Should().BeGreaterThan(moved, "so is one an edit moved");

        var typed = editor.RevealVersion;
        _ = editor.CopyText();
        editor.RevealVersion.Should().Be(typed, "a copy moves nothing, so it scrolls nothing");
    }

    /// <summary>
    /// Undo after a paste restores the document exactly, and redo replays it, whatever breaks the
    /// pasted lines (#600). The document breaks lines on CR, CRLF and LF alike, and the edit recorded
    /// the text as it was handed, which the history split on LF alone: pasting "x\ry" into "abc" after
    /// its first character gave "ax" and "ybc", and undo left "a" and "ybc".
    /// </summary>
    [Theory]
    [InlineData("x\ry")]
    [InlineData("\r")]
    [InlineData("x\r\ny")]
    [InlineData("x\ny")]
    [InlineData("x\r\r\ny\n")]
    public void UndoAfterAPaste_RestoresTheDocument_AndRedoReplaysIt(string pasted)
    {
        var editor = At("abc", 0, 1);
        editor.Paste(pasted).Should().BeTrue();
        var lines = editor.Document.Lines.ToList();
        lines.Should().Equal(CodeDocument.FromText("a" + pasted + "bc").Lines,
            "the paste breaks its lines where the document breaks them");

        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("abc", "undo takes back exactly what the paste put in");

        editor.Redo().Should().BeTrue();
        editor.Document.Lines.Should().Equal(lines, "and redo puts it back the same");
    }

    [Fact]
    public void AnEdit_CarriesTheTextTheDocumentHolds()
    {
        var editor = At("abc", 0, 1);
        CodeEdit? heard = null;
        editor.Changed += edit => heard = edit;

        editor.Paste("x\r\ny\rz");

        heard!.InsertedText.Should().Be("x\ny\nz", "a listener hears the lines the document holds");
        heard.InsertedRange.End.Should().Be(editor.Caret);
    }

    [Fact]
    public void AnEditsRange_BreaksItsLinesAsTheDocumentDoes()
    {
        var edit = new CodeEdit(new CodeRange(new CodePosition(0, 1)), "", "x\ry", default, default, false);

        edit.InsertedRange.End.Should().Be(new CodePosition(1, 1), "a lone CR is a break to the document too");
        edit.IsSimpleInsert.Should().BeFalse("and a break ends a run of typing");
    }
}
