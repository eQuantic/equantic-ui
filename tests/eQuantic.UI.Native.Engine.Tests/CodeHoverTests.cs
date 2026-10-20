using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The card that explains the code (<see cref="CodeHoverSession"/>), driven through the editor's own
/// doors: a pointer passing over the surface and leaving it, keys, and providers that answer at once,
/// late or not at all. The grid is the default one (8 by 18), so the middle of the character at line L,
/// column C is (8C + 4, 18L + 9). No host and no screen (#1005).
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeHoverTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeHoverTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    // ---- the fixtures ---------------------------------------------------------------------------

    /// <summary>A provider that explains every word it is asked about, at once, and says what it was asked.</summary>
    private sealed class Provider : ICodeHoverProvider
    {
        public List<CodePosition> Asked { get; } = [];

        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation)
        {
            Asked.Add(position);
            var word = document.WordAt(position);
            return Task.FromResult<CodeHover?>(new CodeHover(word, $"about {document.TextIn(word)}")
            {
                Signature = $"class {document.TextIn(word)}",
            });
        }
    }

    /// <summary>A provider whose answers come when the test says so.</summary>
    private sealed class LateProvider : ICodeHoverProvider
    {
        private readonly List<(CodeDocument Document, CodePosition Position, TaskCompletionSource<CodeHover?> Answer)> _pending = [];

        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation)
        {
            var answer = new TaskCompletionSource<CodeHover?>();
            _pending.Add((document, position, answer));
            return answer.Task;
        }

        public int Count => _pending.Count;

        public void Answer(int request)
        {
            var (document, position, answer) = _pending[request];
            var word = document.WordAt(position);
            answer.SetResult(new CodeHover(word, $"about {document.TextIn(word)}"));
        }
    }

    /// <summary>A UI thread that is never the caller's, as a Photon host's is to a provider or a timer:
    /// everything is posted, and runs when drained.</summary>
    private sealed class QueueDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _queue = new();
        public bool IsOnUiThread => false;

        public void Post(Action work)
        {
            lock (_queue) _queue.Enqueue(work);
        }

        public void Drain()
        {
            while (true)
            {
                Action? work;
                lock (_queue)
                {
                    if (!_queue.TryDequeue(out work)) return;
                }
                work();
            }
        }
    }

    private static Point Over(int line, int column) => new(8 * column + 4, 18 * line + 9);

    /// <summary>An editor whose card shows the moment the pointer rests, so a test reads it on the next
    /// line.</summary>
    private static CodeEditorController Editor(string text)
    {
        var editor = new CodeEditorController(text);
        editor.Hover.Delay = TimeSpan.Zero;
        editor.Diagnostics.Delay = TimeSpan.Zero;
        return editor;
    }

    private static void Rest(CodeEditorController editor, int line, int column) =>
        editor.HandlePointer(PointerPhase.Hover, Over(line, column), KeyModifiers.None, 0);

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Standard, null);

    private static CodeDiagnostic Error(int line, int from, int to, string message) =>
        new(new CodeRange(new CodePosition(line, from), new CodePosition(line, to)), message)
        {
            Code = "CS0103",
            Source = "csharp",
        };

    // ---- the pointer ----------------------------------------------------------------------------

    [Fact]
    public void APointerAtRestOnAProblem_ShowsItsMessage()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([Error(0, 8, 9, "The name 'y' does not exist")]);

        Rest(editor, 0, 8);

        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.ByKeyboard.Should().BeFalse();
        var problem = editor.Hover.Problems.Single();
        problem.Message.Should().Be("The name 'y' does not exist");
        problem.Code.Should().Be("CS0103");
        problem.Source.Should().Be("csharp");
    }

    [Fact]
    public async Task APointerPassingOver_ShowsNothingAndAsksNothing()
    {
        var editor = new CodeEditorController("int x = y;");
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);
        editor.Hover.Delay = TimeSpan.FromMilliseconds(150);

        Rest(editor, 0, 1);
        editor.HandlePointer(PointerPhase.Exit, Over(0, 1), KeyModifiers.None, 0);
        await Task.Delay(400);

        editor.Hover.IsOpen.Should().BeFalse();
        provider.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task APointerAtRest_ShowsTheCardOnceTheDelayIsOver()
    {
        var editor = new CodeEditorController("int count = 0;");
        editor.Hover.Providers.Add(new Provider());
        editor.Hover.Delay = TimeSpan.FromMilliseconds(100);

        Rest(editor, 0, 5);
        editor.Hover.IsOpen.Should().BeFalse("the pointer has not rested long enough");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!editor.Hover.IsOpen && DateTime.UtcNow < deadline) await Task.Delay(20);

        editor.Hover.Answers.Single().Text.Should().Be("about count");
    }

    [Fact]
    public void AProblemAndAProvidersAnswer_ShowInOneCardTheProblemFirst()
    {
        var editor = Editor("var total = Sum(items);");
        editor.Hover.Providers.Add(new Provider());
        editor.Diagnostics.Report([
            new CodeDiagnostic(new CodeRange(new CodePosition(0, 12), new CodePosition(0, 15)), "Sum is obsolete",
                CodeDiagnosticSeverity.Warning),
        ]);

        Rest(editor, 0, 13);

        editor.Hover.Problems.Single().Message.Should().Be("Sum is obsolete");
        editor.Hover.Answers.Single().Signature.Should().Be("class Sum");
    }

    [Fact]
    public void TheWorstProblemComesFirst()
    {
        var editor = Editor("int x = y;");
        editor.Diagnostics.Report([
            new CodeDiagnostic(new CodeRange(new CodePosition(0, 8), new CodePosition(0, 9)), "a hint", CodeDiagnosticSeverity.Hint),
            new CodeDiagnostic(new CodeRange(new CodePosition(0, 8), new CodePosition(0, 9)), "an error"),
        ]);

        Rest(editor, 0, 8);

        editor.Hover.Problems.Select(problem => problem.Message).Should().Equal("an error", "a hint");
    }

    [Fact]
    public void AnAnswerAfterThePointerMovedOn_IsNotShown()
    {
        var editor = Editor("alpha beta");
        var provider = new LateProvider();
        editor.Hover.Providers.Add(provider);

        Rest(editor, 0, 1);
        Rest(editor, 0, 7);
        provider.Count.Should().Be(2);

        provider.Answer(0);
        editor.Hover.IsOpen.Should().BeFalse("the answer about alpha arrived after the pointer moved to beta");

        provider.Answer(1);
        editor.Hover.Answers.Single().Text.Should().Be("about beta");
    }

    [Fact]
    public void MovingInsideTheWord_KeepsTheCardAndAsksNothing()
    {
        var editor = Editor("Column(gap: 4)");
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);

        Rest(editor, 0, 0);
        Rest(editor, 0, 5);

        editor.Hover.IsOpen.Should().BeTrue();
        provider.Asked.Should().ContainSingle();
    }

    [Fact]
    public void RestingOnAnotherWord_ReplacesTheCard()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());

        Rest(editor, 0, 1);
        Rest(editor, 0, 7);

        editor.Hover.Answers.Single().Text.Should().Be("about beta");
    }

    [Fact]
    public void LeavingTheSurface_ClosesTheCard()
    {
        var editor = Editor("alpha");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);

        editor.HandlePointer(PointerPhase.Exit, Over(0, 1), KeyModifiers.None, 0);

        editor.Hover.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void RestingPastTheEndOfALine_ClosesTheCard()
    {
        var editor = Editor("alpha\nb");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);

        Rest(editor, 1, 4);

        editor.Hover.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void RestingOnWhiteSpace_ShowsNothing()
    {
        var editor = Editor("alpha  beta");
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);

        Rest(editor, 0, 6);

        editor.Hover.IsOpen.Should().BeFalse();
        provider.Asked.Should().BeEmpty();
    }

    [Fact]
    public void APress_ClosesTheCard()
    {
        var editor = Editor("alpha");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);

        editor.HandlePointer(PointerPhase.Down, Over(0, 0), KeyModifiers.None, 1);

        editor.Hover.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void TheCharacterUnderThePointer_IsTheOneItIsDrawnOn()
    {
        var editor = new CodeEditorController("ab\tc");

        editor.CharacterAt(new Point(12, 9)).Should().Be(new CodePosition(0, 1), "the right half of b is still b");
        editor.CharacterAt(new Point(30, 9)).Should().Be(new CodePosition(0, 2), "a tab is drawn up to its stop, the fourth cell");
        editor.CharacterAt(new Point(36, 9)).Should().Be(new CodePosition(0, 3));
        editor.CharacterAt(new Point(44, 9)).Should().BeNull("past the end of the line is over no character");
        editor.CharacterAt(new Point(4, 30)).Should().BeNull("under the last line is over no character");
    }

    // ---- the keyboard ---------------------------------------------------------------------------

    [Fact]
    public void CommandKThenI_ShowsTheCardForTheCaretWithoutWaiting()
    {
        var editor = new CodeEditorController("alpha beta");
        editor.Hover.Delay = TimeSpan.FromSeconds(10);
        editor.Hover.Providers.Add(new Provider());
        editor.Selection = new CodeRange(new CodePosition(0, 7));

        Press(editor, "k", KeyModifiers.Command).Should().BeTrue();
        Press(editor, "i", KeyModifiers.Command).Should().BeTrue();

        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.ByKeyboard.Should().BeTrue();
        editor.Hover.Answers.Single().Text.Should().Be("about beta");
    }

    [Fact]
    public void CommandKThenAnotherKey_HandlesThatKeyAsItself()
    {
        var editor = Editor("alpha beta");
        editor.Selection = new CodeRange(new CodePosition(0, 0));

        Press(editor, "k", KeyModifiers.Command);
        Press(editor, "ArrowRight");

        editor.Caret.Should().Be(new CodePosition(0, 1));
        editor.Hover.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Escape_ClosesTheCardAndTabStillIndents()
    {
        var editor = Editor("alpha");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);

        Press(editor, "Escape").Should().BeTrue("the card took it");

        editor.Hover.IsOpen.Should().BeFalse();
        editor.TabMovesFocus.Should().BeFalse("the Tab trap stays armed while a card closes");
    }

    [Fact]
    public void AKeyboardCard_ClosesWhenTheCaretMoves()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());
        editor.Selection = new CodeRange(new CodePosition(0, 1));
        Press(editor, "k", KeyModifiers.Command);
        Press(editor, "i", KeyModifiers.Command);

        Press(editor, "ArrowRight");

        editor.Hover.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AKeyboardCard_StaysWhileThePointerPassesOverNothing()
    {
        var editor = Editor("alpha\nb");
        editor.Hover.Providers.Add(new Provider());
        editor.Selection = new CodeRange(new CodePosition(0, 1));
        Press(editor, "k", KeyModifiers.Command);
        Press(editor, "i", KeyModifiers.Command);

        Rest(editor, 1, 5);

        editor.Hover.IsOpen.Should().BeTrue();
    }

    /// <summary>
    /// The list and the card are both drawn over the caret, and one of them at a time is: the list
    /// opening closes the card, so Escape closing the list does not bring back a card nobody asked for
    /// since.
    /// </summary>
    [Fact]
    public void TheListOpening_ClosesTheCard_WhichStaysClosedAfterIt()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("alphabet")));
        editor.Selection = new CodeRange(new CodePosition(0, 5));
        Press(editor, "k", KeyModifiers.Command);
        Press(editor, "i", KeyModifiers.Command);
        editor.Hover.IsOpen.Should().BeTrue();

        Press(editor, " ", KeyModifiers.Control).Should().BeTrue();

        editor.Completion.IsOpen.Should().BeTrue("⌃Space opens the list");
        editor.Hover.IsOpen.Should().BeFalse("which closes the card");
        Press(editor, "Escape").Should().BeTrue("Escape closes the list");
        editor.Hover.IsOpen.Should().BeFalse("and the card stays closed");
    }

    /// <summary>
    /// A card asked for again over one that shows, where nothing is there yet, closes, and says so: it
    /// said nothing, so a card the keyboard replaced stayed drawn, and stayed for good when no provider
    /// answered (found reviewing #1005).
    /// </summary>
    [Fact]
    public void AskingAgainOverACard_SaysItClosed_WhileNothingIsThereYet()
    {
        var editor = Editor("alpha beta");
        var late = new LateProvider();
        editor.Hover.Providers.Add(late);
        editor.Hover.ShowAt(new CodePosition(0, 1));
        late.Answer(0);
        editor.Hover.IsOpen.Should().BeTrue();
        var raised = 0;
        editor.Hover.Changed += () => raised++;

        editor.Hover.ShowAt(new CodePosition(0, 7));

        editor.Hover.IsOpen.Should().BeFalse("nothing has answered for the second word yet");
        raised.Should().Be(1, "the card that showed is gone, and whatever draws it has to hear so");
    }

    /// <summary>With no provider to ask and no problem to explain, ⌘K is the host's: a browser's Ctrl+K
    /// went to a chord that could show nothing.</summary>
    [Fact]
    public void CommandK_WithNothingACardCouldShow_IsLeftToTheHost()
    {
        var editor = Editor("alpha beta");

        Press(editor, "k", KeyModifiers.Command).Should().BeFalse();
    }

    /// <summary>A press between the two halves of ⌘K ⌘I ends the chord, so a later ⌘I is only ⌘I.</summary>
    [Fact]
    public void APressBetweenTheChordsHalves_EndsIt()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue();

        editor.HandlePointer(PointerPhase.Down, Over(0, 7), KeyModifiers.None, 1);
        editor.HandlePointer(PointerPhase.Up, Over(0, 7), KeyModifiers.None, 1);
        Press(editor, "i", KeyModifiers.Command);

        editor.Hover.IsOpen.Should().BeFalse();
    }

    /// <summary>
    /// A key something else takes ends the chord as well: ⌃Space and the list's own keys returned before
    /// the chord was read, so ⌘K, ⌃Space, Escape, ⌘I still showed the card.
    /// </summary>
    [Fact]
    public void AKeyTheListTakes_EndsTheChordHalfTyped()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("alphabet")));
        editor.Selection = new CodeRange(new CodePosition(0, 5));
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue();

        Press(editor, " ", KeyModifiers.Control).Should().BeTrue("⌃Space opens the list");
        Press(editor, "Escape").Should().BeTrue("and Escape closes it");
        Press(editor, "i", KeyModifiers.Command);

        editor.Hover.IsOpen.Should().BeFalse("the chord ended at ⌃Space");
    }

    /// <summary>
    /// While the list shows, the pointer resting on the code asks nothing, so no card waits behind the list
    /// to come out when it goes (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public void WhileTheListShows_APointerAtRestAsksNothing_AndNoCardFollowsTheList()
    {
        var editor = Editor("alpha beta");
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("alphabet")));
        editor.Selection = new CodeRange(new CodePosition(0, 5));
        Press(editor, " ", KeyModifiers.Control).Should().BeTrue();
        editor.Completion.IsOpen.Should().BeTrue();

        Rest(editor, 0, 7);

        provider.Asked.Should().BeEmpty("the pointer resting while the list shows asks nothing");
        Press(editor, "Escape").Should().BeTrue();
        editor.Completion.IsOpen.Should().BeFalse();
        editor.Hover.IsOpen.Should().BeFalse("no card comes out from behind the list");
    }

    /// <summary>
    /// The word asked about stays the card's while its provider answers: moving inside it asks nothing
    /// again. It was forgotten when the asking began, so every move restarted the wait and a slow provider's
    /// card never came (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public void MovingInsideAWordWhileItsProviderAnswers_AsksNothingAgain()
    {
        var editor = Editor("alpha beta");
        var late = new LateProvider();
        editor.Hover.Providers.Add(late);
        Rest(editor, 0, 7);
        late.Count.Should().Be(1);

        Rest(editor, 0, 8);
        Rest(editor, 0, 6);

        late.Count.Should().Be(1, "the word is the one asked about, and its answer is on its way");
        late.Answer(0);
        editor.Hover.IsOpen.Should().BeTrue();
    }

    /// <summary>
    /// The wait for the pointer to rest ends on a timer's thread, and the card is asked for from the UI
    /// thread once it runs what the wait posted, after reading there that nothing dismissed it since: off
    /// it, a pointer leaving while the wait ended was overwritten (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public async Task TheWaitForThePointerToRest_EndsOnTheUiThread()
    {
        var queue = new QueueDispatcher();
        UiDispatcher.Current = queue;
        var editor = new CodeEditorController("alpha beta");
        editor.Hover.Delay = TimeSpan.FromMilliseconds(20);
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);

        Rest(editor, 0, 1);
        await Task.Delay(200);

        provider.Asked.Should().BeEmpty("nothing is asked off the UI thread");
        editor.HandlePointer(PointerPhase.Exit, Point.Zero, KeyModifiers.None, 0);
        queue.Drain();
        provider.Asked.Should().BeEmpty("the pointer left before the UI thread ran the wait's end");
        editor.Hover.IsOpen.Should().BeFalse();
    }

    /// <summary>
    /// The pointer leaving cancels the request it made itself, and leaves a card the keyboard showed: over
    /// an F8 card, a pointer resting on another word and leaving before its wait ended replaced the card
    /// once the wait ended (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public async Task APointerLeavingBeforeItsWaitEnds_DropsItsRequest_AndTheKeyboardsCardStays()
    {
        var editor = new CodeEditorController("alpha beta");
        editor.Hover.Delay = TimeSpan.FromMilliseconds(60);
        editor.Diagnostics.Report([Error(0, 0, 5, "bad")]);
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);
        editor.Selection = new CodeRange(new CodePosition(0, 7));
        Press(editor, "F8").Should().BeTrue();
        editor.Hover.ByKeyboard.Should().BeTrue();

        Rest(editor, 0, 7);
        editor.HandlePointer(PointerPhase.Exit, Point.Zero, KeyModifiers.None, 0);
        await Task.Delay(250);

        provider.Asked.Should().BeEmpty("the pointer's request went with the pointer");
        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.ByKeyboard.Should().BeTrue("the card F8 showed stays");
        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("bad");
    }

    /// <summary>…and a request the keyboard made is the keyboard's: the pointer leaving while its provider
    /// answers cancels nothing (found by Copilot reviewing #1008).</summary>
    [Fact]
    public void APointerLeaving_LeavesAKeyboardRequestOnItsWay()
    {
        var editor = Editor("alpha beta");
        var late = new LateProvider();
        editor.Hover.Providers.Add(late);
        editor.Selection = new CodeRange(new CodePosition(0, 1));
        Press(editor, "k", KeyModifiers.Command);
        Press(editor, "i", KeyModifiers.Command);
        late.Count.Should().Be(1);

        editor.HandlePointer(PointerPhase.Exit, Point.Zero, KeyModifiers.None, 0);
        late.Answer(0);

        editor.Hover.IsOpen.Should().BeTrue("the keyboard's request was not the pointer's to cancel");
    }

    /// <summary>
    /// A word ends before the character after it, as <c>WordAt</c> answers it: the pointer moving onto the
    /// space after <c>alpha</c> is off the word, and its card closes (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public void ThePointerMovingOntoTheSpaceAfterAWord_ClosesItsCard()
    {
        var editor = Editor("alpha beta");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);
        editor.Hover.IsOpen.Should().BeTrue();

        Rest(editor, 0, 5);

        editor.Hover.IsOpen.Should().BeFalse();
    }

    /// <summary>…and a request still on its way for the word is dropped there.</summary>
    [Fact]
    public void ThePointerMovingOntoTheSpaceAfterAWord_DropsItsRequest()
    {
        var editor = Editor("alpha beta");
        var late = new LateProvider();
        editor.Hover.Providers.Add(late);
        Rest(editor, 0, 1);

        Rest(editor, 0, 5);
        late.Answer(0);

        editor.Hover.IsOpen.Should().BeFalse("the answer is for a word the pointer left");
    }

    // ---- the problems changing under a card ---------------------------------------------------------

    /// <summary>
    /// A problem reported after the card showed joins it: the pointer's wait (300 ms) ends before the
    /// problems' pause (500 ms), so the first card held only the providers' answers, and moving inside the
    /// word asks nothing again (found by Copilot reviewing #1008).
    /// </summary>
    [Fact]
    public void AProblemReportedAfterTheCardShowed_JoinsIt_AndNoProviderIsAskedAgain()
    {
        var editor = Editor("alpha beta");
        var provider = new Provider();
        editor.Hover.Providers.Add(provider);
        Rest(editor, 0, 1);
        editor.Hover.Answers.Should().ContainSingle();
        editor.Hover.Problems.Should().BeEmpty("the problems have not come yet");

        editor.Diagnostics.Report([Error(0, 0, 5, "late")]);

        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("late");
        editor.Hover.Answers.Should().ContainSingle("the answer stays");
        provider.Asked.Should().ContainSingle("the providers are not asked again");
    }

    /// <summary>A problem replaced under a card is the one the card shows.</summary>
    [Fact]
    public void AProblemReplacedUnderACard_IsTheOneItShows()
    {
        var editor = Editor("alpha beta");
        editor.Diagnostics.Report([Error(0, 0, 5, "first")]);
        Press(editor, "F8").Should().BeTrue();

        editor.Diagnostics.Report([Error(0, 0, 5, "second")]);

        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("second");
    }

    /// <summary>The problem a card showed alone, taken away, closes the card, and whatever draws it hears
    /// so.</summary>
    [Fact]
    public void TheProblemACardShowedAlone_TakenAway_ClosesIt()
    {
        var editor = Editor("alpha beta");
        editor.Diagnostics.Report([Error(0, 0, 5, "gone")]);
        Press(editor, "F8").Should().BeTrue();
        editor.Hover.IsOpen.Should().BeTrue();
        var raised = 0;
        editor.Hover.Changed += () => raised++;

        editor.Diagnostics.Report([]);

        editor.Hover.IsOpen.Should().BeFalse();
        raised.Should().Be(1);
    }

    /// <summary>A problem that arrives while the providers are still answering opens the card at once, and
    /// the answer still lands in it.</summary>
    [Fact]
    public void AProblemBeforeAnyAnswer_OpensTheCard_AndTheAnswerStillLands()
    {
        var editor = Editor("alpha beta");
        var late = new LateProvider();
        editor.Hover.Providers.Add(late);
        Rest(editor, 0, 1);
        editor.Hover.IsOpen.Should().BeFalse("nothing is there to show yet");

        editor.Diagnostics.Report([Error(0, 0, 5, "early")]);
        editor.Hover.IsOpen.Should().BeTrue("a problem is there to show");
        late.Answer(0);

        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("early");
        editor.Hover.Answers.Should().ContainSingle("the answer landed in the same card");
        late.Count.Should().Be(1, "the provider was not asked again");
    }

    /// <summary>The problems changing during the pointer's wait change nothing until it ends, when the card
    /// reads them as they are.</summary>
    [Fact]
    public async Task TheProblemsChangingDuringTheWait_AreReadWhenItEnds()
    {
        var editor = new CodeEditorController("alpha beta");
        editor.Hover.Delay = TimeSpan.FromMilliseconds(60);
        editor.Diagnostics.Delay = TimeSpan.Zero;
        var raised = 0;
        editor.Hover.Changed += () => raised++;
        Rest(editor, 0, 1);

        editor.Diagnostics.Report([Error(0, 0, 5, "during")]);
        raised.Should().Be(0, "the wait has not ended");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!editor.Hover.IsOpen && DateTime.UtcNow < deadline) await Task.Delay(10);

        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("during");
    }

    /// <summary>A problem ends before the character at its end, as a word does: the pointer on the space
    /// after it rests on nothing, while the caret there, between the two, is at the problem.</summary>
    [Fact]
    public void AProblemEndingWhereThePointerRests_IsNotUnderIt_AndIsAtTheCaretThere()
    {
        var editor = Editor("name = 1");
        editor.Diagnostics.Report([Error(0, 0, 4, "bad")]);

        Rest(editor, 0, 4);
        editor.Hover.IsOpen.Should().BeFalse("the pointer is on the space after the problem");

        editor.Selection = new CodeRange(new CodePosition(0, 4));
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue();
        Press(editor, "i", KeyModifiers.Command).Should().BeTrue();

        editor.Hover.IsOpen.Should().BeTrue();
        editor.Hover.Problems.Should().ContainSingle().Which.Message.Should().Be("bad");
    }

    [Fact]
    public void Typing_ClosesTheCard()
    {
        var editor = Editor("alpha");
        editor.Hover.Providers.Add(new Provider());
        Rest(editor, 0, 1);

        editor.HandleText("x");

        editor.Hover.IsOpen.Should().BeFalse();
    }
}
