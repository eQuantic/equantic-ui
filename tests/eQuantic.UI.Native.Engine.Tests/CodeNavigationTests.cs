using eQuantic.UI.Code;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's navigation (#1083), driven as a person drives it, with no screen: ⌃G to a line and ⇧⌘O to a
/// symbol as VS Code's quick access reads them, the code centred on what is read and the view given back on
/// Escape, and the jump list ⌃- and ⌃⇧- walk, as VS Code's navigation history records it.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeNavigationTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeNavigationTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    /// <summary>A hundred lines, the fortieth starting with a tab, a view 300 high over lines 20 high.</summary>
    private static CodeEditorController Editor(CodeRange? selection = null, int lines = 100)
    {
        var text = string.Join("\n", Enumerable.Range(1, lines).Select(i => i == 40 ? "\tx = 1;" : $"line {i}"));
        var editor = new CodeEditorController(text, CodeLanguages.CSharp)
        {
            Grid = new CodeGrid(Point.Zero, new Size(8, 20)),
        };
        editor.Selection = selection ?? Caret(2, 0);
        editor.View = new Rect(0, 0, 400, 300);
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Apple, null);

    private static CodePosition At(int line, int column) => new(line, column);

    private static CodeRange Caret(int line, int column) => new(At(line, column));

    private static CodeRange Lines(int from, int to) => new(At(from, 0), At(to, 0));

    /// <summary>
    /// <c>Ledger</c>, a class over lines 0 to 10, holding <c>AddEntry</c> (lines 2 to 4) and <c>Balance</c>
    /// (lines 6 to 8), each named at a place of its own on its first line.
    /// </summary>
    private static readonly IReadOnlyList<CodeSymbol> Ledger =
    [
        new CodeSymbol("Ledger", CodeSymbolKind.Class, Lines(0, 10), new CodeRange(At(0, 0), At(0, 4)))
        {
            Children =
            [
                new CodeSymbol("AddEntry", CodeSymbolKind.Method, Lines(2, 4), new CodeRange(At(2, 1), At(2, 4))),
                new CodeSymbol("Balance", CodeSymbolKind.Method, Lines(6, 8), new CodeRange(At(6, 2), At(6, 5)))
                {
                    Deprecated = true,
                },
            ],
        },
    ];

    private sealed class Answering(IReadOnlyList<CodeSymbol> symbols) : ICodeSymbolProvider
    {
        public int Asked;

        public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation)
        {
            Asked++;
            return Task.FromResult(symbols);
        }
    }

    private sealed class Waiting : ICodeSymbolProvider
    {
        public readonly TaskCompletionSource<IReadOnlyList<CodeSymbol>> Answer = new();

        public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation) =>
            Answer.Task;
    }

    private static CodeEditorController WithSymbols(CodeRange? selection = null)
    {
        var editor = Editor(selection);
        editor.Symbols.Providers.Add(new Answering(Ledger));
        return editor;
    }

    /// <summary>The top the view was last asked to scroll to.</summary>
    private static float? AskedTop(CodeEditorController editor) => editor.VerticalScroll?.Offset;

    // ---- going to a line --------------------------------------------------------------------------------

    [Fact]
    public void ALineAndAColumn_AreReadAsVSCodeReadsThem_AndTheCaretStaysUntilEnter()
    {
        var editor = Editor();

        Press(editor, "g", KeyModifiers.Control).Should().BeTrue();
        editor.GoTo.IsOpen.Should().BeTrue();
        editor.GoTo.Query.Should().Be(":");
        editor.GoTo.Mode.Should().Be(CodeGoToMode.Line);
        editor.GoTo.Line.Should().BeNull("nothing is typed yet");

        editor.GoTo.Query = ":40:5";

        editor.GoTo.Line.Should().Be(new CodeGoToLine(40, 5, true, 11), "a tab takes four cells, `x = 1;` six more, and the end one");
        editor.GoTo.Preview.Should().Be(Caret(39, 1));
        editor.Selection.Should().Be(Caret(2, 0), "the code follows the line, the caret stays");
        AskedTop(editor).Should().Be(39 * 20 + 10 - 150, "line 40 is centred in a view 300 high");

        editor.GoTo.Accept().Should().BeTrue();
        editor.Selection.Should().Be(Caret(39, 1), "the fifth visible column is the x after the tab");
        editor.GoTo.IsOpen.Should().BeFalse();
    }

    [Theory]
    [InlineData(":-1", 100, null)]
    [InlineData(":250", 100, null)]
    [InlineData(":0", 1, null)]
    [InlineData(":12abc", 12, null)]
    [InlineData(": +7 ", 7, null)]
    [InlineData(":40,5", 40, 5)]
    [InlineData(":40#5", 40, 5)]
    [InlineData(":40:-1", 40, 10)]
    [InlineData(":40:99", 40, 11)]
    [InlineData(":40:0", 40, 1)]
    public void ALine_IsClampedAndCounted_AsVSCodeCountsIt(string query, int line, int? column)
    {
        var editor = Editor();
        editor.GoTo.Open(CodeGoToMode.Line);

        editor.GoTo.Query = query;

        editor.GoTo.Line!.Line.Should().Be(line);
        editor.GoTo.Line.Column.Should().Be(column);
    }

    [Fact]
    public void AColumnAskedFor_IsAColumnStarted()
    {
        var editor = Editor();
        editor.GoTo.Open(CodeGoToMode.Line);

        editor.GoTo.Query = ":40:";

        editor.GoTo.Line.Should().Be(new CodeGoToLine(40, null, true, 11));
        editor.GoTo.Preview.Should().Be(Caret(39, 0));
    }

    [Theory]
    [InlineData(":")]
    [InlineData(":abc")]
    [InlineData(":-")]
    public void NothingToGoTo_IsAcceptedAsNothing_AndTheFieldStays(string query)
    {
        var editor = Editor();
        editor.GoTo.Open(CodeGoToMode.Line);
        editor.GoTo.Query = query;

        editor.GoTo.Line.Should().BeNull();
        editor.GoTo.Accept().Should().BeFalse();
        editor.GoTo.IsOpen.Should().BeTrue();
        editor.Selection.Should().Be(Caret(2, 0));
    }

    // ---- going to a symbol ------------------------------------------------------------------------------

    [Fact]
    public void AMethodByItsInitials_IsListedWithItsContainer_AndGoneTo()
    {
        var editor = WithSymbols();

        Press(editor, "o", KeyModifiers.Shift | KeyModifiers.Command).Should().BeTrue();
        editor.GoTo.Query.Should().Be("@");
        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("Ledger", "AddEntry", "Balance");

        editor.GoTo.Query = "@ae";

        editor.GoTo.Entries.Select(e => e.Name).Should().Equal(["AddEntry", "Balance"],
            "both hold an a and then an e, and AddEntry's start its parts");
        var entry = editor.GoTo.Entries[0];
        entry.Name.Should().Be("AddEntry");
        entry.Container.Should().Be("Ledger");
        entry.Matches.Should().Equal(0, 3);
        editor.GoTo.Selected.Should().Be(0);
        editor.GoTo.Preview.Should().Be(Lines(2, 4), "the symbol's whole range is washed");

        editor.GoTo.Accept().Should().BeTrue();
        editor.Selection.Should().Be(Caret(2, 1), "where its name starts");
    }

    [Fact]
    public void WithNothingTyped_TheSymbolTheCaretIsIn_IsSelected()
    {
        var editor = WithSymbols(Caret(7, 3));

        editor.GoTo.Open(CodeGoToMode.Symbol);

        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("Ledger", "AddEntry", "Balance");
        editor.GoTo.Selected.Should().Be(2, "Balance is the innermost symbol around the caret");
        editor.GoTo.Entries[2].Deprecated.Should().BeTrue();

        editor.GoTo.Query = "@b";
        editor.GoTo.Query = "@";
        editor.GoTo.Selected.Should().Be(0, "once typed in, the list selects its first, as VS Code's does");
    }

    [Fact]
    public void OnePrefix_BecomesTheOther()
    {
        var provider = new Answering(Ledger);
        var editor = Editor();
        editor.Symbols.Providers.Add(provider);
        editor.GoTo.Open(CodeGoToMode.Line);
        provider.Asked.Should().Be(0, "a line asks no symbols");

        editor.GoTo.Query = "@bal";

        provider.Asked.Should().Be(1);
        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("Balance");
        editor.GoTo.Query = "@bala";
        provider.Asked.Should().Be(1, "typing filters the answer it has");
        editor.GoTo.Accept().Should().BeTrue();
        editor.Selection.Should().Be(Caret(6, 2));
    }

    [Fact]
    public void TheWordsAfterTheFirst_MatchTheContainer()
    {
        var editor = WithSymbols();
        editor.GoTo.Open(CodeGoToMode.Symbol);

        editor.GoTo.Query = "@add led";
        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("AddEntry");
        editor.GoTo.Entries[0].ContainerMatches.Should().Equal(0, 1, 2);

        editor.GoTo.Query = "@add zzz";
        editor.GoTo.Entries.Should().BeEmpty();
        editor.GoTo.Selected.Should().Be(-1);
        editor.GoTo.Preview.Should().BeNull();
    }

    [Fact]
    public void TheBestMatch_IsListedFirst_ThenTheDocumentsOrder()
    {
        var editor = Editor();
        editor.Symbols.Providers.Add(new Answering(
        [
            new CodeSymbol("RebalanceAll", CodeSymbolKind.Method, Lines(1, 2), Lines(1, 1)),
            new CodeSymbol("Balance", CodeSymbolKind.Method, Lines(3, 4), Lines(3, 3)),
            new CodeSymbol("Ballot", CodeSymbolKind.Field, Lines(5, 5), Lines(5, 5)),
        ]));
        editor.GoTo.Open(CodeGoToMode.Symbol);

        editor.GoTo.Query = "@bal";

        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("Balance", "Ballot", "RebalanceAll");
    }

    [Fact]
    public void TheArrows_WalkTheList_AndWrap()
    {
        var editor = WithSymbols(Caret(20, 0));
        editor.GoTo.Open(CodeGoToMode.Symbol);
        editor.GoTo.Selected.Should().Be(0);

        editor.GoTo.SelectNext().Should().BeTrue();
        editor.GoTo.SelectNext().Should().BeTrue();
        editor.GoTo.SelectPrevious().Should().BeTrue();
        editor.GoTo.Selected.Should().Be(1);
        editor.GoTo.Preview.Should().Be(Lines(2, 4));

        editor.GoTo.SelectNext();
        editor.GoTo.SelectNext().Should().BeTrue();
        editor.GoTo.Selected.Should().Be(0, "past the last is the first");
        editor.GoTo.SelectPrevious();
        editor.GoTo.Selected.Should().Be(2, "before the first is the last");
        editor.GoTo.SelectNextPage().Should().BeFalse("a page down from the last is the last");
        editor.GoTo.SelectPreviousPage().Should().BeTrue();
        editor.GoTo.Selected.Should().Be(0);
    }

    [Fact]
    public void SymbolsHeldOrStartingTogether_AreListedOuterFirst()
    {
        var editor = Editor();
        editor.Symbols.Providers.Add(new Answering(
        [
            new CodeSymbol("Inner", CodeSymbolKind.Field, Lines(4, 5), Lines(4, 4)),
            new CodeSymbol("Outer", CodeSymbolKind.Class, Lines(4, 9), Lines(4, 4)),
            new CodeSymbol("First", CodeSymbolKind.Class, Lines(1, 2), Lines(1, 1)),
        ]));
        var second = new Answering([new CodeSymbol("Between", CodeSymbolKind.Function, Lines(3, 3), Lines(3, 3))]);
        editor.Symbols.Providers.Add(second);

        editor.GoTo.Open(CodeGoToMode.Symbol);

        editor.GoTo.Entries.Select(e => e.Name).Should().Equal("First", "Between", "Outer", "Inner");
        editor.GoTo.Entries.Select(e => e.Container).Should().AllSatisfy(c => c.Should().BeNull());
    }

    [Fact]
    public void AnAnswerAfterTheFieldMovedOn_IsDropped()
    {
        var waiting = new Waiting();
        var editor = Editor();
        editor.Symbols.Providers.Add(waiting);
        editor.GoTo.Open(CodeGoToMode.Symbol);
        editor.GoTo.IsAsking.Should().BeTrue();

        editor.GoTo.Query = ":5";
        waiting.Answer.SetResult(Ledger);

        editor.GoTo.Mode.Should().Be(CodeGoToMode.Line);
        editor.GoTo.Entries.Should().BeEmpty();
        editor.GoTo.IsAsking.Should().BeFalse();
    }

    [Fact]
    public void AProviderThatThrows_IsReported_AndTheOthersStillList()
    {
        var editor = Editor();
        var failures = new List<Exception>();
        editor.Symbols.Failed += failures.Add;
        editor.Symbols.Providers.Add(new Throwing());
        editor.Symbols.Providers.Add(new Answering(Ledger));

        editor.GoTo.Open(CodeGoToMode.Symbol);

        failures.Should().ContainSingle().Which.Message.Should().Be("no outline");
        editor.GoTo.Entries.Should().HaveCount(3);
    }

    private sealed class Throwing : ICodeSymbolProvider
    {
        public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation) =>
            Task.FromException<IReadOnlyList<CodeSymbol>>(new InvalidOperationException("no outline"));
    }

    // ---- Escape gives the view back ---------------------------------------------------------------------

    [Fact]
    public void Escape_GivesBackTheViewAndTheKeyboard_AndLeavesTheCaret()
    {
        var editor = Editor();
        editor.View = new Rect(16, 240, 400, 300);
        var focus = editor.FocusVersion;
        editor.GoTo.Open(CodeGoToMode.Line);
        editor.GoTo.Query = ":400";
        AskedTop(editor).Should().Be(99 * 20 + 10 - 150, "past the end is the last line, centred");

        editor.GoTo.Close().Should().BeTrue();

        editor.GoTo.IsOpen.Should().BeFalse();
        AskedTop(editor).Should().Be(240, "the view stands where it stood when the field opened");
        editor.HorizontalScroll!.Offset.Should().Be(16);
        editor.Selection.Should().Be(Caret(2, 0));
        editor.FocusVersion.Should().Be(focus + 1);
    }

    /// <summary>
    /// What reaches the code while the field shows is the field's (#1083): on the web the field takes the
    /// keyboard on the frame that draws it, and ⌃G with a line number typed at once edited the code, its
    /// digits and its Enter.
    /// </summary>
    [Fact]
    public void WhatReachesTheCodeWhileTheFieldShows_IsTheFields()
    {
        var editor = Editor(lines: 200);
        var text = editor.Document.Text;
        Press(editor, "g", KeyModifiers.Control);

        editor.HandleText("1").Should().BeTrue();
        editor.HandleText("29").Should().BeTrue();
        Press(editor, "Backspace").Should().BeTrue();
        editor.Paste("\n0").Should().BeTrue();
        editor.GoTo.Query.Should().Be(":120");
        editor.SetComposition("x").Should().BeFalse();
        editor.Cut().Should().BeEmpty();
        Press(editor, "z", KeyModifiers.Command).Should().BeFalse("a chord is the host's");
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be(text, "nothing typed went into the code");
        editor.Selection.Should().Be(Caret(119, 0));
        editor.GoTo.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void TheFieldsKeys_ReachItThroughTheCodeToo()
    {
        var editor = WithSymbols(Caret(20, 0));
        editor.GoTo.Open(CodeGoToMode.Symbol);

        Press(editor, "ArrowDown").Should().BeTrue();
        Press(editor, "ArrowDown").Should().BeTrue();
        Press(editor, "ArrowUp").Should().BeTrue();
        editor.GoTo.Selected.Should().Be(1);
        Press(editor, "PageDown").Should().BeTrue();
        editor.GoTo.Selected.Should().Be(2);
        Press(editor, "Escape").Should().BeTrue();
        editor.GoTo.IsOpen.Should().BeFalse();
        Press(editor, "ArrowDown").Should().BeTrue("the field is closed, and the arrow moves the caret");
        editor.Selection.Should().Be(Caret(21, 0));
    }

    [Fact]
    public void OpeningTheField_ClosesWhatWasOverTheCode()
    {
        var editor = Editor();
        editor.Completion.Providers.Add(new CodeKeywordCompletionProvider());
        editor.Execute(CodeCommand.CompletionShow);
        editor.Completion.IsOpen.Should().BeTrue();

        editor.GoTo.Open(CodeGoToMode.Line);

        editor.Completion.IsOpen.Should().BeFalse();
    }

    // ---- the jump list ----------------------------------------------------------------------------------

    [Fact]
    public void BackFromAJump_IsWhereTheJumpLeft()
    {
        var editor = Editor();
        Press(editor, "g", KeyModifiers.Control);
        editor.GoTo.Query = ":80";
        editor.GoTo.Accept();
        editor.Selection.Should().Be(Caret(79, 0));

        Press(editor, "-", KeyModifiers.Control).Should().BeTrue();
        editor.Selection.Should().Be(Caret(2, 0));
        Press(editor, "-", KeyModifiers.Control).Should().BeFalse("the first place has nothing before it");

        Press(editor, "_", KeyModifiers.Control | KeyModifiers.Shift).Should().BeTrue();
        editor.Selection.Should().Be(Caret(79, 0));
        Press(editor, "-", KeyModifiers.Control | KeyModifiers.Shift).Should().BeFalse("nothing is ahead of the last");
    }

    [Fact]
    public void AShortMove_ReplacesThePlace_AndAFarOneIsANewOne()
    {
        var editor = Editor();
        editor.Selection = Caret(4, 0);
        editor.Selection = Caret(49, 0);

        editor.JumpList.Places.Should().Equal(Caret(4, 0), Caret(49, 0));
        editor.Execute(CodeCommand.NavigateBack).Should().BeTrue();
        editor.Selection.Should().Be(Caret(4, 0));
    }

    [Fact]
    public void AJump_IsAPlaceOfItsOwnNearby_ButNotOnTheSameLine()
    {
        var editor = Editor();
        editor.GoTo.Open(CodeGoToMode.Line);
        editor.GoTo.Query = ":6";
        editor.GoTo.Accept();
        editor.JumpList.Places.Should().Equal(Caret(2, 0), Caret(5, 0));

        editor.GoTo.Open(CodeGoToMode.Line);
        editor.GoTo.Query = ":6:3";
        editor.GoTo.Accept();
        editor.JumpList.Places.Should().Equal(Caret(2, 0), Caret(5, 2));
    }

    [Fact]
    public void APlaceAfterWalkingBack_DropsThePlacesAhead()
    {
        var editor = Editor();
        editor.Selection = Caret(30, 0);
        editor.Selection = Caret(60, 0);
        editor.JumpList.Back();
        editor.JumpList.Back();

        editor.Selection = Caret(90, 0);

        editor.JumpList.Places.Should().Equal(Caret(2, 0), Caret(90, 0));
        editor.JumpList.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void AWalk_RecordsNothing_AndCentresAPlaceOutOfView()
    {
        var editor = Editor();
        editor.Selection = Caret(80, 0);
        var asked = AskedTop(editor);

        editor.JumpList.Back();
        AskedTop(editor).Should().Be(asked, "line 3 is in a view over the top of the document");
        editor.View = new Rect(0, 1500, 400, 300);
        editor.JumpList.Forward();

        editor.JumpList.Places.Should().Equal(Caret(2, 0), Caret(80, 0));
        AskedTop(editor).Should().BeNull("line 81 is in a view over 1500 to 1800");
        editor.View = new Rect(0, 0, 400, 300);
        editor.JumpList.Back();
        editor.JumpList.Forward();
        AskedTop(editor).Should().Be(80 * 20 + 10 - 150);
    }

    [Fact]
    public void FiftyPlaces_AreKept()
    {
        var editor = Editor(lines: 1000);
        for (var i = 1; i <= 60; i++) editor.Selection = Caret(i * 10, 0);

        editor.JumpList.Places.Should().HaveCount(CodeJumpList.Capacity);
        editor.JumpList.Places[0].Should().Be(Caret(110, 0));
        editor.JumpList.Index.Should().Be(CodeJumpList.Capacity - 1);
    }

    [Fact]
    public void APlace_IsCarriedThroughEveryEdit()
    {
        var editor = Editor();
        editor.GoTo.Open(CodeGoToMode.Line);
        editor.GoTo.Query = ":80";
        editor.GoTo.Accept();
        editor.JumpList.Back();

        editor.InsertNewLine();
        editor.InsertNewLine();
        editor.JumpList.Forward().Should().BeTrue();

        editor.Selection.Should().Be(Caret(81, 0), "line 82 is the line that was 80");
    }

    [Fact]
    public void ANewDocument_StartsANewList()
    {
        var editor = Editor();
        editor.Selection = Caret(80, 0);

        editor.Document = CodeDocument.FromText("one\ntwo");

        editor.JumpList.Places.Should().ContainSingle();
        editor.JumpList.CanGoBack.Should().BeFalse();
    }

    // ---- revealing in the middle of the view ------------------------------------------------------------

    [Fact]
    public void ALineFarBelow_IsCentred_AndOneInView_IsLeftWhereItIs()
    {
        var editor = Editor(lines: 200);

        editor.RevealInCenterIfOutsideView(At(5, 0));
        editor.VerticalScroll.Should().BeNull("line 6 is in view");
        editor.RevealInCenter(At(100, 0));

        editor.VerticalScroll!.Offset.Should().Be(1860);
        editor.HorizontalScroll.Should().BeNull("column 0 is in view across");
        editor.Selection.Should().Be(Caret(2, 0));
    }

    [Fact]
    public void BeforeTheViewIsKnown_NothingIsAsked()
    {
        var editor = new CodeEditorController("a\nb");

        editor.RevealInCenter(At(1, 0));

        editor.VerticalScroll.Should().BeNull();
    }

    [Fact]
    public void ALineNearTheTop_IsCentredAsFarAsTheTopAllows()
    {
        var editor = Editor();
        editor.View = new Rect(0, 900, 400, 300);

        editor.RevealInCenter(At(1, 0));

        editor.VerticalScroll!.Offset.Should().Be(0);
    }

    [Fact]
    public void APositionAcrossTheView_IsBroughtIntoItByTheLeastMovement()
    {
        var editor = Editor();
        editor.Document = CodeDocument.FromText(new string('x', 200));

        editor.RevealInCenter(At(0, 150));

        editor.HorizontalScroll!.Offset.Should().Be(150 * 8 + CodeEditorController.CaretWidth - 400);
    }
}
