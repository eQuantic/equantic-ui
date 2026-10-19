using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The problems of an editor (<see cref="CodeDiagnostics"/>), driven as a person drives them: keys
/// and typed text through the editor's own doors, and providers that answer at once, late, wrongly or
/// not at all. No host and no screen (docs/CODE-EDITOR-PLAN.md §5, #299).
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeDiagnosticsTests : IDisposable
{
    // An answer is applied through the process's UI dispatcher when it comes back on another thread;
    // these tests answer on their own, so whatever a hosted app left armed is put aside.
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeDiagnosticsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    // ---- the fixtures ---------------------------------------------------------------------------

    /// <summary>A provider that answers at once with its problems, and says what it was asked.</summary>
    private sealed class Provider : ICodeDiagnosticProvider
    {
        public List<CodeDiagnostic> Problems { get; } = [];
        public Exception? Throws { get; init; }
        public List<string> Asked { get; } = [];

        public Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation)
        {
            Asked.Add(document.Text);
            if (Throws is not null) throw Throws;
            return Task.FromResult<IReadOnlyList<CodeDiagnostic>>(Problems.ToList());
        }
    }

    /// <summary>A provider whose answers come when the test says so.</summary>
    private sealed class LateProvider : ICodeDiagnosticProvider
    {
        private readonly List<TaskCompletionSource<IReadOnlyList<CodeDiagnostic>>> _pending = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            var answer = new TaskCompletionSource<IReadOnlyList<CodeDiagnostic>>();
            _pending.Add(answer);
            return answer.Task;
        }

        public void Answer(int request, params CodeDiagnostic[] problems) => _pending[request].SetResult(problems);
    }

    private static CodeDiagnostic Error(int line, int from, int to, string message = "bad") =>
        new(new CodeRange(new CodePosition(line, from), new CodePosition(line, to)), message);

    private static CodeDiagnostic Warning(int line, int from, int to, string message = "careful") =>
        new(new CodeRange(new CodePosition(line, from), new CodePosition(line, to)), message, CodeDiagnosticSeverity.Warning);

    /// <summary>An editor that asks its providers the moment it is edited, so a test reads the answer
    /// on the next line.</summary>
    private static CodeEditorController Editor(string text)
    {
        var editor = new CodeEditorController(text);
        editor.Diagnostics.Delay = TimeSpan.Zero;
        return editor;
    }

    private static void Type(CodeEditorController editor, string text)
    {
        foreach (var c in text) editor.HandleText(c.ToString());
    }

    private static void Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Standard, null);

    private static CodeRange At(int line, int from, int to) =>
        new(new CodePosition(line, from), new CodePosition(line, to));

    // ---- what the editor holds ------------------------------------------------------------------

    [Fact]
    public void TheAppsProblemsAndAProvidersAnswer_ShowTogetherInDocumentOrder()
    {
        var editor = Editor("one\ntwo\nthree\nfour");
        var provider = new Provider();
        provider.Problems.Add(Warning(1, 0, 3));
        editor.Diagnostics.Providers.Add(provider);

        editor.Diagnostics.Report([Error(3, 0, 4)]);
        editor.Diagnostics.Check();

        editor.Diagnostics.Items.Select(problem => problem.Range).Should().Equal(At(1, 0, 3), At(3, 0, 4));
    }

    [Fact]
    public void AReportReplacesOnlyWhatTheAppReportedBefore()
    {
        var editor = Editor("one\ntwo\nthree\nfour");
        var provider = new Provider();
        provider.Problems.Add(Warning(1, 0, 3));
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Report([Error(3, 0, 4)]);
        editor.Diagnostics.Check();

        editor.Diagnostics.Report([]);

        editor.Diagnostics.Items.Should().ContainSingle().Which.Severity.Should().Be(CodeDiagnosticSeverity.Warning);
    }

    [Fact]
    public void AProvidersRangeOutsideTheDocument_IsPlacedInsideIt()
    {
        var editor = Editor("ab");
        editor.Diagnostics.Report([Error(4, 2, 9)]);

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 2, 2));
    }

    // ---- travelling with the edits --------------------------------------------------------------

    [Fact]
    public void TypingBeforeAProblem_ShiftsItWithTheTextItIsAbout()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Selection = new CodeRange(CodePosition.Start);
        Type(editor, "long ");

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 13, 14));
    }

    [Fact]
    public void TypingRightAfterAProblem_DoesNotGrowIt()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Selection = new CodeRange(new CodePosition(0, 9));
        Type(editor, "z");

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 8, 9));
    }

    [Fact]
    public void TypingRightBeforeAProblem_DoesNotGrowIt()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Selection = new CodeRange(new CodePosition(0, 8));
        Type(editor, "q");

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 9, 10));
    }

    [Fact]
    public void RewritingAProblemsText_CoversTheRewrite()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Selection = At(0, 8, 9);
        editor.Insert("count");

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 8, 13));
    }

    [Fact]
    public void DeletingAProblemsText_LeavesItEmptyWhereTheTextWas()
    {
        var editor = Editor("int x = yes;");
        editor.Diagnostics.Report([Error(0, 8, 11)]);

        editor.Selection = At(0, 8, 11);
        Press(editor, "Backspace");

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 8, 8));
    }

    [Fact]
    public void ALineInsertedAboveAndUndone_PutsTheProblemBackOnItsLine()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Selection = new CodeRange(CodePosition.Start);
        editor.Insert("// a comment\n");
        editor.Diagnostics.Items.Single().Range.Should().Be(At(1, 8, 9));

        editor.Undo().Should().BeTrue();

        editor.Diagnostics.Items.Single().Range.Should().Be(At(0, 8, 9));
    }

    [Fact]
    public void AnotherDocument_DropsEveryProblem()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9)]);

        editor.Document = CodeDocument.FromText("class C { }");

        editor.Diagnostics.Items.Should().BeEmpty();
    }

    // ---- asking ---------------------------------------------------------------------------------

    [Fact]
    public async Task ABurstOfKeystrokes_AsksOnceAboutTheTextTheyLeft()
    {
        var editor = new CodeEditorController("");
        var provider = new Provider();
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Delay = TimeSpan.FromMilliseconds(200);

        Type(editor, "abcde");
        provider.Asked.Should().BeEmpty("nothing is asked while typing has not paused");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (provider.Asked.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        await Task.Delay(300);

        provider.Asked.Should().Equal("abcde");
    }

    [Fact]
    public void AKeystrokeWhileARequestIsOut_CancelsItAndItsAnswerChangesNothing()
    {
        var editor = Editor("");
        var provider = new LateProvider();
        editor.Diagnostics.Providers.Add(provider);

        Type(editor, "a");
        Type(editor, "b");

        provider.Tokens.Should().HaveCount(2);
        provider.Tokens[0].IsCancellationRequested.Should().BeTrue();
        provider.Tokens[1].IsCancellationRequested.Should().BeFalse();

        provider.Answer(0, Error(0, 0, 1, "stale"));
        editor.Diagnostics.Items.Should().BeEmpty();

        provider.Answer(1, Error(0, 0, 2, "fresh"));
        editor.Diagnostics.Items.Single().Message.Should().Be("fresh");
    }

    [Fact]
    public void AProviderThatThrows_RaisesFailedAndTheOthersStillShow()
    {
        var editor = Editor("abc");
        var failing = new Provider { Throws = new InvalidOperationException("the compiler crashed") };
        var answering = new Provider();
        answering.Problems.Add(Warning(0, 0, 3));
        editor.Diagnostics.Providers.Add(failing);
        editor.Diagnostics.Providers.Add(answering);
        var failures = new List<Exception>();
        editor.Diagnostics.Failed += failures.Add;

        editor.Diagnostics.Check();

        failures.Should().ContainSingle().Which.Message.Should().Be("the compiler crashed");
        editor.Diagnostics.Items.Should().ContainSingle().Which.Severity.Should().Be(CodeDiagnosticSeverity.Warning);
    }

    [Fact]
    public void AProviderTakenOut_TakesItsProblemsWithIt()
    {
        var editor = Editor("abc");
        var provider = new Provider();
        provider.Problems.Add(Warning(0, 0, 3));
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Check();

        editor.Diagnostics.Providers.Remove(provider);
        editor.Diagnostics.Report([]);

        editor.Diagnostics.Items.Should().BeEmpty();
    }

    /// <summary>
    /// …and put back, it shows nothing until it answers again: its last answer went with it, and came
    /// back with it before, carried through every edit made while it was out.
    /// </summary>
    [Fact]
    public void AProviderPutBack_ShowsNothingUntilItAnswersAgain()
    {
        var editor = Editor("abc");
        var provider = new Provider();
        provider.Problems.Add(Warning(0, 0, 3));
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Check();
        editor.Diagnostics.Providers.Remove(provider);
        editor.Diagnostics.Report([]);

        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Report([]);

        editor.Diagnostics.Items.Should().BeEmpty();
    }

    /// <summary>
    /// The same problems again change nothing, and say nothing: a component builds again on
    /// <see cref="CodeDiagnostics.Changed"/>, so an answer that found what the last one did would cost a
    /// build, and a provider handed anew on every build would be asked again by the build its own
    /// answer caused, without end.
    /// </summary>
    [Fact]
    public void TheSameProblemsAgain_RaiseNothing()
    {
        var editor = Editor("abc\ndef");
        var provider = new Provider();
        provider.Problems.Add(Warning(1, 0, 3));
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Report([Error(0, 0, 3)]);
        editor.Diagnostics.Check();
        var raised = 0;
        editor.Diagnostics.Changed += () => raised++;

        editor.Diagnostics.Check();
        editor.Diagnostics.Report([Error(0, 0, 3)]);
        editor.Selection = new CodeRange(new CodePosition(1, 3));
        Type(editor, "!");

        raised.Should().Be(0, "an answer, a report and a keystroke after every problem changed none of them");
        editor.Selection = new CodeRange(new CodePosition(0, 0));
        Type(editor, "x");
        raised.Should().Be(1, "a keystroke before them moved them, and that is a change");
    }

    /// <summary>
    /// A provider that reads the text finds the typo, and fixing it takes the problem away once the
    /// provider answers again: what #299 accepts, on the engine every target runs.
    /// </summary>
    [Fact]
    public void FixingWhatAProviderFound_TakesItAway()
    {
        var editor = Editor("var totl = 1;");
        editor.Diagnostics.Providers.Add(new Speller("totl"));
        editor.Diagnostics.Check();
        editor.Diagnostics.Items.Should().ContainSingle().Which.Range.Should().Be(At(0, 4, 8));

        editor.Selection = new CodeRange(new CodePosition(0, 7));
        Type(editor, "a");

        editor.Document.Line(0).Should().Be("var total = 1;");
        editor.Diagnostics.Items.Should().BeEmpty("the provider found no typo in the fixed text");
    }

    /// <summary>Flags every place <paramref name="typo"/> is written, as a spell checker flags a word.</summary>
    private sealed class Speller(string typo) : ICodeDiagnosticProvider
    {
        public Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation)
        {
            var found = new List<CodeDiagnostic>();
            for (var line = 0; line < document.LineCount; line++)
            {
                var text = document.Line(line);
                for (var at = text.IndexOf(typo, StringComparison.Ordinal); at >= 0;
                     at = text.IndexOf(typo, at + typo.Length, StringComparison.Ordinal))
                    found.Add(new CodeDiagnostic(At(line, at, at + typo.Length), $"'{typo}' is misspelt."));
            }
            return Task.FromResult<IReadOnlyList<CodeDiagnostic>>(found);
        }
    }

    /// <summary>A provider taken out and the problems checked: what it found goes at once, with no edit
    /// or report to gather them again (found reviewing #299).</summary>
    [Fact]
    public void AProviderTakenOutAndChecked_TakesItsProblemsAtOnce()
    {
        var editor = Editor("abc");
        var provider = new Provider();
        provider.Problems.Add(Warning(0, 0, 3));
        editor.Diagnostics.Providers.Add(provider);
        editor.Diagnostics.Check();

        editor.Diagnostics.Providers.Remove(provider);
        editor.Diagnostics.Check();

        editor.Diagnostics.Items.Should().BeEmpty();
    }

    // ---- the walk -------------------------------------------------------------------------------

    [Fact]
    public void F8_WalksForwardAndWrapsPastTheLastProblem()
    {
        var editor = Editor("a\nbb\nc\nd\ne\nff\ng");
        editor.Diagnostics.Report([Error(1, 1, 2), Warning(5, 0, 2)]);
        editor.Selection = new CodeRange(new CodePosition(6, 0));

        Press(editor, "F8");
        editor.Caret.Should().Be(new CodePosition(1, 1));

        Press(editor, "F8");
        editor.Caret.Should().Be(new CodePosition(5, 0));
    }

    [Fact]
    public void ShiftF8_WalksBackwardAndWrapsBeforeTheFirst()
    {
        var editor = Editor("a\nbb\nc\nd\ne\nff\ng");
        editor.Diagnostics.Report([Error(1, 1, 2), Warning(5, 0, 2)]);
        editor.Selection = new CodeRange(new CodePosition(0, 0));

        Press(editor, "F8", KeyModifiers.Shift);
        editor.Caret.Should().Be(new CodePosition(5, 0));

        Press(editor, "F8", KeyModifiers.Shift);
        editor.Caret.Should().Be(new CodePosition(1, 1));
    }

    [Fact]
    public void F8_ShowsTheCardOfTheProblemItReached()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9, "CS0103: The name 'y' does not exist")]);

        Press(editor, "F8");

        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.ByKeyboard.Should().BeTrue();
        editor.Hover.Problems.Single().Message.Should().Be("CS0103: The name 'y' does not exist");
    }

    [Fact]
    public void F8_WithNoProblem_IsLeftToTheHost()
    {
        var editor = Editor("int x = y;");

        editor.HandleKey("F8", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeFalse();
    }
}
