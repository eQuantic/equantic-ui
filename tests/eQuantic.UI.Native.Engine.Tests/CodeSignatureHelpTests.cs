using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The panel that explains the call being written (<see cref="CodeSignatureHelpSession"/>), driven as a
/// person drives it: keys and typed text through the editor's own doors, and providers that answer at once,
/// late, wrongly or not at all. No host and no screen (#298).
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeSignatureHelpTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeSignatureHelpTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    // ---- the fixtures ---------------------------------------------------------------------------

    /// <summary>A provider whose answers come when the test says so.</summary>
    private sealed class LateProvider : ICodeSignatureProvider
    {
        private readonly List<TaskCompletionSource<CodeSignatureHelp?>> _pending = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
            CodeSignatureContext context, CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            var answer = new TaskCompletionSource<CodeSignatureHelp?>();
            _pending.Add(answer);
            return answer.Task;
        }

        public int Count => _pending.Count;

        public void Answer(int request, string label) =>
            _pending[request].SetResult(new CodeSignatureHelp([new CodeSignature(label)]));
    }

    /// <summary>A hover provider that explains every word, for a card over the panel.</summary>
    private sealed class Explainer : ICodeHoverProvider
    {
        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation) =>
            Task.FromResult<CodeHover?>(new CodeHover(document.WordAt(position), "explained"));
    }

    /// <summary>A provider that throws on every request.</summary>
    private sealed class Throwing : ICodeSignatureProvider
    {
        public Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
            CodeSignatureContext context, CancellationToken cancellation) =>
            throw new InvalidOperationException("the server crashed");
    }

    private static CallSignatures ColumnOnly() => CallSignatures.ColumnOnly();

    private static CallSignatures Overloaded() => CallSignatures.Overloaded();

    /// <summary>An editor whose panel asks again at once, so a test reads the answer on the next line.</summary>
    private static CodeEditorController Editor(string text, params ICodeSignatureProvider[] providers)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        foreach (var provider in providers) editor.SignatureHelp.Providers.Add(provider);
        editor.Selection = new CodeRange(editor.Document.End);
        return editor;
    }

    private static void Type(CodeEditorController editor, string text)
    {
        foreach (var c in text) editor.HandleText(c.ToString());
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Standard, null);

    // ---- opening --------------------------------------------------------------------------------

    [Fact]
    public void AnOpeningParenthesis_OpensThePanel_OnTheFirstParameter()
    {
        var provider = ColumnOnly();
        var editor = Editor("var c = ", provider);

        Type(editor, "Column(");

        editor.SignatureHelp.IsOpen.Should().BeTrue();
        editor.SignatureHelp.Signature!.Label.Should().Be("Column(float gap, MainAlign main)");
        editor.SignatureHelp.ActiveParameter.Should().Be(0);
        provider.Asked.Should().ContainSingle("only the parenthesis asked");
        provider.Asked[0].Context.Trigger.Should().Be(CodeSignatureTrigger.Character);
        provider.Asked[0].Context.Character.Should().Be('(');
        provider.Asked[0].Context.IsRetrigger.Should().BeFalse();
    }

    [Fact]
    public void CommandShiftSpace_AsksAtTheCaret()
    {
        var provider = ColumnOnly();
        var editor = Editor("var c = Column(1, ", provider);

        Press(editor, " ", KeyModifiers.Control | KeyModifiers.Shift).Should().BeTrue();

        editor.SignatureHelp.ActiveParameter.Should().Be(1);
        provider.Asked.Single().Context.Trigger.Should().Be(CodeSignatureTrigger.Invoked);
    }

    [Fact]
    public void WithNoProvider_NothingOpens_AndTheKeysAreTheHosts()
    {
        var editor = Editor("var c = ");

        Type(editor, "Column(");

        editor.SignatureHelp.IsOpen.Should().BeFalse();
        Press(editor, " ", KeyModifiers.Control | KeyModifiers.Shift).Should().BeFalse();
    }

    // ---- following the arguments ------------------------------------------------------------------

    /// <summary>What #298 accepts: walking <c>Column(</c> to a named argument moves the active parameter.</summary>
    [Fact]
    public void TypingTheArguments_MovesTheActiveParameter_ANamedOneIncluded()
    {
        var provider = ColumnOnly();
        var editor = Editor("var c = ", provider);
        Type(editor, "Column(");
        editor.SignatureHelp.ActiveParameter.Should().Be(0);

        Type(editor, "1, ");
        editor.SignatureHelp.ActiveParameter.Should().Be(1, "the comma moved to the second argument");
        provider.Asked.Last().Context.IsRetrigger.Should().BeTrue();

        Type(editor, "gap: ");
        editor.SignatureHelp.ActiveParameter.Should().Be(0, "the argument named gap is the first parameter");
    }

    [Fact]
    public async Task ABurstOfKeystrokes_AsksOnceAfterThem()
    {
        var provider = ColumnOnly();
        var editor = Editor("var c = ", provider);
        Type(editor, "Column(");
        editor.SignatureHelp.Delay = TimeSpan.FromMilliseconds(80);
        var before = provider.Asked.Count;

        Type(editor, "abc");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (provider.Asked.Count == before && DateTime.UtcNow < deadline) await Task.Delay(20);
        await Task.Delay(200);

        provider.Asked.Count.Should().Be(before + 1, "one request, after the burst");
        provider.Asked.Last().Position.Should().Be(new CodePosition(0, 18), "about the text the burst left");
        provider.Asked.Last().Context.Trigger.Should().Be(CodeSignatureTrigger.ContentChange);
    }

    [Fact]
    public void AClosingParenthesis_ClosesThePanel_WhenTheProviderAnswersNone()
    {
        var editor = Editor("var c = ", ColumnOnly());
        Type(editor, "Column(1");

        // The bracket typed over the one Type paired steps over it, and the caret is out of the call.
        Type(editor, ")");

        editor.SignatureHelp.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ANewerRequest_DropsTheOlderAnswer()
    {
        var late = new LateProvider();
        var editor = Editor("var c = ", late);
        Type(editor, "Column(");
        Type(editor, "1");
        late.Count.Should().Be(2);
        late.Tokens[0].IsCancellationRequested.Should().BeTrue("the newer request cancelled it");

        late.Answer(1, "the newer");
        late.Answer(0, "the older");

        editor.SignatureHelp.Signature!.Label.Should().Be("the newer");
    }

    [Fact]
    public void ASlowFirstProvider_HoldsBackNoOther_AndItsAnswerTakesItsPlace()
    {
        var slow = new LateProvider();
        var editor = Editor("var c = ", slow, ColumnOnly());

        Type(editor, "Column(");
        editor.SignatureHelp.Signature!.Label.Should().Be("Column(float gap, MainAlign main)",
            "the second provider answered, and the first has not");

        slow.Answer(0, "the first provider's");
        editor.SignatureHelp.Signature!.Label.Should().Be("the first provider's", "the first in order that answers wins");
    }

    [Fact]
    public void AProviderThatThrows_RaisesFailed_AndTheOthersStillShow()
    {
        var editor = Editor("var c = ", new Throwing(), ColumnOnly());
        var failures = new List<Exception>();
        editor.SignatureHelp.Failed += failures.Add;

        Type(editor, "Column(");

        failures.Should().ContainSingle().Which.Message.Should().Be("the server crashed");
        editor.SignatureHelp.IsOpen.Should().BeTrue();
    }

    // ---- the keys ---------------------------------------------------------------------------------

    [Fact]
    public void Escape_ClosesThePanel_AndTabStillIndents()
    {
        var editor = Editor("var c = ", ColumnOnly());
        Type(editor, "Column(");

        Press(editor, "Escape").Should().BeTrue("the panel took it");

        editor.SignatureHelp.IsOpen.Should().BeFalse();
        editor.TabMovesFocus.Should().BeFalse("the trap on Tab stays armed while a panel closes");
    }

    [Fact]
    public void TheArrows_WalkTheOverloads_RoundTheEnds()
    {
        var editor = Editor("x.", Overloaded());
        Type(editor, "Add(");
        var shown = new List<int>();

        foreach (var key in new[] { "ArrowDown", "ArrowDown", "ArrowUp", "ArrowUp", "ArrowUp" })
        {
            Press(editor, key).Should().BeTrue();
            shown.Add(editor.SignatureHelp.ActiveSignature);
        }

        shown.Should().Equal(1, 2, 1, 0, 2);
        editor.Caret.Should().Be(new CodePosition(0, 6), "the caret stayed in the call");
    }

    [Fact]
    public void WithOneSignature_TheArrowsMoveTheCaret()
    {
        var editor = Editor("var c = 1;\nvar d = ", ColumnOnly());
        Type(editor, "Column(");

        Press(editor, "ArrowUp").Should().BeTrue();

        editor.Caret.Line.Should().Be(0, "the arrow moved the caret");
    }

    /// <summary>⌥↑ and ⌥↓ walk the overloads beside a list, which keeps ↑ and ↓, as VS Code has it.</summary>
    [Fact]
    public void AltArrows_WalkTheOverloads_BesideAList()
    {
        var editor = Editor("x.", Overloaded());
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("name"), new CodeCompletionItem("named")));
        Type(editor, "Add(");
        Type(editor, "na");
        editor.Completion.IsOpen.Should().BeTrue("a word typed opens the list");
        var selected = editor.Completion.Selected;

        Press(editor, "ArrowDown", KeyModifiers.Alt).Should().BeTrue();

        editor.SignatureHelp.ActiveSignature.Should().Be(1, "⌥↓ walked to the next overload");
        editor.Completion.IsOpen.Should().BeTrue();
        editor.Completion.Selected.Should().Be(selected, "and the list kept its selection");
    }

    /// <summary>Beside a list of ONE entry, ↓ walks the overloads, as VS Code has it: its list claims the arrows
    /// only with more than one suggestion, and the panel claims them with more than one signature.</summary>
    [Fact]
    public void BesideAListOfOneEntry_TheArrowsWalkTheOverloads()
    {
        var editor = Editor("x.", Overloaded());
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("name")));
        Type(editor, "Add(");
        Type(editor, "na");
        editor.Completion.IsOpen.Should().BeTrue("a word typed opens the list");

        Press(editor, "ArrowDown").Should().BeTrue();

        editor.SignatureHelp.ActiveSignature.Should().Be(1, "↓ walked to the next overload");
        editor.Completion.IsOpen.Should().BeTrue("and the caret stayed in the word");
    }

    /// <summary>A card hides the panel, and the arrows are then the caret's: they walked the overloads of a
    /// panel nobody could see.</summary>
    [Fact]
    public void UnderACard_TheArrowsMoveTheCaret()
    {
        var editor = Editor("var a = 1;\nx.", Overloaded());
        editor.Hover.Providers.Add(new Explainer());
        Type(editor, "Add(");
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue();
        Press(editor, "i", KeyModifiers.Command).Should().BeTrue();
        editor.Hover.IsOpen.Should().BeTrue("the card hides the panel");
        var events = 0;
        editor.SignatureHelp.Changed += () => events++;

        Press(editor, "ArrowUp").Should().BeTrue();

        editor.Caret.Line.Should().Be(0, "the arrow moved the caret");
        events.Should().Be(1, "the panel closed when the caret left the call, and no overload was walked first");
    }

    [Fact]
    public void AChosenOverload_IsKept_WhileTheCallHasAsManySignatures()
    {
        var editor = Editor("x.", Overloaded());
        Type(editor, "Add(");
        Press(editor, "ArrowDown");
        editor.SignatureHelp.ActiveSignature.Should().Be(1);

        Type(editor, "1");

        editor.SignatureHelp.ActiveSignature.Should().Be(1, "the answer after the digit had as many signatures");
    }

    // ---- closing ----------------------------------------------------------------------------------

    [Fact]
    public void TheKeyboardLeaving_ClosesThePanel()
    {
        var editor = Editor("var c = ", ColumnOnly());
        Type(editor, "Column(");

        editor.FocusChanged(false);

        editor.SignatureHelp.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AnotherDocument_ClosesThePanel()
    {
        var editor = Editor("var c = ", ColumnOnly());
        Type(editor, "Column(");

        editor.Document = CodeDocument.FromText("class C { }");

        editor.SignatureHelp.IsOpen.Should().BeFalse();
    }

    /// <summary>A callback a provider registered on its request throws when Escape cancels it: that is
    /// the provider's failure, and Escape still closes the panel. It escaped from the key.</summary>
    [Fact]
    public void ACancellationThatThrows_IsReported_AndEscapeStillClosesThePanel()
    {
        var late = new LateProvider();
        var editor = Editor("var c = ", ColumnOnly(), late);
        var failures = new List<Exception>();
        editor.SignatureHelp.Failed += failures.Add;
        Type(editor, "Column(");
        editor.SignatureHelp.IsOpen.Should().BeTrue();
        late.Tokens[^1].Register(() => throw new InvalidOperationException("the connection dropped"));

        Press(editor, "Escape").Should().BeTrue();

        editor.SignatureHelp.IsOpen.Should().BeFalse();
        failures.Should().ContainSingle().Which.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().ContainSingle().Which.Message.Should().Be("the connection dropped");
    }

    /// <summary>With every provider taken out, the next move asks nobody and the panel closes: it asked
    /// nobody and waited, showing what the providers before had answered.</summary>
    [Fact]
    public void EveryProviderTakenOut_ClosesThePanel_OnTheNextMove()
    {
        var editor = Editor("var c = ", ColumnOnly());
        Type(editor, "Column(");
        editor.SignatureHelp.IsOpen.Should().BeTrue();

        editor.SignatureHelp.Providers.Clear();
        Type(editor, "1");

        editor.SignatureHelp.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AnUnansweredTrigger_LeavesNothingAsking()
    {
        var provider = ColumnOnly();
        var editor = Editor("var c = ", provider);
        Type(editor, "Unknown(");
        provider.Asked.Should().ContainSingle();

        Type(editor, "abc");

        provider.Asked.Should().ContainSingle("no panel shows and none was asked for since");
    }

    // ---- a parameter in its label ------------------------------------------------------------------

    [Theory]
    [InlineData("f(int x, int xy)", new[] { "int x", "int xy" }, 1, 9)]
    [InlineData("f(int x, int xy)", new[] { "int x", "int xy" }, 0, 2)]
    [InlineData("x(int x)", new[] { "x" }, 0, 6)]
    [InlineData("Column(float gap)", new[] { "MainAlign main" }, 0, -1)]
    public void AParameter_IsFoundInItsLabel_InOrder_AsAWholeWord(string label, string[] parameters, int index, int start)
    {
        var signature = new CodeSignature(label) { Parameters = [.. parameters.Select(p => new CodeSignatureParameter(p))] };

        signature.ParameterStart(index).Should().Be(start);
    }
}
