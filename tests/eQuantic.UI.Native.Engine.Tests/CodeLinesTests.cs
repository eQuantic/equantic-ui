using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's line operations (#1011), driven as a person drives them: the keys through
/// <see cref="CodeEditorController.HandleKey"/>, the commands from code, and undo after each, with no screen.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeLinesTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeLinesTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static CodeEditorController Editor(string text, CodePosition caret)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.Selection = new CodeRange(caret);
        return editor;
    }

    private static CodeEditorController Editor(string text, CodePosition anchor, CodePosition focus)
    {
        var editor = Editor(text, anchor);
        editor.Selection = new CodeRange(anchor, focus);
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers,
        KeyboardConvention convention = KeyboardConvention.Apple) =>
        editor.HandleKey(key, modifiers, convention, null);

    private static CodePosition At(int line, int column) => new(line, column);

    // ---- move -----------------------------------------------------------------------------------------

    [Fact]
    public void AltDown_SwapsTheLineWithTheNext_AndTheCaretGoesWithIt()
    {
        var editor = Editor("one\ntwo\nthree", At(0, 1));

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("two\none\nthree");
        editor.Caret.Should().Be(At(1, 1));
    }

    [Fact]
    public void AltUp_MovesTwoSelectedLines_AndTheSelectionWithThem()
    {
        var editor = Editor("a\nb\nc\nd", At(2, 0), At(3, 1));

        Press(editor, "ArrowUp", KeyModifiers.Alt, KeyboardConvention.Standard).Should().BeTrue();

        editor.Document.Text.Should().Be("a\nc\nd\nb");
        editor.Selection.Should().Be(new CodeRange(At(1, 0), At(2, 1)));
    }

    /// <summary>A selection that ends at a line's start leaves that line out, as Tab does.</summary>
    [Fact]
    public void ASelectionEndingAtALinesStart_LeavesThatLineOut()
    {
        var editor = Editor("a\nb\nc\nd\ne", At(1, 0), At(3, 0));

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("a\nd\nb\nc\ne");
        editor.Selection.Should().Be(new CodeRange(At(2, 0), At(4, 0)));
    }

    /// <summary>A move down is refused while the selection reaches the document's last line, its end at
    /// the start of that line included, as VS Code's <c>MoveLinesCommand</c> refuses it: there is no line
    /// after the block for that end to stand at once it moved, and an end at the start of an empty last line
    /// would leave that line out of the next move (Copilot, #1033).</summary>
    [Fact]
    public void AMoveDown_WhoseSelectionReachesTheLastLine_IsTheHosts()
    {
        foreach (var text in new[] { "a\nb\nc", "a\n\nc" })
        {
            var editor = Editor(text, At(0, 0), At(2, 0));

            editor.CanExecute(CodeCommand.MoveLinesDown).Should().BeFalse();
            Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeFalse();

            editor.Document.Text.Should().Be(text);
            editor.Selection.Should().Be(new CodeRange(At(0, 0), At(2, 0)));
        }
    }

    /// <summary>A block whose last line is empty goes down and comes back whole, the empty line with it
    /// (Copilot, #1033).</summary>
    [Fact]
    public void ABlockEndingOnABlankLine_GoesDownAndComesBackWhole()
    {
        var editor = Editor("a\n\nc\nd", At(0, 0), At(2, 0));

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();
        editor.Document.Text.Should().Be("c\na\n\nd");
        editor.Selection.Should().Be(new CodeRange(At(1, 0), At(3, 0)));

        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();
        editor.Document.Text.Should().Be("a\n\nc\nd");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(2, 0)));
    }

    [Fact]
    public void AMoveWithNoLineToPass_IsTheHosts()
    {
        var top = Editor("one\ntwo", At(0, 1));
        var bottom = Editor("one\ntwo", At(1, 1));

        top.CanExecute(CodeCommand.MoveLinesUp).Should().BeFalse();
        Press(top, "ArrowUp", KeyModifiers.Alt).Should().BeFalse();
        bottom.CanExecute(CodeCommand.MoveLinesDown).Should().BeFalse();
        Press(bottom, "ArrowDown", KeyModifiers.Alt).Should().BeFalse();

        top.Document.Text.Should().Be("one\ntwo");
        bottom.Document.Text.Should().Be("one\ntwo");
    }

    /// <summary>Undo puts back the selection the move found, and redo the one it left, where an edit's redo
    /// came back to the caret at the end of what it wrote.</summary>
    [Fact]
    public void UndoAndRedo_PutBackTheSelectionEachSideOfTheMove()
    {
        var editor = Editor("a\nb\nc", At(0, 0), At(0, 1));
        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();

        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("a\nb\nc");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(0, 1)));

        editor.Redo().Should().BeTrue();
        editor.Document.Text.Should().Be("b\na\nc");
        editor.Selection.Should().Be(new CodeRange(At(1, 0), At(1, 1)), "the moved line selected again");
    }

    /// <summary>While the signature panel shows overloads it keeps ⌥↑ ⌥↓, and the lines move once it is gone.</summary>
    [Fact]
    public void ThePanelsOverloads_KeepAltArrows_AndTheLinesMoveOtherwise()
    {
        var editor = Editor("x.\nnext", At(0, 2));
        editor.SignatureHelp.Providers.Add(CallSignatures.Overloaded());
        foreach (var c in "Add(") editor.HandleText(c.ToString());
        editor.SignatureHelp.IsOpen.Should().BeTrue();

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();
        editor.SignatureHelp.ActiveSignature.Should().Be(1, "the panel walked to its next overload");
        editor.Document.Text.Should().Be("x.Add()\nnext", "no line moved (the parenthesis closed itself)");

        editor.SignatureHelp.Dismiss();
        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();
        editor.Document.Text.Should().Be("next\nx.Add()");
    }

    // ---- copy -----------------------------------------------------------------------------------------

    [Fact]
    public void ShiftAltDown_CopiesTheLineBelow_AndTheCaretGoesToTheCopy()
    {
        var editor = Editor("one\ntwo", At(0, 1));

        Press(editor, "ArrowDown", KeyModifiers.Shift | KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("one\none\ntwo");
        editor.Caret.Should().Be(At(1, 1));
    }

    [Fact]
    public void ShiftAltUp_CopiesTheLinesAbove_AndTheSelectionStaysOnTheUpperCopy()
    {
        var editor = Editor("a\nb\nc", At(0, 0), At(1, 1));

        Press(editor, "ArrowUp", KeyModifiers.Shift | KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("a\nb\na\nb\nc");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(1, 1)));
    }

    // ---- delete ---------------------------------------------------------------------------------------

    [Fact]
    public void ShiftCommandK_DeletesTheLine_AndTheCaretKeepsItsColumn()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 2));

        Press(editor, "k", KeyModifiers.Shift | KeyModifiers.Command).Should().BeTrue();

        editor.Document.Text.Should().Be("one\nthree");
        editor.Caret.Should().Be(At(1, 2));
    }

    [Fact]
    public void DeletingTheLastLines_PutsTheCaretOnTheLineBefore()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 1), At(2, 2));

        Press(editor, "K", KeyModifiers.Shift | KeyModifiers.Command, KeyboardConvention.Standard).Should().BeTrue();

        editor.Document.Text.Should().Be("one");
        editor.Caret.Should().Be(At(0, 2), "the column clamped to the line before");
    }

    [Fact]
    public void DeletingTheOnlyLine_LeavesAnEmptyDocument_AndThenNothingToDelete()
    {
        var editor = Editor("solo", At(0, 2));

        editor.Execute(CodeCommand.DeleteLines).Should().BeTrue();

        editor.Document.Text.Should().Be("");
        editor.CanExecute(CodeCommand.DeleteLines).Should().BeFalse();
        Press(editor, "k", KeyModifiers.Shift | KeyModifiers.Command).Should().BeFalse();
    }

    // ---- join -----------------------------------------------------------------------------------------

    /// <summary>The next line's indent goes, one space joins them, and the caret stands before it.</summary>
    [Fact]
    public void ControlJ_JoinsTheLineWithTheNext_AsVSCodeDoes()
    {
        var editor = Editor("int a =\n    1;\nend", At(0, 3));

        Press(editor, "j", KeyModifiers.Control).Should().BeTrue();

        editor.Document.Text.Should().Be("int a = 1;\nend");
        editor.Caret.Should().Be(At(0, 7));
    }

    /// <summary>The browser reports Ctrl as the command key, so ⌘J joins on a Mac too.</summary>
    [Fact]
    public void CommandJ_JoinsOnAMac_AsCtrlArrivesInABrowser()
    {
        var editor = Editor("a\nb", At(0, 0));

        Press(editor, "j", KeyModifiers.Command).Should().BeTrue();

        editor.Document.Text.Should().Be("a b");
    }

    [Fact]
    public void TrailingWhiteSpace_CollapsesToOneSpace_AndAnEmptyLineAddsNothing()
    {
        var editor = Editor("a   \n   \n  b", At(0, 0), At(2, 3));

        editor.Execute(CodeCommand.JoinLines).Should().BeTrue();

        editor.Document.Text.Should().Be("a b");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(0, 3)), "the joined text selected");
    }

    /// <summary>VS Code's join takes every line the selection touches, the one it ends at the start of
    /// too, where a move or a delete leaves that line out.</summary>
    [Fact]
    public void AJoin_TakesTheLineTheSelectionEndsAtTheStartOf()
    {
        var editor = Editor("a\nb\nc\nd", At(0, 0), At(2, 0));

        editor.Execute(CodeCommand.JoinLines).Should().BeTrue();

        editor.Document.Text.Should().Be("a b c\nd");
    }

    /// <summary>Only spaces and tabs are the white space a join takes, as in VS Code: a no-break space is
    /// text, a string literal's own among it.</summary>
    [Fact]
    public void AJoin_KeepsANoBreakSpace_OnEitherSideOfTheBreak()
    {
        var editor = Editor("x = \"a\u00A0\n\u00A0  b\"", At(0, 0));

        editor.Execute(CodeCommand.JoinLines).Should().BeTrue();

        editor.Document.Text.Should().Be("x = \"a\u00A0 \u00A0  b\"");
    }

    /// <summary>The white space a join collapses at the end of the first line takes the selection's ends
    /// that stood in it to the one space left, the start of a selection over lines and both ends of one on
    /// that line, so typing never replaces text the selection did not hold (Copilot, #1033).</summary>
    [Fact]
    public void AJoin_TakesTheEndsInTheCollapsedWhiteSpaceToTheSpaceLeft()
    {
        var forward = Editor("ab        \ncdef", At(0, 8), At(1, 1));
        forward.Execute(CodeCommand.JoinLines).Should().BeTrue();
        forward.Document.Text.Should().Be("ab cdef");
        forward.Selection.Should().Be(new CodeRange(At(0, 3), At(0, 4)), "the c alone, as the selection held it");

        var backward = Editor("ab        \ncdef", At(1, 1), At(0, 8));
        backward.Execute(CodeCommand.JoinLines).Should().BeTrue();
        backward.Selection.Should().Be(new CodeRange(At(0, 4), At(0, 3)));

        var onTheLine = Editor("ab    \ncd", At(0, 3), At(0, 5));
        onTheLine.Execute(CodeCommand.JoinLines).Should().BeTrue();
        onTheLine.Document.Text.Should().Be("ab cd");
        onTheLine.Selection.Should().Be(new CodeRange(At(0, 3)), "the white space it held is gone");
    }

    /// <summary>A selection over lines keeps its end at the text it stood before, as VS Code's join keeps
    /// it, so typing replaces what was selected and nothing after it (Copilot, #1033).</summary>
    [Fact]
    public void AJoin_KeepsTheSelectionsEndAtItsText()
    {
        var editor = Editor("ab\ncdef", At(0, 1), At(1, 1));

        editor.Execute(CodeCommand.JoinLines).Should().BeTrue();

        editor.Document.Text.Should().Be("ab cdef");
        editor.Selection.Should().Be(new CodeRange(At(0, 1), At(0, 4)));

        var upward = Editor("ab\n    cdef\nx", At(1, 6), At(0, 1));
        upward.Execute(CodeCommand.JoinLines).Should().BeTrue();
        upward.Document.Text.Should().Be("ab cdef\nx");
        upward.Selection.Should().Be(new CodeRange(At(0, 5), At(0, 1)), "the direction kept, the indentation gone");

        var inTheIndentation = Editor("ab\n    cd", At(0, 1), At(1, 2));
        inTheIndentation.Execute(CodeCommand.JoinLines).Should().BeTrue();
        inTheIndentation.Selection.Should().Be(new CodeRange(At(0, 1), At(0, 3)), "an end in the indentation goes to the text");

        var aBlankLast = Editor("ab\n   ", At(0, 1), At(1, 2));
        aBlankLast.Execute(CodeCommand.JoinLines).Should().BeTrue();
        aBlankLast.Document.Text.Should().Be("ab");
        aBlankLast.Selection.Should().Be(new CodeRange(At(0, 1), At(0, 2)), "a blank last line adds nothing, and the end is the line's");
    }

    [Fact]
    public void JoiningTheLastLine_IsTheHosts_AndWindowsBindsNoJoin()
    {
        var last = Editor("a\nb", At(1, 0));
        var standard = Editor("a\nb", At(0, 0));

        last.CanExecute(CodeCommand.JoinLines).Should().BeFalse();
        Press(last, "j", KeyModifiers.Control).Should().BeFalse();
        Press(standard, "j", KeyModifiers.Command, KeyboardConvention.Standard).Should().BeFalse("Ctrl+J stays the browser's");
        standard.Document.Text.Should().Be("a\nb");
    }

    // ---- sort -----------------------------------------------------------------------------------------

    [Fact]
    public void SortingTheLines_IsOrdinalCaseAside_EitherWay()
    {
        var editor = Editor("cherry\napple\nBanana", At(0, 0), At(2, 6));

        editor.Execute(CodeCommand.SortLinesAscending).Should().BeTrue();
        editor.Document.Text.Should().Be("apple\nBanana\ncherry");
        editor.CanExecute(CodeCommand.SortLinesAscending).Should().BeFalse("they are in order");
        editor.Execute(CodeCommand.SortLinesAscending).Should().BeFalse();

        editor.Execute(CodeCommand.SortLinesDescending).Should().BeTrue();
        editor.Document.Text.Should().Be("cherry\nBanana\napple");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(2, 5)), "the selection kept, clamped to the line now last");
    }

    [Fact]
    public void OneLine_HasNothingToSort()
    {
        var editor = Editor("b\na", At(0, 0));

        editor.CanExecute(CodeCommand.SortLinesAscending).Should().BeFalse();
        editor.Execute(CodeCommand.SortLinesAscending).Should().BeFalse();
    }

    // ---- insert above and below ------------------------------------------------------------------------

    [Fact]
    public void CommandEnter_OpensALineBelowABlocksOpening_IndentedOneStepMore()
    {
        var editor = Editor("    if (x) {\n    }", At(0, 6));

        Press(editor, "Enter", KeyModifiers.Command).Should().BeTrue();

        editor.Document.Text.Should().Be("    if (x) {\n        \n    }");
        editor.Caret.Should().Be(At(1, 8));
    }

    [Fact]
    public void ShiftCommandEnter_OpensALineAbove_IndentedAsTheCaretsLine()
    {
        var editor = Editor("    a\nb", At(0, 5));

        Press(editor, "Enter", KeyModifiers.Shift | KeyModifiers.Command).Should().BeTrue();

        editor.Document.Text.Should().Be("    \n    a\nb");
        editor.Caret.Should().Be(At(0, 4));
    }

    // ---- every operation ----------------------------------------------------------------------------------

    private static readonly CodeCommand[] Operations =
    [
        CodeCommand.MoveLinesUp, CodeCommand.MoveLinesDown, CodeCommand.CopyLinesUp, CodeCommand.CopyLinesDown,
        CodeCommand.DeleteLines, CodeCommand.JoinLines, CodeCommand.SortLinesAscending,
        CodeCommand.SortLinesDescending, CodeCommand.InsertLineAbove, CodeCommand.InsertLineBelow,
    ];

    /// <summary>Each operation is ONE undo step, and what it says it can do it does.</summary>
    [Fact]
    public void EachOperation_IsOneUndoStep_AndRunsWhereItSaysItCan()
    {
        const string text = "delta\n  alpha\ncharlie\nbravo";
        foreach (var command in Operations)
        {
            var editor = Editor(text, At(1, 1), At(2, 3));
            var can = editor.CanExecute(command);

            editor.Execute(command).Should().Be(can, $"{command} runs where CanExecute says it can");
            if (!can) continue;
            editor.Document.Text.Should().NotBe(text, $"{command} changed the lines");
            editor.Undo().Should().BeTrue();
            editor.Document.Text.Should().Be(text, $"one undo takes {command} back");
            editor.Selection.Should().Be(new CodeRange(At(1, 1), At(2, 3)));
        }
    }

    /// <summary>The selection an operation leaves never stands inside a text element, as no selection
    /// placed any other way does: a deleted line's column goes to the edge of the element it would cut
    /// (Copilot, #1033).</summary>
    [Fact]
    public void ADeletedLinesColumn_NeverCutsATextElement()
    {
        var editor = Editor("ab\n\uD83D\uDE00x", At(0, 1));

        editor.Execute(CodeCommand.DeleteLines).Should().BeTrue();

        editor.Document.Text.Should().Be("\uD83D\uDE00x");
        editor.Caret.Should().Be(At(0, 0));
    }

    /// <summary>A sort keeps the selection's columns, and an end the sort puts inside a text element goes
    /// to its edge (Copilot, #1033).</summary>
    [Fact]
    public void ASortedSelection_NeverCutsATextElement()
    {
        var editor = Editor("ab\n\uD83D\uDE00x", At(0, 1), At(1, 2));

        editor.Execute(CodeCommand.SortLinesDescending).Should().BeTrue();

        editor.Document.Text.Should().Be("\uD83D\uDE00x\nab");
        editor.Selection.Should().Be(new CodeRange(At(0, 0), At(1, 2)), "the anchor's column 1 is inside the emoji now");
    }

    /// <summary>A composition comes out before a line operation, as before a paste: the operation works on
    /// the document the composition began over, and undo puts that document back.</summary>
    [Fact]
    public void AComposition_ComesOutBeforeALineOperation()
    {
        var editor = Editor("one\ntwo", At(1, 3));
        editor.SetComposition("ka").Should().BeTrue();

        editor.Execute(CodeCommand.MoveLinesUp).Should().BeTrue();

        editor.Document.Text.Should().Be("two\none");
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("one\ntwo");
    }

    [Fact]
    public void AReadOnlyEditor_RunsNone()
    {
        var editor = Editor("a\nb\nc", At(1, 0));
        editor.ReadOnly = true;

        foreach (var command in Operations)
        {
            editor.CanExecute(command).Should().BeFalse($"{command} in a read-only editor");
            editor.Execute(command).Should().BeFalse();
        }
        editor.Document.Text.Should().Be("a\nb\nc");
    }
}
