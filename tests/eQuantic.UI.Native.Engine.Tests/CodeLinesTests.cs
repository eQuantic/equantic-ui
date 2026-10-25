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

    /// <summary>
    /// A move is the line it passes written on the block's other side and deleted where it was: two
    /// replacements, one undo step, and every replacement heard, through the move, its undo and its redo, in
    /// the order made, in one change once the step is made (#1031).
    /// </summary>
    [Fact]
    public void AMove_IsTwoReplacementsInOneStep_HeardAsOneChange()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 1));
        var heard = new List<string>();
        editor.Changed += change => heard.Add(
            string.Join(", ", change.Edits.Select(edit => $"{edit.Range.Start}-{edit.Range.End} {edit.RemovedText.Replace("\n", "|")}>{edit.InsertedText.Replace("\n", "|")}"))
            + $" {editor.Document.Text.Replace("\n", "|")}");

        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();
        editor.Document.Text.Should().Be("two\none\nthree");
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("one\ntwo\nthree");
        editor.Selection.Should().Be(new CodeRange(At(1, 1)));
        editor.Redo().Should().BeTrue();
        editor.Document.Text.Should().Be("two\none\nthree");
        editor.Selection.Should().Be(new CodeRange(At(0, 1)));

        heard.Should().Equal(
            "2:0-2:0 >one|, 0:0-1:0 one|> two|one|three",
            "0:0-0:0 >one|, 2:0-3:0 one|> one|two|three",
            "2:0-2:0 >one|, 0:0-1:0 one|> two|one|three");
    }

    /// <summary>
    /// A listener that edits while it hears a step's replacements corrupts none of them: the step is made
    /// whole before any is raised, so the listener's edit lands on the step's result and is a step of its
    /// own. Raised between them, an insertion at the top made the move's next replacement delete the
    /// listener's text, and the undo's next one delete the moved line (Copilot, #1058).
    /// </summary>
    [Fact]
    public void AListenerThatEditsWhileAStepIsHeard_CorruptsNoReplacement()
    {
        static void InsertOnce(CodeEditorController editor)
        {
            var done = false;
            editor.Changed += _ =>
            {
                if (done) return;
                done = true;
                editor.Apply(new CodeRange(At(0, 0)), "extra\n");
            };
        }

        var moved = Editor("one\ntwo\nthree", At(1, 1));
        InsertOnce(moved);
        Press(moved, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();
        moved.Document.Text.Should().Be("extra\ntwo\none\nthree");

        var undone = Editor("one\ntwo\nthree", At(1, 1));
        Press(undone, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();
        InsertOnce(undone);
        undone.Undo().Should().BeTrue();
        undone.Document.Text.Should().Be("extra\none\ntwo\nthree");
        undone.Undo().Should().BeTrue();
        undone.Document.Text.Should().Be("one\ntwo\nthree", "the listener's insertion is an undo step of its own");
    }

    /// <summary>
    /// Every listener hears the replacements in the order they were made, even when another one edits as it
    /// hears them: a problem, carried by the editor's own first listener, stays on its text. Raised at once,
    /// the insertion reached the problem before the move's second replacement, whose range was taken before
    /// it, and the problem went onto the inserted line (Copilot, #1058).
    /// </summary>
    [Fact]
    public void AListenerThatEdits_LeavesTheOthersHearingEveryReplacementInOrder()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 1));
        editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(At(1, 0), At(1, 3)), "two")]);
        var done = false;
        editor.Changed += _ =>
        {
            if (done) return;
            done = true;
            editor.Apply(new CodeRange(At(1, 0)), "extra\n");
        };

        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("two\nextra\none\nthree");
        editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(0, 0), At(0, 3)));
    }

    /// <summary>
    /// A problem reported while a step is heard, as a listener that checks the text on every change reports
    /// one, stands where it was reported: the step is heard as one change once it is made, so nothing of it
    /// is left to carry the report through. Heard a replacement at a time, the move's second replacement
    /// took a problem reported over the moved line during the first, against the text the move left, to the
    /// line's start (Copilot, #1058).
    /// </summary>
    [Fact]
    public void AProblemReportedWhileAStepIsHeard_StandsWhereItWasReported()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 1));
        var reported = false;
        editor.Changed += _ =>
        {
            if (reported) return;
            reported = true;
            editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(At(0, 0), At(0, 3)), "two")]);
        };

        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("two\none\nthree");
        editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(0, 0), At(0, 3)));
    }

    /// <summary>
    /// A problem reported after a listener edited while a change was heard stands where it was reported, by
    /// that listener or by one after it: it describes the text as it stands, the listener's edit included, and
    /// the problems follow every replacement the moment the document takes it, so the edit's own change,
    /// heard later, carries nothing through it again. Carried when the change was heard, the report went a
    /// line down, onto the text after it (Copilot, #1058).
    /// </summary>
    [Fact]
    public void AProblemReportedAfterAListenersEdit_StandsWhereItWasReported()
    {
        void Holds(bool byTheListenerThatEdited)
        {
            var editor = Editor("one\ntwo\nthree", At(1, 1));
            var problem = new CodeDiagnostic(new CodeRange(At(1, 0), At(1, 3)), "two");
            var edited = false;
            editor.Changed += _ =>
            {
                if (edited) return;
                edited = true;
                editor.Apply(new CodeRange(At(0, 0)), "extra\n");
                if (byTheListenerThatEdited) editor.Diagnostics.Report([problem]);
            };
            var reported = byTheListenerThatEdited;
            editor.Changed += _ =>
            {
                if (reported) return;
                reported = true;
                editor.Diagnostics.Report([problem]);
            };

            Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();

            editor.Document.Text.Should().Be("extra\ntwo\none\nthree");
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(1, 0), At(1, 3)),
                byTheListenerThatEdited ? "reported by the listener that edited" : "reported by a listener after it");
        }

        Holds(byTheListenerThatEdited: true);
        Holds(byTheListenerThatEdited: false);
    }

    /// <summary>
    /// A problem of no width at a moved line's end, where Roslyn reports a missing semicolon, stays on that
    /// line through the move, its undo and its redo: a move never writes text where the block's lines start
    /// or end. Written after the block's last line, the passed line took the problem with it (Copilot,
    /// #1058).
    /// </summary>
    [Fact]
    public void AProblemOfNoWidth_AtAMovedLinesEdge_StaysOnIt()
    {
        void Holds(string text, CodePosition caret, CodePosition problem, string key, CodePosition moved)
        {
            var editor = Editor(text, caret);
            editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(problem), "; expected")]);

            Press(editor, key, KeyModifiers.Alt).Should().BeTrue();
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(moved), $"{key} over {text}");
            editor.Undo().Should().BeTrue();
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(problem), $"undo of {key} over {text}");
            editor.Redo().Should().BeTrue();
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(moved), $"redo of {key} over {text}");
        }

        Holds("alpha\nbeta\ngamma", At(1, 0), At(1, 4), "ArrowUp", At(0, 4));
        Holds("alpha\nbeta\ngamma\ndelta", At(1, 0), At(1, 4), "ArrowDown", At(2, 4));
        Holds("alpha\nbeta\ngamma", At(1, 0), At(1, 0), "ArrowUp", At(0, 0));
        Holds("alpha\n\ngamma", At(1, 0), At(1, 0), "ArrowUp", At(0, 0));
        Holds("alpha\n\ngamma\ndelta", At(1, 0), At(1, 0), "ArrowDown", At(2, 0));
    }

    /// <summary>
    /// The replacements an undo and a redo raise carry the selection as it was before them and as they left
    /// it, the live one: a caret moved after the edit is the undo's selection before, and the restored one is
    /// every replacement's selection after (Copilot, #1058).
    /// </summary>
    [Fact]
    public void TheReplacementsHeard_CarryTheLiveSelectionOnEachSide()
    {
        var editor = Editor("one\ntwo\nthree", At(1, 1));
        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();
        editor.Selection = new CodeRange(At(2, 3));
        var heard = new List<CodeEdit>();
        editor.Changed += change => heard.AddRange(change.Edits);

        editor.Undo().Should().BeTrue();
        heard.Should().OnlyContain(edit => edit.SelectionBefore == new CodeRange(At(2, 3)) && edit.SelectionAfter == new CodeRange(At(1, 1)));

        heard.Clear();
        editor.Redo().Should().BeTrue();
        heard.Should().OnlyContain(edit => edit.SelectionBefore == new CodeRange(At(1, 1)) && edit.SelectionAfter == editor.Selection);
        editor.Selection.Should().Be(new CodeRange(At(0, 1)));
    }

    /// <summary>A problem on a moved line stays on its text, through the move, its undo and its redo, and
    /// covers nothing of the line it passed; one replacement over both took it to the replacement's ends
    /// (#1031).</summary>
    [Fact]
    public void AProblemOnAMovedLine_StaysOnItsText()
    {
        foreach (var down in new[] { false, true })
        {
            var editor = Editor("alpha\nbeta\ngamma", At(1, 0));
            editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(At(1, 1), At(1, 3)), "bad")]);

            Press(editor, "Arrow" + (down ? "Down" : "Up"), KeyModifiers.Alt).Should().BeTrue();
            var moved = down ? 2 : 0;
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(moved, 1), At(moved, 3)), "the problem moved with beta");

            editor.Undo().Should().BeTrue();
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(1, 1), At(1, 3)), "undo took it back");

            editor.Redo().Should().BeTrue();
            editor.Diagnostics.Items.Single().Range.Should().Be(new CodeRange(At(moved, 1), At(moved, 3)), "redo moved it again");
        }
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

    /// <summary>
    /// A join of many lines writes the joined line once. Built by appending each line to the text so far,
    /// every line copied all the lines before it, and collapsing the white space at the end copied them
    /// again: 12,000 lines of 80 characters copied some 5.8 billion on the input's own thread (Copilot,
    /// #1033). What it allocates is measured, which a shared machine's clock is not.
    /// </summary>
    [Fact]
    public void JoiningManyLines_WritesTheJoinedLineOnce()
    {
        var text = string.Join("\n", Enumerable.Range(0, 12_000).Select(i => $"    line {i} {new string('x', 64)}  "));
        var editor = Editor(text, At(0, 0), At(11_999, 4));

        var before = GC.GetAllocatedBytesForCurrentThread();
        editor.Execute(CodeCommand.JoinLines).Should().BeTrue();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        editor.Document.LineCount.Should().Be(1);
        allocated.Should().BeLessThan(text.Length * 2L * 100, "a join allocates in proportion to the text it joins");
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

    /// <summary>
    /// What every operation promises, over seeded documents and selections rather than hand-picked ones:
    /// its undo gives back the text and the selection it found, redo the text and the selection it left, the
    /// selection it leaves cuts no text element, a move with its opposite after it gives the text back, and
    /// the selection holds after a move, a copy or a join the text it held before, a join's white space
    /// aside. Copilot's two rounds on #1033 found five selections the hand-picked cases did not reach: these
    /// are the promises those cases broke, and the ends at column 0 that a person's ⇧↓ makes are drawn
    /// half the time.
    /// </summary>
    [Fact]
    public void EveryOperation_KeepsItsPromises_OverSeededDocumentsAndSelections()
    {
        string[] pool = ["", "a", "  b", "c  ", "\t{", "}", "x y", "\uD83D\uDE00z", "Bb", "aa  ", "e \t", "   "];
        var random = new Random(2026_10_24);
        var failures = new List<string>();
        for (var round = 0; round < 3000; round++)
        {
            var lines = Enumerable.Range(0, random.Next(1, 7)).Select(_ => pool[random.Next(pool.Length)]).ToList();
            var text = string.Join("\n", lines);
            CodePosition Somewhere()
            {
                var line = random.Next(lines.Count);
                return At(line, random.Next(2) == 0 ? 0 : random.Next(lines[line].Length + 1));
            }
            var anchor = Somewhere();
            var focus = random.Next(3) == 0 ? anchor : Somewhere();
            foreach (var command in Operations)
            {
                var editor = Editor(text, anchor, focus);
                var before = editor.Selection;
                var held = editor.Document.TextIn(before);
                var what = $"{command} over {System.Text.Json.JsonSerializer.Serialize(text)} from {before.Anchor}-{before.Focus}";
                if (!editor.Execute(command)) continue;
                var done = editor.Document.Text;
                var left = editor.Selection;
                var holds = editor.Document.TextIn(left);
                var moved = command is CodeCommand.MoveLinesUp or CodeCommand.MoveLinesDown or CodeCommand.CopyLinesUp
                    or CodeCommand.CopyLinesDown;
                if ((moved && holds != held)
                    || (command == CodeCommand.JoinLines && !before.IsEmpty && Blanked(holds) != Blanked(held)))
                    failures.Add($"{what}: holds {System.Text.Json.JsonSerializer.Serialize(holds)}, where it held {System.Text.Json.JsonSerializer.Serialize(held)}");
                editor.Selection = left;
                if (editor.Selection != left) failures.Add($"{what}: leaves {left.Anchor}-{left.Focus}, inside a text element");
                if (command is CodeCommand.MoveLinesUp or CodeCommand.MoveLinesDown)
                {
                    var back = command == CodeCommand.MoveLinesUp ? CodeCommand.MoveLinesDown : CodeCommand.MoveLinesUp;
                    if (!editor.Execute(back) || editor.Document.Text != text)
                        failures.Add($"{what}: {back} after it gives {System.Text.Json.JsonSerializer.Serialize(editor.Document.Text)}");
                    editor.Undo();
                }
                editor.Undo().Should().BeTrue(what);
                if (editor.Document.Text != text || editor.Selection != before)
                    failures.Add($"{what}: undo gives {System.Text.Json.JsonSerializer.Serialize(editor.Document.Text)} at {editor.Selection.Anchor}-{editor.Selection.Focus}");
                editor.Redo().Should().BeTrue(what);
                if (editor.Document.Text != done || editor.Selection != left)
                    failures.Add($"{what}: redo gives {System.Text.Json.JsonSerializer.Serialize(editor.Document.Text)} at {editor.Selection.Anchor}-{editor.Selection.Focus}");
            }
        }
        failures.Should().BeEmpty($"{failures.Count} broke, among them:\n{string.Join("\n", failures.Take(10))}");
    }

    /// <summary>Text with every run of spaces, tabs and line breaks one space, and none at its ends: what a
    /// join leaves of the text it joins.</summary>
    private static string Blanked(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "[ \\t\\n]+", " ").Trim();

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
