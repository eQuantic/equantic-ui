using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The completion of an editor (<see cref="CodeCompletion"/>), driven as a person drives it: keys and
/// typed text through the editor's own doors, and providers that answer at once, late, wrongly or
/// not at all. No host and no screen, which is the plan's point (§5): what the list does is decided
/// here, once, and a realizer only draws it.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeCompletionTests : IDisposable
{
    // An answer is applied through the process's UI dispatcher when it comes back on another thread;
    // these tests answer on their own, so whatever a hosted app left armed is put aside.
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeCompletionTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    // ---- the fixtures ---------------------------------------------------------------------------

    /// <summary>A provider that answers at once with its items, and says what it was asked.</summary>
    private sealed class Provider : ICodeCompletionProvider
    {
        public List<CodeCompletionItem> Items { get; } = [];
        public bool Incomplete { get; init; }
        public IReadOnlyList<char> TriggerCharacters { get; init; } = [];
        public Exception? Throws { get; init; }
        public Exception? Faults { get; init; }
        /// <summary>Registers, on every request, a callback that throws when the request is
        /// cancelled: a language server's adapter whose connection dropped.</summary>
        public bool ThrowsOnCancel { get; init; }
        public Func<CodeCompletionItem, CodeCompletionItem>? Resolver { get; init; }
        public List<(CodePosition Position, CodeCompletionContext Context)> Asked { get; } = [];
        public List<string> Resolved { get; } = [];

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation)
        {
            Asked.Add((position, context));
            if (ThrowsOnCancel) cancellation.Register(() => throw new InvalidOperationException("the connection dropped"));
            if (Throws is not null) throw Throws;
            if (Faults is not null) return Task.FromException<CodeCompletionList>(Faults);
            return Task.FromResult(new CodeCompletionList(Items.ToList(), Incomplete));
        }

        public Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem item, CancellationToken cancellation)
        {
            Resolved.Add(item.Label);
            return Task.FromResult(Resolver?.Invoke(item) ?? item);
        }
    }

    /// <summary>A provider whose answers come when the test says so, in whatever order it says.</summary>
    private sealed class LateProvider : ICodeCompletionProvider
    {
        private readonly List<TaskCompletionSource<CodeCompletionList>> _pending = [];
        public List<CancellationToken> Cancellations { get; } = [];
        public IReadOnlyList<char> TriggerCharacters { get; init; } = [];

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation)
        {
            Cancellations.Add(cancellation);
            var answer = new TaskCompletionSource<CodeCompletionList>();
            _pending.Add(answer);
            return answer.Task;
        }

        public int Count => _pending.Count;

        public void Answer(int request, params string[] labels) =>
            _pending[request].SetResult(new CodeCompletionList(labels.Select(label => new CodeCompletionItem(label)).ToList()));
    }

    /// <summary>A UI thread that is never the caller's: everything is posted, and runs when drained.</summary>
    private sealed class QueueDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _queue = new();
        public bool IsOnUiThread => false;
        public void Post(Action work) => _queue.Enqueue(work);

        public void Drain()
        {
            while (_queue.TryDequeue(out var work)) work();
        }
    }

    private static Provider Offering(params string[] labels)
    {
        var provider = new Provider();
        provider.Items.AddRange(labels.Select(label => new CodeCompletionItem(label)));
        return provider;
    }

    /// <summary>An editor over <paramref name="text"/>, its caret at the end, asking the providers.</summary>
    private static CodeEditorController Editor(string text, params ICodeCompletionProvider[] providers) =>
        Editor(text, CodeLanguages.CSharp, providers);

    private static CodeEditorController Editor(string text, ICodeLanguage language,
        params ICodeCompletionProvider[] providers)
    {
        var editor = new CodeEditorController(text, language);
        foreach (var provider in providers) editor.Completion.Providers.Add(provider);
        editor.Selection = new CodeRange(editor.Document.End);
        return editor;
    }

    /// <summary>Types one character at a time, as a keyboard does.</summary>
    private static void Type(CodeEditorController editor, string text)
    {
        foreach (var c in text) editor.HandleText(c.ToString());
    }

    private static bool Key(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Standard, null);

    private static List<string> Labels(CodeEditorController editor) =>
        editor.Completion.Items.Select(match => match.Item.Label).ToList();

    private static string SelectedLabel(CodeEditorController editor) =>
        editor.Completion.Items[editor.Completion.Selected].Item.Label;

    // ---- asking once, filtering locally -----------------------------------------------------------

    [Fact]
    public void AnEditor_OffersNothingUntilItIsGivenAProvider()
    {
        var editor = new CodeEditorController("", CodeLanguages.CSharp);

        editor.Completion.Providers.Should().BeEmpty(
            "a list nothing draws must never take a key, so whoever draws it says what it offers");
        Type(editor, "ret");
        Key(editor, " ", KeyModifiers.Command).Should().BeFalse();
        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void StartingAWord_AsksOnce_AndTypingFiltersWhatWasAnswered()
    {
        var provider = Offering("Column", "ColorToken", "Row", "Collection");
        var editor = Editor("", provider);

        Type(editor, "Col");

        provider.Asked.Should().ContainSingle();
        provider.Asked[0].Context.Trigger.Should().Be(CodeCompletionTrigger.Typing);
        provider.Asked[0].Context.Character.Should().Be('C');
        provider.Asked[0].Context.Language.Should().BeSameAs(CodeLanguages.CSharp);
        provider.Asked[0].Position.Should().Be(new CodePosition(0, 1));
        editor.Completion.IsOpen.Should().BeTrue();
        editor.Completion.Start.Should().Be(new CodePosition(0, 0));
        // Three prefixes of equal worth, then by their text: the order is the rule's, not the answer's.
        Labels(editor).Should().Equal("Collection", "ColorToken", "Column");
        editor.Completion.Items[0].Highlights.Should().Equal(0, 1, 2);

        Type(editor, "u");

        Labels(editor).Should().Equal("Column");
        provider.Asked.Should().ContainSingle("a complete answer is filtered as the word grows, never asked for again");
    }

    [Fact]
    public void ABetterMatch_RanksFirst_WhateverOrderTheProviderAnswered()
    {
        var editor = Editor("", Offering("CopyLine", "Column"));

        Type(editor, "col");

        Labels(editor).Should().Equal("Column", "CopyLine");
    }

    [Fact]
    public void AWordTypedBeforeTheAnswerCame_FiltersIt_WhenItComes()
    {
        var late = new LateProvider();
        var editor = Editor("", late);

        Type(editor, "Colu");
        editor.Completion.IsLoading.Should().BeTrue();
        editor.Completion.IsOpen.Should().BeFalse();
        late.Count.Should().Be(1, "the word was started once");

        late.Answer(0, "Collection", "Column");

        editor.Completion.IsLoading.Should().BeFalse();
        Labels(editor).Should().Equal("Column");
    }

    // ---- the list's keys ---------------------------------------------------------------------------

    [Fact]
    public void Enter_AcceptsTheSelectedEntry_OverTheWordTyped_AndOneUndoGivesTheWordBack()
    {
        var editor = Editor("var x = ", Offering("Column", "Row"));
        Type(editor, "Co");

        Key(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("var x = Column");
        editor.Caret.Should().Be(new CodePosition(0, 14));
        editor.Completion.IsOpen.Should().BeFalse();
        editor.Undo();
        editor.Document.Text.Should().Be("var x = Co", "accepting is one step of its own");
    }

    [Fact]
    public void Enter_EndsTheLine_WhenTheWordIsTypedOutInFull()
    {
        var editor = Editor("", Offering("int", "interface"));
        Type(editor, "int");
        editor.Completion.IsOpen.Should().BeTrue();
        SelectedLabel(editor).Should().Be("int");

        Key(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("int\n", "taking the word that is already there changes nothing");
        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Tab_Accepts_EvenAWordTypedOutInFull()
    {
        var editor = Editor("", Offering("int", "interface"));
        Type(editor, "int");

        Key(editor, "Tab").Should().BeTrue();

        editor.Document.Text.Should().Be("int", "Tab took the list's entry, and indented nothing");
        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Escape_ClosesTheList_AndOnlyTheList()
    {
        var editor = Editor("", Offering("Column"));
        Type(editor, "Co");

        Key(editor, "Escape").Should().BeTrue("closing the list is what that Escape meant");

        editor.Completion.IsOpen.Should().BeFalse();
        editor.TabMovesFocus.Should().BeFalse("the trap on Tab is released by the NEXT Escape");
        Key(editor, "Escape").Should().BeFalse();
        editor.TabMovesFocus.Should().BeTrue();
    }

    [Fact]
    public void AListAskedForAfterEscape_ArmsTheTrapOnTabAgain()
    {
        var editor = Editor("", Offering("Column"));
        Key(editor, "Escape").Should().BeFalse();
        editor.TabMovesFocus.Should().BeTrue("Escape with no list releases the trap");

        // ⌃Space is a key like any other, and the Escape after it closes the list it asked for, only it.
        Key(editor, " ", KeyModifiers.Control).Should().BeTrue();
        editor.Completion.IsOpen.Should().BeTrue();
        Key(editor, "Escape").Should().BeTrue();

        editor.TabMovesFocus.Should().BeFalse("a key was pressed since the Escape that released it");
        Key(editor, "Tab").Should().BeTrue("Tab indents, and stays in the editor");
        editor.Document.Text.Should().Be("    ");
    }

    [Fact]
    public void TheArrowsWalkTheList_RoundFromTheLastToTheFirst()
    {
        var editor = Editor("", Offering("Cab", "Cad", "Cat"));
        Type(editor, "Ca");
        editor.Completion.Selected.Should().Be(0);

        Key(editor, "ArrowDown").Should().BeTrue();
        editor.Completion.Selected.Should().Be(1);
        Key(editor, "ArrowDown");
        Key(editor, "ArrowDown");
        editor.Completion.Selected.Should().Be(0);
        Key(editor, "ArrowUp");
        editor.Completion.Selected.Should().Be(2);
        editor.Caret.Should().Be(new CodePosition(0, 2), "the arrows walked the list, not the text");

        Key(editor, "Enter");
        editor.Document.Text.Should().Be("Cat");
    }

    [Fact]
    public void WithOneEntryShowing_TheArrowsMoveTheCaret()
    {
        var editor = Editor("first\n", Offering("Column"));
        Type(editor, "Co");

        Key(editor, "ArrowUp").Should().BeTrue();

        editor.Caret.Line.Should().Be(0);
        editor.Completion.IsOpen.Should().BeFalse("the caret left the word");
    }

    [Fact]
    public void ThePageKeys_StepAPage_AndStopAtEitherEnd()
    {
        var editor = Editor("", Offering(Enumerable.Range(0, 30).Select(i => $"Item{i:00}").ToArray()));
        editor.Completion.PageSize = 10;
        Type(editor, "It");

        Key(editor, "PageDown");
        editor.Completion.Selected.Should().Be(10);
        Key(editor, "PageDown");
        Key(editor, "PageDown");
        editor.Completion.Selected.Should().Be(29);
        Key(editor, "PageUp");
        editor.Completion.Selected.Should().Be(19);
    }

    [Fact]
    public void ControlSpace_AsksForTheList_WhereNoWordIsTyped()
    {
        var provider = Offering("beta", "alpha");
        var editor = Editor("x = ", provider);

        // A browser reports ⌃ as the command key, and a Mac's shell as Control: both ask.
        Key(editor, " ", KeyModifiers.Control).Should().BeTrue();

        provider.Asked.Should().ContainSingle().Which.Context.Trigger.Should().Be(CodeCompletionTrigger.Invoked);
        editor.Completion.Start.Should().Be(new CodePosition(0, 4));
        Labels(editor).Should().Equal("alpha", "beta");

        Key(editor, "Escape");
        Key(editor, " ", KeyModifiers.Command).Should().BeTrue();
        editor.Completion.IsOpen.Should().BeTrue();
    }

    // ---- what opens a list, and what closes one ---------------------------------------------------

    [Fact]
    public void ATriggerCharacter_AsksOnlyItsProvider_FromAfterIt()
    {
        var members = new Provider { TriggerCharacters = ['.'] };
        members.Items.Add(new CodeCompletionItem("Theme", CodeCompletionKind.Property));
        members.Items.Add(new CodeCompletionItem("Route", CodeCompletionKind.Property));
        var words = Offering("context");
        var editor = Editor("", members, words);
        Type(editor, "context");
        var askedBefore = words.Asked.Count;

        Type(editor, ".");

        members.Asked[^1].Context.Trigger.Should().Be(CodeCompletionTrigger.Character);
        members.Asked[^1].Context.Character.Should().Be('.');
        members.Asked[^1].Position.Should().Be(new CodePosition(0, 8));
        words.Asked.Should().HaveCount(askedBefore, "a trigger character is a claim of the provider that declared it");
        editor.Completion.Start.Should().Be(new CodePosition(0, 8));
        Labels(editor).Should().Equal("Route", "Theme");

        Type(editor, "Th");
        Key(editor, "Enter");
        editor.Document.Text.Should().Be("context.Theme");
    }

    [Fact]
    public void NoListOpensUnasked_InACommentOrAString_AndControlSpaceStillOpensOne()
    {
        var provider = Offering("Column");
        var editor = Editor("", provider);

        Type(editor, "// Co");
        editor.Completion.IsOpen.Should().BeFalse();
        Type(editor, "\n\"Co");
        editor.Completion.IsOpen.Should().BeFalse();
        provider.Asked.Should().BeEmpty();

        Key(editor, " ", KeyModifiers.Command).Should().BeTrue();
        editor.Completion.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void ANumber_OpensNoList()
    {
        var provider = Offering("x1");
        var editor = Editor("", provider);

        Type(editor, "12");

        provider.Asked.Should().BeEmpty();
    }

    [Fact]
    public void AReadOnlyEditor_NeverOpensAList()
    {
        var provider = Offering("Column");
        var editor = Editor("Co", provider);
        editor.ReadOnly = true;

        Key(editor, " ", KeyModifiers.Command).Should().BeFalse();

        provider.Asked.Should().BeEmpty();
    }

    [Fact]
    public void LeavingTheWord_ClosesTheList()
    {
        var editor = Editor("x ", Offering("Column"));
        Type(editor, "Co");
        Key(editor, "ArrowLeft");
        editor.Completion.IsOpen.Should().BeTrue("the caret is still in the word, and the word is now C");
        Labels(editor).Should().Equal("Column");

        Key(editor, "ArrowLeft");
        editor.Completion.IsOpen.Should().BeFalse("the caret is before the word now");

        Type(editor, "C");
        Key(editor, "ArrowRight", KeyModifiers.Shift);
        editor.Completion.IsOpen.Should().BeFalse("a selection is not a word being typed");
    }

    [Fact]
    public void ACharacterThatEndsAWord_ClosesTheList()
    {
        var editor = Editor("", Offering("Column"));
        Type(editor, "Co");

        Type(editor, " ");

        editor.Completion.IsOpen.Should().BeFalse();
        editor.Document.Text.Should().Be("Co ");
    }

    [Fact]
    public void LosingTheKeyboard_ClosesTheList()
    {
        var editor = Editor("", Offering("Column"));
        Type(editor, "Co");

        editor.FocusChanged(false);

        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ReplacingTheDocument_ClosesTheList()
    {
        var editor = Editor("", Offering("Column"));
        Type(editor, "Co");

        editor.Document = CodeDocument.FromText("another file");

        editor.Completion.IsOpen.Should().BeFalse();
    }

    // ---- latency: answers that arrive late, and answers nobody wants ------------------------------

    [Fact]
    public void AnAnswerForAListAlreadyClosed_IsDropped_AndItsRequestCancelled()
    {
        var late = new LateProvider();
        var editor = Editor("", late);
        Type(editor, "Co");

        Key(editor, "Escape").Should().BeFalse("nothing was showing, so Escape goes on to mean what it means");
        late.Cancellations[0].IsCancellationRequested.Should().BeTrue();
        late.Answer(0, "Column");

        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ANewerRequest_DropsTheOlderAnswer_HoweverLateItComes()
    {
        var late = new LateProvider { TriggerCharacters = ['.'] };
        var editor = Editor("", late);
        Type(editor, "a");
        Type(editor, ".");
        late.Count.Should().Be(2);

        late.Answer(1, "Theme");
        late.Answer(0, "apple");

        Labels(editor).Should().Equal("Theme");
        late.Cancellations[0].IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void EnterWithNothingShowing_EndsTheLine_AndDropsTheRequest()
    {
        var late = new LateProvider();
        var editor = Editor("", late);
        Type(editor, "Co");

        Key(editor, "Enter").Should().BeTrue();
        late.Answer(0, "Column");

        editor.Document.Text.Should().Be("Co\n");
        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AnIncompleteAnswer_IsAskedForAgainAsTheWordChanges_AndACompleteOneIsNot()
    {
        var server = new Provider { Incomplete = true };
        server.Items.Add(new CodeCompletionItem("Column"));
        var local = Offering("Colossus");
        var editor = Editor("", server, local);

        Type(editor, "Co");

        server.Asked.Should().HaveCount(2);
        server.Asked[1].Context.Trigger.Should().Be(CodeCompletionTrigger.Incomplete);
        server.Asked[1].Position.Should().Be(new CodePosition(0, 2));
        local.Asked.Should().ContainSingle();
        Labels(editor).Should().Equal("Colossus", "Column");
    }

    [Fact]
    public void AnAnswerOffTheUiThread_IsPostedToIt()
    {
        var dispatcher = new QueueDispatcher();
        UiDispatcher.Current = dispatcher;
        var editor = Editor("", Offering("Column"));

        Type(editor, "Co");
        editor.Completion.IsOpen.Should().BeFalse("the answer waits for the thread the list is read on");

        dispatcher.Drain();
        editor.Completion.IsOpen.Should().BeTrue();
        Labels(editor).Should().Equal("Column");
    }

    // ---- what providers can say ---------------------------------------------------------------------

    [Fact]
    public void AProviderThatThrows_IsReported_AndTheOthersStillShow()
    {
        var editor = Editor("",
            new Provider { Throws = new InvalidOperationException("at once") },
            new Provider { Faults = new InvalidOperationException("later") },
            Offering("Column"));
        var failures = new List<Exception>();
        editor.Completion.Failed += failures.Add;

        Type(editor, "Co");

        failures.Select(failure => failure.Message).Should().Equal("at once", "later");
        Labels(editor).Should().Equal("Column");
    }

    [Fact]
    public void ACancellationThatThrows_IsReported_AndTheKeyThatClosedTheListStillTypes()
    {
        var provider = new Provider { ThrowsOnCancel = true };
        provider.Items.Add(new CodeCompletionItem("Column"));
        var editor = Editor("", provider);
        var failures = new List<Exception>();
        editor.Completion.Failed += failures.Add;
        Type(editor, "Co");

        // The space ends the word, the list closes, and closing cancels the request.
        Type(editor, " ");

        editor.Document.Text.Should().Be("Co ");
        editor.Completion.IsOpen.Should().BeFalse();
        failures.Should().ContainSingle().Which.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().ContainSingle().Which.Message.Should().Be("the connection dropped");
    }

    [Fact]
    public void ACancellationThatThrows_IsReported_AndAnIncompleteAnswerIsStillAskedForAgain()
    {
        var provider = new Provider { Incomplete = true, ThrowsOnCancel = true };
        provider.Items.Add(new CodeCompletionItem("Column"));
        var editor = Editor("", provider);
        var failures = new List<Exception>();
        editor.Completion.Failed += failures.Add;
        Type(editor, "C");

        // Asking again cancels the request before it.
        Type(editor, "o");

        provider.Asked.Should().HaveCount(2, "the word changed and the answer was incomplete");
        failures.Should().ContainSingle();
        Labels(editor).Should().Equal("Column");
    }

    [Fact]
    public void ACommitCharacter_AcceptsTheSelectedEntry_AndIsTypedAfterIt()
    {
        var provider = new Provider { TriggerCharacters = ['.'] };
        provider.Items.Add(new CodeCompletionItem("Console", CodeCompletionKind.Class) { CommitCharacters = ['.', '('] });
        var editor = Editor("", provider);
        Type(editor, "Con");

        Type(editor, ".");

        editor.Document.Text.Should().Be("Console.");
        provider.Asked[^1].Context.Trigger.Should().Be(CodeCompletionTrigger.Character, "the dot then opened the members");
    }

    [Fact]
    public void ACommitCharacter_AnInputMethodCommits_AcceptsOverTheWordAsItWasBeforeTheComposition()
    {
        var provider = new Provider();
        provider.Items.Add(new CodeCompletionItem("Console", CodeCompletionKind.Class) { CommitCharacters = ['.'] });
        var editor = Editor("", provider);
        Type(editor, "Con");
        // An input method composes after the word, and commits a character the entry commits on.
        editor.SetComposition("s");
        editor.Completion.IsOpen.Should().BeTrue();

        editor.HandleText(".");

        editor.Document.Text.Should().Be("Console.");
        editor.Composition.Should().BeNull();
    }

    [Fact]
    public void APreselectedEntry_IsSelected_AmongThoseThatMatchBest()
    {
        var provider = new Provider();
        provider.Items.Add(new CodeCompletionItem("Alpha"));
        provider.Items.Add(new CodeCompletionItem("ApplyLater") { Preselect = true });
        var editor = Editor("", provider);

        editor.Completion.Invoke();
        SelectedLabel(editor).Should().Be("ApplyLater", "nothing typed, every entry matches as well as any other");

        Type(editor, "Al");
        Labels(editor).Should().Equal("Alpha", "ApplyLater");
        SelectedLabel(editor).Should().Be("Alpha", "the L of ApplyLater is a part's start after a gap, and matches worse");
    }

    [Fact]
    public void AWordTypedAwayToNothing_ClosesTheList_UnlessItOpenedWithNothingTyped()
    {
        var typed = Editor("", Offering("Column"));
        Type(typed, "Co");
        Key(typed, "Backspace");
        typed.Completion.IsOpen.Should().BeTrue();
        Key(typed, "Backspace");
        typed.Completion.IsOpen.Should().BeFalse();

        var members = new Provider { TriggerCharacters = ['.'] };
        members.Items.Add(new CodeCompletionItem("Theme"));
        var dotted = Editor("context", members);
        Type(dotted, ".Th");
        Key(dotted, "Backspace");
        Key(dotted, "Backspace");
        dotted.Completion.IsOpen.Should().BeTrue("the list opened right after the dot, with nothing typed");
        Labels(dotted).Should().Equal("Theme");
    }

    [Fact]
    public void AProvidersRange_IsReplaced_ItsEndMovedByWhatWasTypedSince()
    {
        var provider = new Provider();
        // Asked with the caret in "Fo|bar", the provider replaces the whole word, past the caret.
        provider.Items.Add(new CodeCompletionItem("FooBaz")
        {
            Replacing = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 5)),
        });
        var editor = Editor("Fobar", provider);
        editor.Selection = new CodeRange(new CodePosition(0, 2));
        Key(editor, " ", KeyModifiers.Command);

        Type(editor, "o");
        Key(editor, "Enter");

        editor.Document.Text.Should().Be("FooBaz");
        editor.Caret.Should().Be(new CodePosition(0, 6));
    }

    [Fact]
    public void AProvidersRange_StaysWhereItWas_WhenOnlyTheCaretMoves()
    {
        var provider = new Provider();
        // Asked with the caret in "Fo|bar", the provider replaces the whole word, past the caret.
        provider.Items.Add(new CodeCompletionItem("FooBaz")
        {
            Replacing = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 5)),
        });
        var editor = Editor("Fobar()", provider);
        editor.Selection = new CodeRange(new CodePosition(0, 2));
        Key(editor, " ", KeyModifiers.Command);

        // An arrow moves the caret and edits nothing: the list follows the word, and the range is the
        // provider's as it was.
        Key(editor, "ArrowLeft");
        editor.Completion.IsOpen.Should().BeTrue();
        Key(editor, "Enter");

        editor.Document.Text.Should().Be("FooBaz()");
        editor.Caret.Should().Be(new CodePosition(0, 6));
    }

    [Fact]
    public void TwoProvidersOfferingOneEntry_ListItOnce_AsTheFirstOneSaidIt()
    {
        var first = new Provider();
        first.Items.Add(new CodeCompletionItem("return", CodeCompletionKind.Keyword));
        var editor = Editor("", first, Offering("return", "result"));

        Type(editor, "re");

        editor.Completion.Items.Where(match => match.Item.Label == "return").Should().ContainSingle()
            .Which.Item.Kind.Should().Be(CodeCompletionKind.Keyword);
    }

    [Fact]
    public void ACopyThatDoesNotMatch_HidesNoCopyThatDoes()
    {
        // The first provider filters its Column by a text the word does not match; the second's
        // Column matches it, and is the one entry listed.
        var first = new Provider();
        first.Items.Add(new CodeCompletionItem("Column") { FilterText = "zzz" });
        var editor = Editor("", first, Offering("Column"));

        Type(editor, "Col");

        Labels(editor).Should().Equal("Column");
        editor.Completion.Items[0].Item.FilterText.Should().BeNull("it is the second provider's copy");
    }

    [Fact]
    public void TheSelectedEntry_IsResolvedOnce_AndShowsWhatItsProviderAdded()
    {
        var provider = new Provider { Resolver = item => item with { Documentation = $"about {item.Label}" } };
        provider.Items.Add(new CodeCompletionItem("Alpha"));
        provider.Items.Add(new CodeCompletionItem("Beta"));
        var editor = Editor("", provider);

        editor.Completion.Invoke();
        editor.Completion.Items[0].Item.Documentation.Should().Be("about Alpha");
        Key(editor, "ArrowDown");
        Key(editor, "ArrowUp");

        provider.Resolved.Should().Equal("Alpha", "Beta");
        editor.Completion.Items[1].Item.Documentation.Should().Be("about Beta");
    }

    // ---- the built-in providers -----------------------------------------------------------------

    [Fact]
    public async Task TheDocumentsWords_LeaveOutTheWordTyped_AndNumbers()
    {
        var document = CodeDocument.FromText("alpha beta 42 alpha\nbet");

        var list = await new CodeWordCompletionProvider().CompleteAsync(document, new CodePosition(1, 3),
            new CodeCompletionContext(CodeCompletionTrigger.Typing, CodeLanguages.PlainText), CancellationToken.None);

        list.Items.Select(item => item.Label).Should().Equal("alpha", "beta");
        list.Items.Should().OnlyContain(item => item.Kind == CodeCompletionKind.Text);
    }

    [Fact]
    public async Task TheDocumentsWords_AreReadNearestFirst_AndALongFileIsNotReadWhole()
    {
        // Two thousand lines of a hundred characters with their ends, each with a word of its own:
        // w0 to w1999. The word is typed at the start of the middle line.
        var document = CodeDocument.FromText(string.Join("\n",
            Enumerable.Range(0, 2000).Select(n => $"w{n}".PadRight(99))));

        var list = await new CodeWordCompletionProvider().CompleteAsync(document, new CodePosition(1000, 0),
            new CodeCompletionContext(CodeCompletionTrigger.Typing, CodeLanguages.PlainText), CancellationToken.None);

        var offered = list.Items.Select(item => int.Parse(item.Label[1..])).ToHashSet();
        offered.Should().Contain([999, 1001]).And.NotContain(1000, "it is the word being typed");
        offered.Should().NotContain([0, 1999], "the lines farthest from the caret are the ones left out");
        offered.Count.Should().BeLessThan(1000, "a part of the document is read, and not the whole of it");
        var reach = offered.Max(n => Math.Abs(n - 1000));
        Enumerable.Range(0, 2000).Where(n => n != 1000 && Math.Abs(n - 1000) < reach)
            .Should().OnlyContain(n => offered.Contains(n), "every line nearer the caret was read first");
    }

    [Fact]
    public async Task ALineLongerThanWhatAnAnswerReads_IsReadAroundTheCaret()
    {
        // One line of 40,000 words, about 270,000 characters, as a minified file has one: w0 to
        // w39999. The caret is at the start of w20000.
        var text = string.Join(" ", Enumerable.Range(0, 40000).Select(n => $"w{n}"));
        var caret = text.IndexOf(" w20000 ", StringComparison.Ordinal) + 1;

        var list = await new CodeWordCompletionProvider().CompleteAsync(CodeDocument.FromText(text),
            new CodePosition(0, caret),
            new CodeCompletionContext(CodeCompletionTrigger.Typing, CodeLanguages.PlainText), CancellationToken.None);

        var offered = list.Items.Select(item => item.Label).ToHashSet();
        offered.Should().Contain(["w19999", "w20001"]).And.NotContain("w20000", "it is the word being typed");
        offered.Should().NotContain(["w0", "w39999"], "the words far from the caret are the ones left out");
    }

    [Fact]
    public async Task TheLanguagesWords_AreOfferedWhereAWordStarts_AndNeverAfterADot()
    {
        var keywords = new CodeKeywordCompletionProvider();
        var context = new CodeCompletionContext(CodeCompletionTrigger.Typing, CodeLanguages.CSharp);

        var list = await keywords.CompleteAsync(CodeDocument.FromText("ret"), new CodePosition(0, 3), context,
            CancellationToken.None);
        var afterADot = await keywords.CompleteAsync(CodeDocument.FromText("x.ret"), new CodePosition(0, 5), context,
            CancellationToken.None);

        list.Items.Select(item => item.Label).Should().Contain(["return", "int", "null"],
            "the reserved words, the built-in types and the constants are one list");
        list.Items.Should().OnlyContain(item => item.Kind == CodeCompletionKind.Keyword);
        afterADot.Items.Should().BeEmpty();
    }

    [Fact]
    public void EveryLanguage_OffersTheWordsItColours()
    {
        CodeLanguages.Python.Keywords.Should().Contain(["def", "None", "str"]);
        CodeLanguages.Json.Keywords.Should().Equal("true", "false", "null");
        CodeLanguages.TypeScript.Keywords.Should().Contain(["function", "number", "undefined"]);
        CodeLanguages.PlainText.Keywords.Should().BeEmpty();
    }

    [Fact]
    public void TheBuiltIns_TogetherOfferTheLanguageAndTheDocument()
    {
        var editor = Editor("result = compute();\n", new CodeKeywordCompletionProvider(), new CodeWordCompletionProvider());

        Type(editor, "re");

        Labels(editor).Should().Contain(["readonly", "record", "ref", "required", "result", "return"]);
    }

    // ---- the real answer of a language service ----------------------------------------------------

    /// <summary>One answer Roslyn gave in the playground, recorded (Fixtures/roslyn-completions.fixture.json).</summary>
    private sealed record RecordedAnswer(string Name, string Line, int Caret, int ReplaceFrom, int ReplaceTo,
        List<RecordedItem> Items);

    private sealed record RecordedItem(string Label, string Kind, string? FilterText, string? SortText, bool Preselect);

    private sealed record Recording(string RecordedWith, List<RecordedAnswer> Answers);

    private static RecordedAnswer Recorded(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "tests", "eQuantic.UI.Native.Engine.Tests", "Fixtures",
            "roslyn-completions.fixture.json");
        var recording = JsonSerializer.Deserialize<Recording>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return recording.Answers.Single(answer => answer.Name == name);
    }

    /// <summary>
    /// Roslyn's answer played back as a provider would hand it over: its tags as kinds, its filter
    /// and sort texts, its preselection, and the span it replaces.
    /// </summary>
    private sealed class RecordedRoslyn(RecordedAnswer answer) : ICodeCompletionProvider
    {
        public IReadOnlyList<char> TriggerCharacters => ['.'];

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation)
        {
            position.Column.Should().Be(answer.Caret, "the recording answers the caret it was recorded at");
            var replacing = new CodeRange(new CodePosition(position.Line, answer.ReplaceFrom),
                new CodePosition(position.Line, answer.ReplaceTo));
            return Task.FromResult(new CodeCompletionList(answer.Items.Select(item =>
                new CodeCompletionItem(item.Label, KindOf(item.Kind))
                {
                    FilterText = item.FilterText,
                    SortText = item.SortText,
                    Preselect = item.Preselect,
                    Replacing = replacing,
                }).ToList()));
        }

        private static CodeCompletionKind KindOf(string tag) => tag switch
        {
            "Method" or "ExtensionMethod" => CodeCompletionKind.Method,
            "Property" => CodeCompletionKind.Property,
            "Field" => CodeCompletionKind.Field,
            "Class" => CodeCompletionKind.Class,
            "Structure" => CodeCompletionKind.Struct,
            "Interface" => CodeCompletionKind.Interface,
            "Enum" => CodeCompletionKind.Enum,
            "EnumMember" => CodeCompletionKind.EnumMember,
            "Delegate" => CodeCompletionKind.Function,
            "Keyword" => CodeCompletionKind.Keyword,
            "Namespace" => CodeCompletionKind.Module,
            "Local" or "Parameter" => CodeCompletionKind.Variable,
            "Constant" => CodeCompletionKind.Constant,
            "Event" => CodeCompletionKind.Event,
            _ => CodeCompletionKind.Text,
        };
    }

    [Fact]
    public void RoslynsRecordedAnswer_AfterADot_OffersTheMembers_AndAcceptingOneWritesIt()
    {
        var answer = Recorded("after-a-dot");
        var editor = Editor(answer.Line.Remove(answer.Caret - 1, 1), new RecordedRoslyn(answer));
        editor.Selection = new CodeRange(new CodePosition(0, answer.Caret - 1));

        Type(editor, ".");

        Labels(editor).Should().Contain("Theme").And.HaveCount(answer.Items.Count);
        // Text(...) wants a string, and Roslyn preselects what gives one.
        SelectedLabel(editor).Should().Be("ToString");

        Type(editor, "Th");
        Labels(editor).Should().Equal("Theme");
        Key(editor, "Enter");

        editor.Document.Text.Should().Be("        Text(context.Theme);");
        editor.Caret.Should().Be(new CodePosition(0, 26));
    }

    [Fact]
    public void RoslynsRecordedAnswer_InAnExpression_OffersTheFactory_AndAcceptingItReplacesTheWord()
    {
        var answer = Recorded("in-an-expression");
        answer.Items.Should().HaveCountGreaterThan(2000, "a real answer in an expression is the whole scope");
        var editor = Editor(answer.Line.Remove(answer.ReplaceFrom, answer.Caret - answer.ReplaceFrom),
            new RecordedRoslyn(answer));
        editor.Selection = new CodeRange(new CodePosition(0, answer.ReplaceFrom));

        // The word arrives whole, as an input method commits it, so it is asked for where it was recorded.
        editor.HandleText("Col");

        Labels(editor).Should().Contain("Column");
        // Equal matches go by the provider's sort text: what is in scope before what would need a using.
        Labels(editor).Take(2).Should().Equal("CollectionExtensions", "Column");

        Type(editor, "u");
        SelectedLabel(editor).Should().Be("Column");
        Key(editor, "Enter");

        editor.Document.Text.Should().Be("        Column;");
        editor.Caret.Should().Be(new CodePosition(0, 14));
    }
}
