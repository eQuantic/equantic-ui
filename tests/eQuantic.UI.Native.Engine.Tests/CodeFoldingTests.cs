using eQuantic.UI.Code;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's folding (#1119), driven as a person drives it, with no screen: the regions as VS Code's indentation
/// strategy and <c>sanitizeAndMerge</c> make them, its seven keys in both conventions, and a fold that never hides
/// what is worked on: an edit and the caret reaching inside unfold it, and a copy takes what it hides.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeFoldingTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeFoldingTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private const KeyModifiers Command = KeyModifiers.Command;
    private const KeyModifiers Shift = KeyModifiers.Shift;
    private const KeyModifiers Alt = KeyModifiers.Alt;

    /// <summary>
    /// <c>class A</c> with a method <c>B</c>, braces on lines of their own: by indentation, a region from the class's
    /// <c>{</c> on line 1 to line 5, and one from the method's on line 3 to line 4.
    /// </summary>
    private const string Source = "class A\n{\n    void B()\n    {\n        x();\n    }\n}";

    private static CodeEditorController Editor(string text = Source, int caretLine = 0)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Folding.Delay = TimeSpan.Zero;
        editor.Selection = new CodeRange(new CodePosition(caretLine, 0));
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None,
        KeyboardConvention convention = KeyboardConvention.Apple) =>
        editor.HandleKey(key, modifiers, convention, null);

    private static IEnumerable<int> Hidden(CodeEditorController editor) =>
        Enumerable.Range(0, editor.Document.LineCount).Where(editor.Folding.IsHidden);

    private sealed class Fixed(params CodeFold[] folds) : ICodeFoldProvider
    {
        public int Asked;

        public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation)
        {
            Asked++;
            return Task.FromResult<IReadOnlyList<CodeFold>>(folds);
        }
    }

    private sealed class Waiting : ICodeFoldProvider
    {
        public TaskCompletionSource<IReadOnlyList<CodeFold>> Answer = new();

        public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation) =>
            Answer.Task;
    }

    /// <summary>A UI thread that is never the caller's, as a Photon host's is to a timer: everything is posted, and
    /// runs when drained.</summary>
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

    private sealed class Throwing : ICodeFoldProvider
    {
        public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation) =>
            throw new InvalidOperationException("the language server timed out");
    }

    private sealed class Clipboard : ITextClipboard
    {
        public string Text = "";

        public void Write(string text) => Text = text;

        public string Read() => Text;
    }

    // ---- the regions ------------------------------------------------------------------------------------

    [Fact]
    public void WithNoProvider_TheIndentationFolds()
    {
        var editor = Editor();

        editor.Folding.Regions.Should().Equal(new CodeFold(1, 5), new CodeFold(3, 4));
    }

    [Fact]
    public async Task AnIndentedBlock_FoldsUnderItsHeader_ForALanguageNobodyParses()
    {
        var document = CodeDocument.FromText("def run():\n    a = 1\n    b = 2\nprint(1)");

        var folds = await new IndentationFoldProvider().FoldsAsync(document, CancellationToken.None);

        folds.Should().Equal([new CodeFold(0, 2)], "the region ends where the indentation comes back");
    }

    [Fact]
    public async Task ATabAndAsManySpaces_IndentToTheSameDepth()
    {
        // A tab is drawn to its stop, four columns here, so the second line and the third stand side by side: as
        // one column, the tab made the second line a header hiding the third.
        var document = CodeDocument.FromText("a\n\tb\n    c");

        var folds = await new IndentationFoldProvider(4).FoldsAsync(document, CancellationToken.None);

        folds.Should().Equal([new CodeFold(0, 2)]);
        Editor("a\n\tb\n    c").Folding.Regions.Should().Equal([new CodeFold(0, 2)], "the editor's own tab size");
    }

    [Fact]
    public void TwoRegionsOnOneHeader_KeepTheOneReachingFurthest_AndOneCrossingAnotherIsDropped()
    {
        var editor = Editor("a\nb\nc\nd\ne\nf\ng\nh\ni\nj");
        editor.Folding.Providers.Add(new Fixed(new CodeFold(2, 5), new CodeFold(2, 8), new CodeFold(4, 9), new CodeFold(6, 6)));

        editor.Folding.Check();

        editor.Folding.Regions.Should().Equal(new CodeFold(2, 8));
    }

    [Fact]
    public void AnAnswerAfterTheTextChangedAgain_IsDropped()
    {
        var editor = Editor();
        var waiting = new Waiting();
        editor.Folding.Providers.Add(waiting);
        editor.Folding.Check();
        var stale = waiting.Answer;

        waiting.Answer = new TaskCompletionSource<IReadOnlyList<CodeFold>>();
        editor.Apply(new CodeRange(new CodePosition(6, 1)), "\n");
        stale.SetResult([new CodeFold(0, 2)]);
        waiting.Answer.SetResult([new CodeFold(1, 5)]);

        editor.Folding.Regions.Should().Equal(new CodeFold(1, 5));
    }

    // ---- folding and unfolding --------------------------------------------------------------------------

    [Fact]
    public void FoldingTheMethod_HidesItsBody_AndTheCaretStepsOverIt()
    {
        var editor = Editor(caretLine: 4);

        Press(editor, "[", Alt | Command).Should().BeTrue();

        Hidden(editor).Should().Equal(4);
        editor.Selection = new CodeRange(new CodePosition(3, 0));
        Press(editor, "ArrowDown");
        editor.Selection.Should().Be(new CodeRange(new CodePosition(5, 0)), "↓ steps over the hidden line");
        editor.Selection = new CodeRange(new CodePosition(3, 0));
        Press(editor, "]", Alt | Command).Should().BeTrue();
        Hidden(editor).Should().BeEmpty();
    }

    [Fact]
    public void AFoldThatHidesTheCaret_PutsItAtTheEndOfTheHeader()
    {
        var editor = Editor(caretLine: 4);

        Press(editor, "[", Alt | Command);

        editor.Selection.Should().Be(new CodeRange(new CodePosition(3, 5)), "as VS Code moves its cursor out of what it folds");
        editor.Folding.IsFolded(3).Should().BeTrue("and the move does not unfold it again");
    }

    [Fact]
    public void AFoldOverASelectionsAnchor_PutsTheAnchorAtTheEndOfTheHeader_AndKeepsItsDirection()
    {
        var editor = Editor();
        editor.Selection = new CodeRange(new CodePosition(4, 2), new CodePosition(6, 1));

        editor.Folding.Toggle(3);

        editor.Selection.Should().Be(new CodeRange(new CodePosition(3, 5), new CodePosition(6, 1)));
        Press(editor, "ArrowUp", Shift);
        editor.Folding.IsFolded(3).Should().BeTrue("the end that moves stays on lines that show");
    }

    [Fact]
    public void AFoldAboveTheView_KeepsTheLineAtItsTopThere()
    {
        // VS Code keeps its viewport's first line when what it hides changes: a fold of ninety lines above the view
        // asks the view to scroll up by them, so the same lines and the caret among them stay in it.
        var editor = Editor(string.Join("\n", Enumerable.Range(0, 200).Select(i => $"line {i}")), caretLine: 160);
        editor.Folding.Providers.Add(new Fixed(new CodeFold(10, 100)));
        editor.Folding.Check();
        var origin = editor.Grid.Origin.Y;
        var line = editor.Grid.Cell.Height;
        editor.View = new Rect(0, origin + 150 * line, 600, 400);

        editor.Folding.Toggle(10);

        editor.VerticalScroll!.Offset.Should().BeApproximately(origin + 60 * line, 0.01f);
    }

    [Fact]
    public void FoldAll_AndUnfoldAll_AreChordsOfTwoKeys()
    {
        var editor = Editor();

        Press(editor, "k", Command).Should().BeTrue();
        Press(editor, "0", Command).Should().BeTrue();
        editor.Folding.IsFolded(1).Should().BeTrue();
        editor.Folding.IsFolded(3).Should().BeTrue();
        Hidden(editor).Should().Equal(2, 3, 4, 5);

        Press(editor, "k", Command);
        Press(editor, "j", Command).Should().BeTrue();
        Hidden(editor).Should().BeEmpty();
    }

    [Fact]
    public void FoldRecursively_FoldsTheRegionAndEveryOneInsideIt()
    {
        var editor = Editor(caretLine: 2);

        Press(editor, "k", Command);
        Press(editor, "[", Command).Should().BeTrue();
        editor.Folding.IsFolded(1).Should().BeTrue();
        editor.Folding.IsFolded(3).Should().BeTrue();

        Press(editor, "k", Command);
        Press(editor, "]", Command).Should().BeTrue();
        editor.Folding.IsFolded(1).Should().BeFalse();
        editor.Folding.IsFolded(3).Should().BeFalse();
    }

    [Fact]
    public void ToggleFold_FoldsAndUnfolds_TheInnermostRegion()
    {
        var editor = Editor(caretLine: 4);

        Press(editor, "k", Command);
        Press(editor, "l", Command).Should().BeTrue();
        editor.Folding.IsFolded(3).Should().BeTrue();
        editor.Folding.IsFolded(1).Should().BeFalse();
        editor.Folding.Toggle(1).Should().BeTrue("a press on the class's chevron");
        Hidden(editor).Should().Equal(2, 3, 4, 5);
    }

    [Fact]
    public void WithNoRegionAtTheCaret_TheKeyIsNotClaimed()
    {
        var editor = Editor(caretLine: 0);

        Press(editor, "[", Alt | Command).Should().BeFalse();
        Press(editor, "]", Alt | Command).Should().BeFalse();
        editor.CanExecute(CodeCommand.UnfoldAll).Should().BeFalse();
    }

    [Theory]
    [InlineData("[", "]")]
    [InlineData("{", "}")]
    public void OnWindowsAndLinux_CtrlShiftBracketsFold(string fold, string unfold)
    {
        // Ctrl is Command there, and ⇧ makes the bracket key a brace in a browser.
        var editor = Editor(caretLine: 4);

        Press(editor, fold, Shift | Command, KeyboardConvention.Standard).Should().BeTrue();
        Hidden(editor).Should().Equal(4);
        Press(editor, unfold, Shift | Command, KeyboardConvention.Standard).Should().BeTrue();
        Hidden(editor).Should().BeEmpty();
    }

    [Fact]
    public void OnAMac_TheBracketKeyTypedWithOption_FoldsToo()
    {
        var editor = Editor(caretLine: 4);

        Press(editor, "“", Alt | Command).Should().BeTrue();
        Hidden(editor).Should().Equal(4);
        Press(editor, "‘", Alt | Command).Should().BeTrue();
        Hidden(editor).Should().BeEmpty();
    }

    // ---- what is worked on ------------------------------------------------------------------------------

    [Fact]
    public void ACaretPutOnAHiddenLine_UnfoldsWhatHidesIt()
    {
        var editor = Editor();
        editor.Folding.Toggle(1);

        editor.Selection = new CodeRange(new CodePosition(4, 2));

        editor.Folding.IsFolded(1).Should().BeFalse();
        Hidden(editor).Should().BeEmpty();
    }

    [Fact]
    public void ASelectionAcrossAFold_KeepsItFolded_AndACopyTakesWhatItHides()
    {
        var editor = Editor();
        editor.Folding.Toggle(1);
        var clipboard = new Clipboard();

        editor.Selection = new CodeRange(new CodePosition(1, 0), new CodePosition(6, 1));
        editor.Execute(CodeCommand.Copy, clipboard).Should().BeTrue();

        editor.Folding.IsFolded(1).Should().BeTrue("the selection ends on lines that show");
        clipboard.Text.Should().Be("{\n    void B()\n    {\n        x();\n    }\n}");
    }

    [Fact]
    public void ALineInsertedAbove_CarriesTheFold()
    {
        var editor = Editor();
        editor.Folding.Providers.Add(new Waiting());
        editor.Folding.Toggle(1);

        editor.Apply(new CodeRange(new CodePosition(0, 0)), "// top\n");

        editor.Folding.IsFolded(2).Should().BeTrue();
        Hidden(editor).Should().Equal(3, 4, 5, 6);
    }

    [Fact]
    public void AnEditOnAHiddenLine_UnfoldsTheRegion()
    {
        var editor = Editor();
        editor.Folding.Providers.Add(new Waiting());
        editor.Folding.Toggle(1);
        editor.Selection = new CodeRange(new CodePosition(0, 0));

        editor.Apply(new CodeRange(new CodePosition(4, 8), new CodePosition(4, 9)), "y");

        editor.Folding.IsFolded(1).Should().BeFalse();
    }

    [Fact]
    public void AHeaderJoinedToTheLineAbove_TakesItsRegionAway()
    {
        var editor = Editor();
        editor.Folding.Providers.Add(new Waiting());

        editor.Apply(new CodeRange(new CodePosition(0, 7), new CodePosition(1, 0)), " ");

        editor.Folding.Regions.Should().Equal([new CodeFold(2, 3)], "the class's region went with its header, the method's moved up");
    }

    [Fact]
    public void TheLinesAboveAHeaderDeleted_LeaveItsRegion()
    {
        var editor = Editor();
        editor.Folding.Providers.Add(new Waiting());
        editor.Folding.Toggle(3);
        editor.Selection = new CodeRange(new CodePosition(0, 0));

        editor.Apply(new CodeRange(new CodePosition(0, 0), new CodePosition(1, 0)), "");

        editor.Folding.Regions.Should().Equal(new CodeFold(0, 4), new CodeFold(2, 3));
        editor.Folding.IsFolded(2).Should().BeTrue("the edit reached no hidden line");
    }

    [Fact]
    public void ACaretOfSeveral_PutOnAHiddenLine_UnfoldsWhatHidesIt()
    {
        var editor = Editor();
        editor.Folding.Toggle(3);

        editor.Selections = [new CodeRange(new CodePosition(0, 0)), new CodeRange(new CodePosition(4, 0))];

        editor.Folding.IsFolded(3).Should().BeFalse();
    }

    [Fact]
    public void AFoldThatHidesACaretOfSeveral_PutsItAtTheEndOfTheHeader()
    {
        var editor = Editor();
        editor.Selections = [new CodeRange(new CodePosition(0, 0)), new CodeRange(new CodePosition(4, 2))];

        editor.Folding.Fold(4).Should().BeTrue();

        editor.Selections.Should().Equal(new CodeRange(new CodePosition(0, 0)), new CodeRange(new CodePosition(3, 5)));
        editor.Folding.IsFolded(3).Should().BeTrue();
    }

    [Fact]
    public void TheHeaderLineDeletedFromItsFirstColumn_TakesItsRegionAway()
    {
        var editor = Editor();
        editor.Folding.Providers.Add(new Waiting());

        editor.Apply(new CodeRange(new CodePosition(1, 0), new CodePosition(2, 0)), "");

        editor.Folding.Regions.Should().Equal([new CodeFold(2, 3)], "the class's header went, the method's moved up");
    }

    [Fact]
    public void AProviderThatFails_LeavesTheRegionsAndTheFoldsAsTheyWere()
    {
        var editor = Editor();
        editor.Folding.Toggle(3);
        var failures = new List<Exception>();
        editor.Folding.Failed += failures.Add;
        editor.Folding.Providers.Add(new Throwing());

        editor.Folding.Check();

        failures.Should().ContainSingle();
        editor.Folding.Regions.Should().Equal(new CodeFold(1, 5), new CodeFold(3, 4));
        editor.Folding.IsFolded(3).Should().BeTrue();
    }

    [Fact]
    public void AGridWithRowsOfItsOwn_HidesNoFold_AndTheKeysClaimNothing()
    {
        // A diff's side brings its own rows: a fold there would move the caret and hide nothing.
        var editor = Editor(caretLine: 4);
        editor.Grid = editor.Grid with { Rows = new CodeRows(editor.Document.LineCount, [], []) };

        Press(editor, "[", Alt | Command).Should().BeFalse();
        editor.CanExecute(CodeCommand.FoldAll).Should().BeFalse();
        editor.Selection.Should().Be(new CodeRange(new CodePosition(4, 0)));
    }

    /// <summary>The pause before asking ends on a timer's thread, and the providers are asked from the UI thread
    /// once it runs what the pause posted, about the document as it stands there.</summary>
    [Fact]
    public async Task ThePauseBeforeAsking_EndsOnTheUiThread()
    {
        var queue = new QueueDispatcher();
        UiDispatcher.Current = queue;
        var editor = new CodeEditorController(Source, CodeLanguages.CSharp);
        editor.Folding.Delay = TimeSpan.FromMilliseconds(20);
        var provider = new Fixed(new CodeFold(1, 5));
        editor.Folding.Providers.Add(provider);
        editor.Folding.Check();
        queue.Drain();
        provider.Asked = 0;

        editor.Apply(new CodeRange(new CodePosition(6, 1)), "\n");
        await Task.Delay(200);

        provider.Asked.Should().Be(0, "nothing is asked off the UI thread");
        queue.Drain();
        provider.Asked.Should().Be(1);
    }

    [Fact]
    public void AGridWhoseGeometryAloneChanged_StillHidesTheFolds()
    {
        var editor = Editor(caretLine: 0);
        editor.Folding.Toggle(1);

        editor.Folding.Toggle(3);
        editor.Grid.Rows!.RowCount.Should().Be(3);

        editor.Grid = editor.Grid with { Cell = new Size(8, 20) };
        editor.Folding.Toggle(1);

        editor.Grid.Rows!.RowCount.Should().Be(6, "the class opened, the method's body is folded still, and the rows follow");
        Press(editor, "k", Command);
        Press(editor, "j", Command).Should().BeTrue("the fold keys still act");
        editor.Grid.Rows.Should().BeNull();
    }

    [Fact]
    public void AnEditorThatDoesNotFold_HasNoRegion_AndItsKeysClaimNothing()
    {
        var editor = Editor(caretLine: 4);

        editor.Folding.Enabled = false;

        editor.Folding.Regions.Should().BeEmpty();
        Press(editor, "[", Alt | Command).Should().BeFalse();
        editor.Apply(new CodeRange(new CodePosition(0, 0)), "x");
        editor.Folding.Regions.Should().BeEmpty("nothing is asked");
        editor.Folding.Enabled = true;
        editor.Folding.Regions.Should().Equal(new CodeFold(1, 5), new CodeFold(3, 4));
    }

    [Fact]
    public void ASpreadsheetCellsEntry_FoldsNothing()
    {
        // The entry draws every line it holds, so a fold there would move the caret off the text it draws.
        var sheet = new SheetController(rows: 2, cols: 2);
        sheet.Entry.Document = CodeDocument.FromText("total\n  =A1\n  +A2");
        sheet.Entry.Selection = new CodeRange(new CodePosition(1, 0));

        sheet.Entry.HandleKey("[", Shift | Command, KeyboardConvention.Standard, null).Should().BeFalse();
        sheet.Entry.Grid.Rows.Should().BeNull();
        sheet.Entry.Folding.Regions.Should().BeEmpty();
    }

    [Fact]
    public void AnEditWhileTheGridBringsItsOwnRows_DropsWhatWasAskedBeforeIt()
    {
        // Nothing is asked again for a grid with rows of its own, and an answer about the ten lines before the edit
        // must not land in the two lines after it.
        var editor = Editor("a\nb\nc\nd\ne\nf\ng\nh\ni\nj");
        var waiting = new Waiting();
        editor.Folding.Providers.Add(waiting);
        editor.Folding.Check();
        editor.Grid = editor.Grid with { Rows = new CodeRows(editor.Document.LineCount, [], []) };

        editor.Apply(new CodeRange(new CodePosition(0, 0), new CodePosition(8, 0)), "");
        waiting.Answer.SetResult([new CodeFold(7, 8)]);

        editor.Folding.Regions.Should().BeEmpty("the answer was about the document before the edit");
    }

    [Fact]
    public void ANewAnswer_KeepsFoldedWhatWasFoldedOnItsHeader()
    {
        var editor = Editor();
        var provider = new Fixed(new CodeFold(1, 5), new CodeFold(3, 4));
        editor.Folding.Providers.Add(provider);
        editor.Folding.Check();
        editor.Folding.Toggle(3);

        editor.Folding.Check();

        editor.Folding.IsFolded(3).Should().BeTrue();
        editor.Folding.IsFolded(1).Should().BeFalse();
    }

    [Fact]
    public void ANewAnswer_NeverHidesTheCaret()
    {
        // The method's region folded, and the next answer reaching one line further, onto the caret's line: VS Code's
        // folding model blocks the lines its selections stand on, so the region arrives open.
        var editor = Editor("a\n{\n  b\n  c\n}\nd\ne", caretLine: 5);
        var provider = new Fixed(new CodeFold(1, 4));
        editor.Folding.Providers.Add(provider);
        editor.Folding.Check();
        editor.Folding.Toggle(1);

        editor.Folding.Providers.Clear();
        editor.Folding.Providers.Add(new Fixed(new CodeFold(1, 5)));
        editor.Folding.Check();

        editor.Folding.Regions.Should().Equal(new CodeFold(1, 5));
        editor.Folding.IsFolded(1).Should().BeFalse("folded, it would hide the caret's line");
        Hidden(editor).Should().BeEmpty();
    }

    [Fact]
    public void ANewDocument_StartsWithItsOwnRegions_NoneFolded()
    {
        var editor = Editor();
        editor.Folding.FoldAll();

        editor.Document = CodeDocument.FromText("x\n  y\nz");

        editor.Folding.Regions.Should().Equal(new CodeFold(0, 1));
        Hidden(editor).Should().BeEmpty();
    }
}
