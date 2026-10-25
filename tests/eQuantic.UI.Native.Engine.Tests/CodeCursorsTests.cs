using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's several carets (#1012), driven as a person drives them, with no screen: the carets placed,
/// every command made at each of them as one undo step, and the carets that meet made one.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeCursorsTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeCursorsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static CodeEditorController Editor(string text, params CodeRange[] selections)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.Selections = selections;
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None,
        KeyboardConvention convention = KeyboardConvention.Apple) =>
        editor.HandleKey(key, modifiers, convention, null);

    private static CodePosition At(int line, int column) => new(line, column);

    private static CodeRange Caret(int line, int column) => new(At(line, column));

    // ---- the carets -----------------------------------------------------------------------------------

    [Fact]
    public void Selections_AreThePrimaryFirst_AndTheOthersInDocumentOrder()
    {
        var editor = Editor("one\ntwo\nthree", Caret(2, 0), Caret(0, 0), Caret(1, 0));

        editor.Selections.Should().Equal(Caret(2, 0), Caret(0, 0), Caret(1, 0));
        editor.Selection.Should().Be(Caret(2, 0));
    }

    [Fact]
    public void SettingTheSelection_LeavesThatSelectionAlone()
    {
        var editor = Editor("one\ntwo\nthree", Caret(0, 0), Caret(1, 0), Caret(2, 0));
        var heard = 0;
        editor.SelectionChanged += _ => heard++;

        editor.Selection = Caret(0, 0);

        editor.Selections.Should().Equal(Caret(0, 0));
        heard.Should().Be(1, "the carets that went are a change of the selection, though the primary stayed");
    }

    [Fact]
    public void CaretsThatMeet_AreMadeOne_ThePrimaryKept()
    {
        var editor = Editor("abc\nabc\nabc", Caret(2, 3), Caret(1, 3));

        Press(editor, "ArrowUp");
        editor.Selections.Should().Equal(Caret(1, 3), Caret(0, 3));

        Press(editor, "ArrowUp");
        editor.Selections.Should().Equal(Caret(0, 3));
    }

    [Fact]
    public void TwoSelectionsThatOnlyTouch_StayTwo_AndACaretOnAnEdgeIsMadeOneWithIt()
    {
        var touching = Editor("abcdef", new CodeRange(At(0, 0), At(0, 3)), new CodeRange(At(0, 3), At(0, 6)));
        touching.Selections.Should().HaveCount(2);

        var caret = Editor("abcdef", new CodeRange(At(0, 0), At(0, 3)), Caret(0, 3));
        caret.Selections.Should().Equal(new CodeRange(At(0, 0), At(0, 3)));
    }

    [Fact]
    public void EachCaret_KeepsTheCellItAimsAt_DownThroughAShortLine()
    {
        var editor = Editor("abcdef\nab\nabcdef\nabcdef", Caret(0, 5), Caret(2, 1));

        Press(editor, "ArrowDown");
        editor.Selections.Should().Equal(Caret(1, 2), Caret(3, 1));

        Press(editor, "ArrowUp");
        editor.Selections.Should().Equal(Caret(0, 5), Caret(2, 1));
    }

    // ---- every command at every caret ------------------------------------------------------------------

    [Fact]
    public void TypingAtThreeCarets_IsOneUndoStep_ThatPutsBackEveryCaret()
    {
        var editor = Editor("one\ntwo\nthree", Caret(1, 0), Caret(0, 0), Caret(2, 0));

        editor.HandleText("x").Should().BeTrue();
        editor.HandleText("y").Should().BeTrue();

        editor.Document.Text.Should().Be("xyone\nxytwo\nxythree");
        editor.Selections.Should().Equal(Caret(1, 2), Caret(0, 2), Caret(2, 2));

        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("one\ntwo\nthree", "one undo takes back the six characters");
        editor.Selections.Should().Equal(Caret(1, 0), Caret(0, 0), Caret(2, 0));

        editor.Redo().Should().BeTrue();
        editor.Document.Text.Should().Be("xyone\nxytwo\nxythree");
        editor.Selections.Should().Equal(Caret(1, 2), Caret(0, 2), Caret(2, 2));
    }

    [Fact]
    public void TypingAtTwoCaretsOnOneLine_WritesAtEach()
    {
        var editor = Editor("abcd", Caret(0, 1), Caret(0, 3));

        editor.HandleText("x");

        editor.Document.Text.Should().Be("axbcxd");
        editor.Selections.Should().Equal(Caret(0, 2), Caret(0, 5));
    }

    [Fact]
    public void TypingAtACaretAfterALineBreakTypedBeforeIt_LandsOnItsOwnLine()
    {
        var editor = Editor("ab cd", Caret(0, 2), Caret(0, 5));

        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("ab\n cd\n");
        editor.Selections.Should().Equal(Caret(1, 0), Caret(2, 0));
    }

    [Fact]
    public void BackspaceAtTwoCaretsOnOneLine_DeletesBeforeEach()
    {
        var editor = Editor("abcd", Caret(0, 1), Caret(0, 3));

        Press(editor, "Backspace").Should().BeTrue();

        editor.Document.Text.Should().Be("bd");
        editor.Selections.Should().Equal(Caret(0, 0), Caret(0, 1));
    }

    [Fact]
    public void AWordDeletedLeft_ThatTakesAnotherCaretsWord_LeavesOneCaret()
    {
        var editor = Editor("foo bar", Caret(0, 3), Caret(0, 1));

        Press(editor, "Backspace", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be(" bar");
        editor.Selections.Should().Equal(Caret(0, 0));
    }

    [Fact]
    public void AWordDeletedRight_StopsWhereTheNextCaretsTextBegins()
    {
        var editor = Editor("foo bar", Caret(0, 0), Caret(0, 2));

        Press(editor, "Delete", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("bar", "as VS Code keeps the first of two deletions that would overlap");
        editor.Selections.Should().Equal(Caret(0, 0));
    }

    [Fact]
    public void TabWithTwoSelectionsOnOneLine_IndentsItOnce()
    {
        var editor = Editor("ab cd\nef", new CodeRange(At(0, 0), At(0, 2)), new CodeRange(At(0, 3), At(0, 5)));

        Press(editor, "Tab").Should().BeTrue();

        editor.Document.Text.Should().Be("    ab cd\nef");
        editor.Selections.Should().Equal(new CodeRange(At(0, 0), At(0, 6)), new CodeRange(At(0, 7), At(0, 9)));
    }

    [Fact]
    public void TabWithEmptyCarets_TypesAStepAtEach()
    {
        var editor = Editor("ab\ncd", Caret(0, 1), Caret(1, 1));

        Press(editor, "Tab").Should().BeTrue();

        editor.Document.Text.Should().Be("a   b\nc   d");
    }

    [Fact]
    public void CommentingCaretsOnTwoLinesOfOneBlock_CommentsEachLineOnce()
    {
        var editor = Editor("a\nb\nc", Caret(0, 1), Caret(1, 1), Caret(1, 0));

        Press(editor, "/", KeyModifiers.Command).Should().BeTrue();

        editor.Document.Text.Should().Be("// a\n// b\nc");
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("a\nb\nc");
    }

    [Fact]
    public void MovingTwoBlocksUp_MovesEachPastItsOwnLine()
    {
        var editor = Editor("a\nb\nc\nd\ne", Caret(1, 0), Caret(3, 0));

        Press(editor, "ArrowUp", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("b\na\nd\nc\ne");
        editor.Selections.Should().Equal(Caret(0, 0), Caret(2, 0));
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("a\nb\nc\nd\ne");
        editor.Selections.Should().Equal(Caret(1, 0), Caret(3, 0));
    }

    [Fact]
    public void MovingCaretsOnLinesThatFollowEachOther_MovesThemAsOneBlock()
    {
        var editor = Editor("a\nb\nc\nd", Caret(1, 0), Caret(2, 0));

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();

        editor.Document.Text.Should().Be("a\nd\nb\nc");
        editor.Selections.Should().Equal(Caret(2, 0), Caret(3, 0));
    }

    [Fact]
    public void ACommandAtSeveralCarets_IsHeardAsOneChange()
    {
        var editor = Editor("one\ntwo\nthree", Caret(0, 0), Caret(1, 0), Caret(2, 0));
        var heard = new List<CodeChange>();
        editor.Changed += heard.Add;

        editor.HandleText("x");

        heard.Should().ContainSingle().Which.Edits.Should().HaveCount(3);
    }

    // ---- adding and taking away carets ------------------------------------------------------------------

    [Fact]
    public void AddingCaretsBelow_MakesAColumn_TheLastAddedThePrimary()
    {
        var editor = Editor("one two\nthree four\nfive six\nseven", Caret(1, 4));

        Press(editor, "ArrowDown", KeyModifiers.Alt | KeyModifiers.Command).Should().BeTrue();
        Press(editor, "ArrowDown", KeyModifiers.Alt | KeyModifiers.Command).Should().BeTrue();

        editor.Selections.Should().Equal(Caret(3, 4), Caret(1, 4), Caret(2, 4));
    }

    [Fact]
    public void AddingACaretAbove_OnTheFirstLine_IsNotClaimed()
    {
        var editor = Editor("one\ntwo", Caret(0, 1));

        Press(editor, "ArrowUp", KeyModifiers.Command | KeyModifiers.Alt, KeyboardConvention.Standard).Should().BeFalse();

        editor.Selections.Should().Equal(Caret(0, 1));
    }

    [Fact]
    public void CommandD_SelectsTheWord_ThenAddsItsNextWholeOccurrence()
    {
        var editor = Editor("count = count + counter;\ncount++;", Caret(0, 2));

        Press(editor, "d", KeyModifiers.Command).Should().BeTrue();
        editor.Selections.Should().Equal(new CodeRange(At(0, 0), At(0, 5)));

        Press(editor, "d", KeyModifiers.Command).Should().BeTrue();
        Press(editor, "d", KeyModifiers.Command).Should().BeTrue();

        editor.Selections.Should().Equal(new CodeRange(At(1, 0), At(1, 5)), new CodeRange(At(0, 0), At(0, 5)),
            new CodeRange(At(0, 8), At(0, 13)));
        Press(editor, "d", KeyModifiers.Command).Should().BeTrue("claimed when every occurrence is taken");
        editor.Selections.Should().HaveCount(3, "counter is not a whole occurrence of count");
    }

    [Fact]
    public void CommandShiftL_SelectsEveryOccurrence_TheOneTheCaretStoodInThePrimary()
    {
        var editor = Editor("a b a\na", Caret(0, 4));

        Press(editor, "l", KeyModifiers.Command | KeyModifiers.Shift).Should().BeTrue();

        editor.Selections.Should().Equal(new CodeRange(At(0, 4), At(0, 5)), new CodeRange(At(0, 0), At(0, 1)),
            new CodeRange(At(1, 0), At(1, 1)));
        editor.HandleText("z");
        editor.Document.Text.Should().Be("z b z\nz");
    }

    [Fact]
    public void Escape_TakesAwayTheOtherCarets_BeforeItReleasesTab()
    {
        var editor = Editor("one\ntwo\nthree", Caret(2, 0), Caret(0, 0), Caret(1, 0));

        Press(editor, "Escape").Should().BeTrue();
        editor.Selections.Should().Equal(Caret(2, 0));
        editor.TabMovesFocus.Should().BeFalse("the carets went first, and Tab still indents");

        Press(editor, "Escape").Should().BeFalse();
        editor.TabMovesFocus.Should().BeTrue();
    }

    private static Point Over(int line, int column) => new(column * 8 + 1, line * 18 + 9);

    [Fact]
    public void AltAndAPress_AddsACaret_AndOnACaretTakesItAway()
    {
        var editor = Editor("one\ntwo\nthree", Caret(0, 1));

        editor.HandlePointer(PointerPhase.Down, Over(2, 2), KeyModifiers.Alt, 1).Should().BeTrue();
        editor.HandlePointer(PointerPhase.Up, Over(2, 2), KeyModifiers.Alt, 1);
        editor.Selections.Should().Equal(Caret(2, 2), Caret(0, 1));

        editor.HandlePointer(PointerPhase.Down, Over(0, 1), KeyModifiers.Alt, 1).Should().BeTrue();
        editor.HandlePointer(PointerPhase.Up, Over(0, 1), KeyModifiers.Alt, 1);
        editor.Selections.Should().Equal(Caret(2, 2));
    }

    [Fact]
    public void ShiftAltAndAPress_SelectsAColumn_ALineTooShortLeftOut()
    {
        var editor = Editor("abcdef\na\nabcdef", Caret(0, 2));

        editor.HandlePointer(PointerPhase.Down, Over(2, 4), KeyModifiers.Shift | KeyModifiers.Alt, 1).Should().BeTrue();

        editor.Selections.Should().Equal(new CodeRange(At(2, 2), At(2, 4)), new CodeRange(At(0, 2), At(0, 4)));

        editor.HandlePointer(PointerPhase.Move, Over(1, 5), KeyModifiers.Shift | KeyModifiers.Alt, 1).Should().BeTrue();
        editor.Selections.Should().ContainSingle().Which.Should().Be(new CodeRange(At(0, 2), At(0, 5)),
            "the drag narrowed the column to the first line, the second too short to reach it");
    }

    // ---- the clipboard and the completion ---------------------------------------------------------------

    [Fact]
    public void APasteWithALineForEachCaret_GivesEachItsLine()
    {
        var editor = Editor("x\ny\nz", Caret(0, 1), Caret(1, 1), Caret(2, 1));

        editor.Paste("a\nb\nc\n").Should().BeTrue();

        editor.Document.Text.Should().Be("xa\nyb\nzc");
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("x\ny\nz");
    }

    [Fact]
    public void APasteOfAnotherLength_GoesWholeToEveryCaret()
    {
        var editor = Editor("x\ny", Caret(0, 1), Caret(1, 1));

        editor.Paste("ab").Should().BeTrue();

        editor.Document.Text.Should().Be("xab\nyab");
    }

    [Fact]
    public void ACopyAtSeveralCarets_TakesEverySelection_AndAPasteAtAsManyGivesEachItsOwn()
    {
        var editor = Editor("one two\nthree", new CodeRange(At(0, 0), At(0, 3)), new CodeRange(At(1, 0), At(1, 5)));

        var copied = editor.CopyText();
        copied.Should().Be("one\nthree");

        editor.Selections = [Caret(0, 7), Caret(1, 5)];
        editor.Paste(copied).Should().BeTrue();
        editor.Document.Text.Should().Be("one twoone\nthreethree");
    }

    [Fact]
    public void ACutAtCaretsOnTwoLines_TakesBothLines()
    {
        var editor = Editor("a\nb\nc", Caret(0, 0), Caret(2, 0));

        editor.Cut().Should().Be("a\nc\n");

        editor.Document.Text.Should().Be("b\n");
    }

    private sealed class Offers(params string[] labels) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList(labels.Select(label => new CodeCompletionItem(label)).ToList()));

        public Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem item, CancellationToken cancellation) =>
            Task.FromResult(item);
    }

    [Fact]
    public void ACompletionAccepted_GoesInAtEveryCaret_AsOneUndoStep()
    {
        var editor = Editor("x = \ny = ", Caret(0, 4), Caret(1, 4));
        editor.Completion.Providers.Add(new Offers("Column", "Row"));

        editor.HandleText("C");
        editor.HandleText("o");
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("x = Column\ny = Column");
        editor.Selections.Should().Equal(Caret(0, 10), Caret(1, 10));
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("x = Co\ny = Co");
    }

    private sealed class Replacing(string label, CodeRange range) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList([new CodeCompletionItem(label) { Replacing = range }]));

        public Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem item, CancellationToken cancellation) =>
            Task.FromResult(item);
    }

    /// <summary>
    /// The word being completed is carried through what the other carets do (Copilot on #1068): with a caret
    /// before the primary on its line, a Backspace there moved the word a column back, the word's start stayed
    /// where it had been, and the entry accepted replaced one character of the word instead of two.
    /// </summary>
    [Fact]
    public void ACompletion_FollowsTheWord_ThatACaretBeforeItOnItsLineMoved()
    {
        var editor = Editor("Con Con", Caret(0, 7), Caret(0, 3));
        editor.Completion.Providers.Add(new Offers("ConsoleOutput"));
        editor.Completion.Invoke().Should().BeTrue();

        Press(editor, "Backspace").Should().BeTrue();
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("ConsoleOutput ConsoleOutput");
    }

    /// <summary>
    /// A provider's range is carried through what the other carets do (Copilot on #1068): its end was kept as
    /// far from the end of the line as when the provider was asked, so a character typed at a caret further
    /// on the line moved it, and the entry took the space after the word with it.
    /// </summary>
    [Fact]
    public void AProvidersRange_StaysWithItsWord_WhenACaretAfterItOnItsLineTypes()
    {
        var editor = Editor("Con Con", Caret(0, 3), Caret(0, 7));
        editor.Completion.Providers.Add(new Replacing("ConsoleOutput", new CodeRange(At(0, 0), At(0, 3))));
        editor.Completion.Invoke().Should().BeTrue();

        editor.HandleText("s");
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("ConsoleOutput ConsoleOutput");
    }

    // ---- from the review ------------------------------------------------------------------------------

    private sealed class Late : ICodeCompletionProvider
    {
        public TaskCompletionSource<CodeCompletionList> Answer { get; } = new();

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) => Answer.Task;

        public Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem item, CancellationToken cancellation) =>
            Task.FromResult(item);
    }

    /// <summary>Escape that takes the other carets away drops a list asked for and not yet showing, as the card's
    /// and the panel's Escape do: its late answer opened a list nobody wanted.</summary>
    [Fact]
    public void EscapeTakingAwayTheCarets_DropsAListNotYetShowing()
    {
        var editor = Editor("x = \ny = ", Caret(0, 4), Caret(1, 4));
        var late = new Late();
        editor.Completion.Providers.Add(late);

        editor.HandleText("C");
        Press(editor, "Escape").Should().BeTrue();
        late.Answer.SetResult(new CodeCompletionList([new CodeCompletionItem("Column")]));

        editor.Selections.Should().HaveCount(1);
        editor.Completion.IsOpen.Should().BeFalse("Escape said nothing over the caret was wanted");
    }

    /// <summary>Carets placed are heard once they are made one: heard first, a listener read a primary the merge
    /// then grew.</summary>
    [Fact]
    public void SelectionsPlaced_AreHeardOnceMadeOne()
    {
        var editor = Editor("abcdef", Caret(0, 0));
        var heard = new List<CodeRange>();
        editor.SelectionChanged += heard.Add;

        editor.Selections = [Caret(0, 2), new CodeRange(At(0, 1), At(0, 4))];

        editor.Selections.Should().Equal(new CodeRange(At(0, 1), At(0, 4)));
        heard.Should().ContainSingle().Which.Should().Be(editor.Selection);
    }

    /// <summary>Whole lines copied at carets two of which share a line go back as lines, one for each caret:
    /// taken once, they were fewer than the carets and went in whole in the middle of every line.</summary>
    [Fact]
    public void WholeLinesCopiedAtCaretsSharingALine_GoBackAsLines()
    {
        var editor = Editor("one\ntwo\nthree", Caret(0, 1), Caret(0, 2), Caret(2, 0));

        var copied = editor.CopyText();
        editor.Paste(copied).Should().BeTrue();

        copied.Should().Be("one\none\nthree\n");
        editor.Document.Text.Should().Be("one\none\none\ntwo\nthree\nthree");
    }

    [Fact]
    public void AltAndADoublePress_AddsTheWord()
    {
        var editor = Editor("alpha beta\ngamma", Caret(1, 2));

        editor.HandlePointer(PointerPhase.Down, Over(0, 7), KeyModifiers.Alt, 1);
        editor.HandlePointer(PointerPhase.Down, Over(0, 7), KeyModifiers.Alt, 2).Should().BeTrue();
        editor.HandlePointer(PointerPhase.Up, Over(0, 7), KeyModifiers.Alt, 2);

        editor.Selections.Should().Equal(new CodeRange(At(0, 6), At(0, 10)), Caret(1, 2));
    }

    /// <summary>An input method composes at the primary caret, the other carets keeping their text as it grows and
    /// goes, and what it commits is typed at every caret.</summary>
    [Fact]
    public void ACompositionAtThePrimary_CarriesTheOthers_AndItsCommitGoesToEvery()
    {
        var editor = Editor("abcd", Caret(0, 1), Caret(0, 3));

        editor.SetComposition("x").Should().BeTrue();
        editor.SetComposition("xy").Should().BeTrue();
        editor.Document.Text.Should().Be("axybcd");
        editor.Selections.Should().Equal(Caret(0, 3), Caret(0, 5));

        editor.SetComposition("").Should().BeTrue();
        editor.Document.Text.Should().Be("abcd");
        editor.Selections.Should().Equal(Caret(0, 1), Caret(0, 3));

        editor.SetComposition("z");
        editor.HandleText("zw").Should().BeTrue();
        editor.Document.Text.Should().Be("azwbczwd");
        editor.Selections.Should().Equal(Caret(0, 3), Caret(0, 7));
    }

    /// <summary>
    /// A command at carets on lines of their own does at each what it does at one caret alone: every command
    /// that keeps to its line, over seeded documents and carets, against the same command at a caret on that
    /// line by itself.
    /// </summary>
    [Fact]
    public void ACommandAtCaretsOnLinesOfTheirOwn_DoesAtEachWhatItDoesAtOne()
    {
        var commands = new (string Name, Func<CodeEditorController, bool> Run)[]
        {
            ("type x", editor => editor.HandleText("x")),
            ("type (", editor => editor.HandleText("(")),
            ("type quote", editor => editor.HandleText("\"")),
            ("backspace", editor => editor.DeleteBackward()),
            ("delete", editor => editor.DeleteForward()),
            ("word left", editor => editor.DeleteBackward(CodeMotion.Word)),
            ("word right", editor => editor.DeleteForward(CodeMotion.Word)),
            ("tab", editor => editor.Indent()),
            ("shift tab", editor => editor.Outdent()),
            ("comment", editor => editor.ToggleLineComment()),
        };
        var words = new[] { "foo", "bar", "  ", "\t", "x(y)", "// c", "\"s\"", "{", "}", "a b" };
        var random = new Random(1012);
        var failures = new List<string>();
        for (var round = 0; round < 300; round++)
        {
            var lines = new List<string>();
            var count = random.Next(2, 7);
            for (var i = 0; i < count; i++)
            {
                var line = "";
                var parts = random.Next(1, 4);
                for (var p = 0; p < parts; p++) line += words[random.Next(words.Length)];
                lines.Add(line + "z");
            }
            var carets = new List<CodeRange>();
            for (var i = 0; i < count; i++)
                if (random.Next(3) > 0) carets.Add(Caret(i, random.Next(1, lines[i].Length)));
            if (carets.Count < 2) continue;
            var (name, run) = commands[random.Next(commands.Length)];

            var editor = Editor(string.Join("\n", lines), carets.ToArray());
            run(editor);
            var placed = editor.Selections.OrderBy(selection => selection.Start).ToList();
            if (placed.Count != carets.Count)
            {
                failures.Add($"{name} over {Show(lines)} at {Show(carets)}: {placed.Count} carets left");
                continue;
            }
            for (var k = 0; k < carets.Count; k++)
            {
                var line = carets[k].Start.Line;
                var alone = Editor(lines[line], Caret(0, carets[k].Start.Column));
                run(alone);
                var expected = alone.Document.Text;
                var at = placed[k].Start.Line;
                if (editor.Document.Line(at) != expected || placed[k].Start.Column != alone.Selection.Start.Column
                    || placed[k].End.Column != alone.Selection.End.Column)
                {
                    failures.Add($"{name} over {Show(lines)} at {Show(carets)}: line {line} reads "
                        + $"\"{editor.Document.Line(at)}\" at {placed[k].Start.Column}-{placed[k].End.Column}, "
                        + $"alone \"{expected}\" at {alone.Selection.Start.Column}-{alone.Selection.End.Column}");
                }
            }
            if (editor.Undo() && editor.Document.Text != string.Join("\n", lines))
                failures.Add($"{name} over {Show(lines)} at {Show(carets)}: undo gives \"{editor.Document.Text}\"");
        }
        failures.Should().BeEmpty($"{failures.Count} broke, among them:\n{string.Join("\n", failures.Take(10))}");

        static string Show<T>(IEnumerable<T> items) => string.Join("|", items);
    }
}
