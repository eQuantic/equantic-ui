using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's snippets (#1013), driven as a person drives them, with no screen: the protocol's syntax read
/// as VS Code reads it, a snippet inserted at every caret, its stops walked with Tab and ⇧Tab, its mirrors
/// typed with their stop, its choices taken from a list, and the session ended as VS Code ends it.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeSnippetsTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeSnippetsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static CodeEditorController Editor(string text, params CodeRange[] selections)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.Selections = selections.Length > 0 ? selections : [new CodeRange(editor.Document.End)];
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.HandleKey(key, modifiers, KeyboardConvention.Apple, null);

    private static CodePosition At(int line, int column) => new(line, column);

    private static CodeRange Caret(int line, int column) => new(At(line, column));

    private static CodeRange Span(int line, int from, int to) => new(At(line, from), At(line, to));

    private static CodeEditorController Inserted(string body, string text = "", params CodeRange[] selections)
    {
        var editor = Editor(text, selections);
        editor.InsertSnippet(CodeSnippet.Parse(body)).Should().BeTrue();
        return editor;
    }

    // ---- the syntax -----------------------------------------------------------------------------------

    [Fact]
    public void ALoopWithMirrors_SelectsEveryPlaceOfItsFirstStop()
    {
        var editor = Inserted("for (var ${1:i} = 0; $1 < ${2:count}; $1++)");

        editor.Document.Text.Should().Be("for (var i = 0; i < count; i++)");
        editor.Selections.Should().Equal(Span(0, 9, 10), Span(0, 16, 17), Span(0, 27, 28));
        editor.Snippet.IsActive.Should().BeTrue();
    }

    [Fact]
    public void WhatIsNotSyntax_IsText()
    {
        var editor = Inserted(@"cost: $, ${1 and \$2");

        editor.Document.Text.Should().Be("cost: $, ${1 and $2");
        editor.Snippet.IsActive.Should().BeFalse();
        editor.Selection.Should().Be(Caret(0, 19));
    }

    [Theory]
    [InlineData("${1:a${2:b}c}", "abc")]
    [InlineData(@"a\}b\\c", @"a}b\c")]
    [InlineData("a}b", "a}b")]
    [InlineData("${1:a", "${1:a")]
    [InlineData("${1|a,b", "${1|a,b")]
    [InlineData("${2:x} and $2", "x and x")]
    [InlineData("$2 before ${2:y}", "y before y")]
    // A default that names a stop defined after it (Copilot on #1079), and one that names itself.
    [InlineData("${1:$2} ${2:x} $1", "x x x")]
    [InlineData("${1:a $1} $1", "a a  a a ")]
    [InlineData("${1/(.*)/${1:/upcase}/g}!", "!")]
    [InlineData("${TM_LINE_NUMBER}:${TM_LINE_INDEX}", "1:0")]
    [InlineData("${TM_SELECTED_TEXT:none}", "none")]
    public void TheSyntax_ReadsAsVSCodeReadsIt(string body, string text)
    {
        Inserted(body).Document.Text.Should().Be(text);
    }

    [Fact]
    public void AChoice_InsertsItsFirstOption_AndListsEveryOption()
    {
        var editor = Inserted(@"${1|one,two\,three|}");

        editor.Document.Text.Should().Be("one");
        editor.Completion.IsOpen.Should().BeTrue();
        editor.Completion.Items.Select(item => item.Item.Label).Should().Equal("one", "two,three");
    }

    // ---- inserting -----------------------------------------------------------------------------------

    [Fact]
    public void ASnippetAtTwoCarets_IsACopyAtEach_AsOneUndoStep()
    {
        var editor = Inserted("(${1:x})", "a\nb", Caret(0, 1), Caret(1, 1));

        editor.Document.Text.Should().Be("a(x)\nb(x)");
        editor.Selections.Should().Equal(Span(0, 2, 3), Span(1, 2, 3));
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("a\nb");
    }

    /// <summary>An editor of one line takes no snippet that breaks one, and nothing changes (Copilot on #1079): the
    /// copy was refused but its stops were placed on text never written.</summary>
    [Theory]
    [InlineData("x\n${1:y}")]
    [InlineData("${1:x\ny}")]
    public void AnEditorOfOneLine_RefusesASnippetThatBreaksOne(string body)
    {
        var editor = Editor("abc");
        editor.SingleLine = true;

        editor.InsertSnippet(CodeSnippet.Parse(body)).Should().BeFalse();

        editor.Document.Text.Should().Be("abc");
        editor.Selection.Should().Be(Caret(0, 3));
        editor.Snippet.IsActive.Should().BeFalse();
    }

    [Fact]
    public void TheFinalStopAlone_LeavesACaret_AndStartsNoSession()
    {
        var editor = Inserted("Run($0);");

        editor.Selection.Should().Be(Caret(0, 4));
        editor.Snippet.IsActive.Should().BeFalse();
        Press(editor, "Tab").Should().BeTrue();
        editor.Document.Text.Should().Be("Run(    );", "Tab indents where no snippet is being filled in");
    }

    // ---- Tab walks the stops --------------------------------------------------------------------------

    [Fact]
    public void TabAndShiftTab_WalkTheStops_AndTheFinalStopEndsTheSession()
    {
        var editor = Inserted("${1:a} ${2:b} $0.");
        editor.Selection.Should().Be(Span(0, 0, 1));

        Press(editor, "Tab").Should().BeTrue();
        editor.Selection.Should().Be(Span(0, 2, 3));
        Press(editor, "Tab", KeyModifiers.Shift).Should().BeTrue();
        editor.Selection.Should().Be(Span(0, 0, 1));
        Press(editor, "Tab");
        Press(editor, "Tab");

        editor.Selection.Should().Be(Caret(0, 4));
        editor.Snippet.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ShiftTabAtTheFirstStop_IsNotTheSnippets()
    {
        var editor = Inserted("${1:a} ${2:b}");

        editor.Snippet.HasPrevious.Should().BeFalse();
        editor.Execute(CodeCommand.PreviousTabStop).Should().BeFalse("⇧Tab goes on to outdent");
        editor.Snippet.IsActive.Should().BeTrue();
        editor.Selection.Should().Be(Span(0, 0, 1));
    }

    // ---- mirrors and the active stop ------------------------------------------------------------------

    [Fact]
    public void AMirror_IsTypedWithItsStop()
    {
        var editor = Inserted("${1:name} = $1;");

        editor.HandleText("x");

        editor.Document.Text.Should().Be("x = x;");
    }

    [Fact]
    public void TypingAtTheEndOfAPlaceholder_LengthensIt_AndLeavesTheNextStopAfterTheTyping()
    {
        var editor = Inserted("${1:a}$2;");

        editor.HandleText("x");
        editor.HandleText("y");
        Press(editor, "Tab").Should().BeTrue();

        editor.Document.Text.Should().Be("xy;");
        editor.Selection.Should().Be(Caret(0, 2));
        editor.Snippet.IsActive.Should().BeTrue();
    }

    // ---- nested placeholders --------------------------------------------------------------------------

    [Fact]
    public void ANestedStop_IsReachedInsideItsPlaceholder()
    {
        var editor = Inserted("${1:new ${2:Thing}()}$0");
        editor.Selection.Should().Be(Span(0, 0, 11));

        Press(editor, "Tab");

        editor.Selection.Should().Be(Span(0, 4, 9));
    }

    [Fact]
    public void ANestedStop_GoesWithThePlaceholderTypedOver()
    {
        var editor = Inserted("${1:new ${2:Thing}()}$0");

        foreach (var c in "null") editor.HandleText(c.ToString());
        Press(editor, "Tab");

        editor.Document.Text.Should().Be("null");
        editor.Selection.Should().Be(Caret(0, 4));
        editor.Snippet.IsActive.Should().BeFalse();
    }

    /// <summary>A mirror of a placeholder holds its nested stops, as VS Code clones a placeholder into its mirrors
    /// (Copilot on #1079): typing at the nested stop changed the placeholder and left the mirror as it was.</summary>
    [Fact]
    public void AMirrorOfAPlaceholder_HoldsItsNestedStops()
    {
        var editor = Inserted("${1:new ${2:Thing}()} = $1");

        Press(editor, "Tab");
        editor.HandleText("X");

        editor.Document.Text.Should().Be("new X() = new X()");
    }

    /// <summary>A malformed snippet is read in time linear in its length (Copilot on #1079): every enclosing
    /// construct that failed read again what its nested one had read, sixteen unterminated openers 65,535 times.</summary>
    [Fact]
    public void UnterminatedPlaceholders_AreReadOnce()
    {
        var body = string.Concat(Enumerable.Repeat("${1:", 200)) + "x";

        Inserted(body).Document.Text.Should().Be(body);
    }

    /// <summary>Two mirrors side by side stay two as they are typed in (Copilot on #1079): what was typed where the
    /// first ends grew the second too, they overlapped, and going back to them made them one caret.</summary>
    [Fact]
    public void MirrorsSideBySide_StayApart()
    {
        var editor = Inserted("${1:a}$1 ${2:b}");

        editor.HandleText("x");
        editor.HandleText("y");
        Press(editor, "Tab");
        Press(editor, "Tab", KeyModifiers.Shift);

        editor.Document.Text.Should().Be("xyxy b");
        editor.Selections.Should().Equal(Span(0, 0, 2), Span(0, 2, 4));
        editor.HandleText("z");
        editor.Document.Text.Should().Be("zz b");
    }

    /// <summary>The placeholders around the stop the carets stand at grow with it, as VS Code's do (Copilot on #1079):
    /// what was typed at the end of a nested stop was outside its enclosing placeholder, which went back to its
    /// first character alone.</summary>
    [Fact]
    public void ThePlaceholderAroundAStop_GrowsWithIt()
    {
        var editor = Inserted("${1:${2:a}}");

        Press(editor, "Tab");
        editor.HandleText("x");
        editor.HandleText("y");
        Press(editor, "Tab", KeyModifiers.Shift).Should().BeTrue();

        editor.Selection.Should().Be(Span(0, 0, 2));
        editor.HandleText("z");
        editor.Document.Text.Should().Be("z");
    }

    /// <summary>A stop written both alone and in another's placeholder grows that placeholder where it is nested in
    /// it (Copilot on #1079): its first place, outside every placeholder, stood for all of them.</summary>
    [Fact]
    public void AStopNestedAtOnePlace_GrowsThePlaceholderThere()
    {
        var editor = Inserted("${2:a} ${1:$2}");

        Press(editor, "Tab");
        editor.HandleText("x");
        editor.HandleText("y");
        Press(editor, "Tab", KeyModifiers.Shift).Should().BeTrue();

        editor.Document.Text.Should().Be("xy xy");
        editor.Selection.Should().Be(Span(0, 3, 5));
        editor.HandleText("z");
        editor.Document.Text.Should().Be("xy z");
    }

    /// <summary>A press into another placeholder makes it the stop the carets stand at (Copilot on #1079): the
    /// session stayed on the first stop, so typing there ended it and Tab went back to it.</summary>
    [Fact]
    public void APressIntoAnotherPlaceholder_MakesItTheStop()
    {
        var typed = Inserted("${1:a} ${2:b} ${3:c}");
        typed.Selection = Caret(0, 3);
        typed.HandleText("x");
        typed.Snippet.IsActive.Should().BeTrue();
        Press(typed, "Tab");
        typed.Selection.Should().Be(Span(0, 5, 6));

        var walked = Inserted("${1:a} ${2:b} ${3:c}");
        walked.Selection = Caret(0, 3);
        Press(walked, "Tab");
        walked.Selection.Should().Be(Span(0, 4, 5));
    }

    /// <summary>A snippet of mirrors side by side at hundreds of carets is typed in as at one (Copilot on #1079):
    /// keeping its places apart compared every pair after every edit.</summary>
    [Fact]
    public void MirrorsAtHundredsOfCarets_AreTypedIn()
    {
        const int lines = 300;
        var carets = Enumerable.Range(0, lines).Select(line => Caret(line, 1)).ToArray();
        var editor = Inserted("${1:x}$1", string.Join("\n", Enumerable.Repeat("v", lines)), carets);

        editor.HandleText("y");
        editor.HandleText("z");

        editor.Document.Text.Should().Be(string.Join("\n", Enumerable.Repeat("vyzyz", lines)));
    }

    // ---- choices --------------------------------------------------------------------------------------

    [Fact]
    public void AChoice_TakenFromItsList_IsWrittenOverEveryPlaceOfItsStop()
    {
        var editor = Inserted("${1|one,two,three|} = $1");
        editor.Completion.Items.Select(item => item.Item.Label).Should().Equal("one", "two", "three");

        Press(editor, "ArrowDown").Should().BeTrue();
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("two = two");
        editor.Completion.IsOpen.Should().BeFalse();
        editor.Undo().Should().BeTrue();
        editor.Document.Text.Should().Be("one = one");
    }

    /// <summary>The list of the stop the carets leave goes with it (Copilot on #1079): ⇧Tab from a choice left its
    /// list showing, and Enter wrote one of its options over the stop reached.</summary>
    [Fact]
    public void LeavingAChoice_DropsItsList()
    {
        var editor = Inserted("${2|one,two|} ${1:b}");

        Press(editor, "Tab");
        editor.Completion.IsOpen.Should().BeTrue();
        Press(editor, "Tab", KeyModifiers.Shift).Should().BeTrue();

        editor.Completion.IsOpen.Should().BeFalse();
        editor.Selection.Should().Be(Span(0, 4, 5));
    }

    /// <summary>An empty option is a choice taken (Copilot on #1079): it wrote nothing, so it was answered as not
    /// taken, and the Enter that took it went on to break the line.</summary>
    [Fact]
    public void AnEmptyOption_IsTaken()
    {
        var editor = Inserted("${1|,x|};$0");

        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be(";");
        editor.Completion.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AChoice_FiltersByWhatIsTypedInIt()
    {
        var editor = Inserted("${1|alpha,beta,gamma|}");

        editor.HandleText("b");

        editor.Completion.Items.Select(item => item.Item.Label).Should().Equal("beta");
    }

    // ---- variables ------------------------------------------------------------------------------------

    [Fact]
    public void TheSelectedText_IsWhatTheCopyReplaces()
    {
        var editor = Inserted("Log($TM_SELECTED_TEXT);", "value", Span(0, 0, 5));

        editor.Document.Text.Should().Be("Log(value);");
    }

    /// <summary>Every copy reads its variables in the document as it stood before any copy went in (Copilot on
    /// #1079): the copy at the first caret read a line the copy at the second was already in.</summary>
    [Fact]
    public void EveryCopy_ReadsTheDocumentAsItWas()
    {
        Inserted("$TM_CURRENT_LINE", "ab", Caret(0, 0), Caret(0, 2)).Document.Text.Should().Be("ababab");
    }

    /// <summary>The current word is the one at the selection, whatever range the copy replaces (Copilot on
    /// #1079): an entry that replaced the whole line read the line's first word.</summary>
    [Fact]
    public void TheCurrentWord_IsTheOneAtTheSelection()
    {
        var editor = Editor("alpha beta");
        editor.Completion.Providers.Add(new Offers(new CodeCompletionItem("word", CodeCompletionKind.Snippet)
        {
            FilterText = "alpha beta",
            Replacing = new CodeRange(At(0, 0), At(0, 10)),
            Snippet = CodeSnippet.Parse("$TM_CURRENT_WORD"),
        }));

        editor.Completion.Invoke().Should().BeTrue();
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("beta");
    }

    [Fact]
    public void AnUnknownVariable_IsAPlaceholderHoldingItsName_AfterTheSnippetsStops()
    {
        var editor = Inserted("${AUTHOR} wrote $1");

        editor.Document.Text.Should().Be("AUTHOR wrote ");
        editor.Selection.Should().Be(Caret(0, 13));
        Press(editor, "Tab");
        editor.Selection.Should().Be(Span(0, 0, 6));
    }

    // ---- indentation ----------------------------------------------------------------------------------

    [Fact]
    public void ABlock_TakesTheLinesIndentation_AndATabIsTheIndentStep()
    {
        var editor = Inserted("if ($1)\n{\n\t$0\n}", "    ");

        editor.Document.Text.Should().Be("    if ()\n    {\n        \n    }");
        editor.Selection.Should().Be(Caret(0, 8));
        Press(editor, "Tab");
        editor.Selection.Should().Be(Caret(2, 8));
    }

    // ---- the end --------------------------------------------------------------------------------------

    [Fact]
    public void Escape_EndsTheSession_AndTabIndentsAfter()
    {
        var editor = Inserted("${1:a} ${2:b}");

        Press(editor, "Escape").Should().BeTrue();

        editor.Snippet.IsActive.Should().BeFalse();
        editor.Selection.Should().Be(Span(0, 0, 1));
        Press(editor, "Tab").Should().BeTrue();
        editor.Document.Text.Should().StartWith("    ", "Tab indents once the snippet is left");
    }

    [Fact]
    public void ACaretMovedOutOfTheSnippet_EndsIt()
    {
        var editor = Inserted("(${1:a})");

        Press(editor, "End");

        editor.Snippet.IsActive.Should().BeFalse();
    }

    [Fact]
    public void EscapeInASnippetWithAMirror_LeavesItFirst_ThenTheOtherCarets()
    {
        var editor = Inserted("${1:a} = $1");

        Press(editor, "Escape").Should().BeTrue();
        editor.Snippet.IsActive.Should().BeFalse();
        editor.Selections.Should().HaveCount(2);

        Press(editor, "Escape").Should().BeTrue();
        editor.Selections.Should().HaveCount(1);
    }

    // ---- completion ------------------------------------------------------------------------------------

    private sealed class Offers(CodeCompletionItem item) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList([item]));
    }

    [Fact]
    public void AnEntryThatInsertsASnippet_StartsItsSession()
    {
        var editor = Editor("");
        editor.Completion.Providers.Add(new Offers(new CodeCompletionItem("Console.WriteLine", CodeCompletionKind.Method)
        {
            Snippet = CodeSnippet.Parse("Console.WriteLine($1);$0"),
        }));

        foreach (var c in "Con") editor.HandleText(c.ToString());
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("Console.WriteLine();");
        editor.Selection.Should().Be(Caret(0, 18));
        Press(editor, "Tab");
        editor.Selection.Should().Be(Caret(0, 20));
        editor.Snippet.IsActive.Should().BeFalse();
    }

    /// <summary>The selected text of a snippet accepted from the completion is the selection, which is nothing:
    /// the word typed, which the entry replaces, was taken for it (the review of #1013).</summary>
    [Fact]
    public void ASnippetFromTheCompletion_SelectsNothing()
    {
        var editor = Editor("");
        editor.Completion.Providers.Add(new Offers(new CodeCompletionItem("Log", CodeCompletionKind.Snippet)
        {
            Snippet = CodeSnippet.Parse("Log($TM_SELECTED_TEXT);"),
        }));

        editor.HandleText("L");
        editor.HandleText("o");
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("Log();");
    }

    /// <summary>An undo ends the snippet, as VS Code's does (the review of #1013): undoing the insertion collapsed
    /// every stop where the restored caret stood, which still counted as inside one, so Tab walked empty stops
    /// instead of indenting.</summary>
    [Fact]
    public void Undo_EndsTheSnippet()
    {
        var editor = Inserted("${1:a} ${2:b}");

        editor.Undo().Should().BeTrue();

        editor.Snippet.IsActive.Should().BeFalse();
        Press(editor, "Tab").Should().BeTrue();
        editor.Document.Text.Should().Be("    ");
    }

    [Fact]
    public void NamedSnippets_CompleteByTheirPrefix()
    {
        var editor = Editor("");
        editor.Completion.Providers.Add(new CodeSnippetCompletionProvider(
            [new CodeSnippetDefinition("for", "for (var ${1:i} = 0; $1 < ${2:count}; $1++)", "A loop")]));

        editor.HandleText("f");
        editor.HandleText("o");
        editor.Completion.Items.Should().ContainSingle(item => item.Item.Kind == CodeCompletionKind.Snippet
            && item.Item.Detail == "A loop");
        Press(editor, "Enter").Should().BeTrue();

        editor.Document.Text.Should().Be("for (var i = 0; i < count; i++)");
        editor.Selections.Should().Equal(Span(0, 9, 10), Span(0, 16, 17), Span(0, 27, 28));
    }

    /// <summary>A snippet named like a keyword is an entry of its own (#1013): the completion keyed an entry by
    /// its label and its inserted text, so the snippet `for` went into the keyword's entry, and the provider
    /// listed first, the keywords', was the one offered.</summary>
    [Fact]
    public void ASnippetNamedLikeAKeyword_IsListedBesideIt()
    {
        var editor = Editor("");
        editor.Completion.Providers.Add(new CodeKeywordCompletionProvider());
        editor.Completion.Providers.Add(new CodeSnippetCompletionProvider([new CodeSnippetDefinition("for", "for ($1)$0")]));

        foreach (var c in "for") editor.HandleText(c.ToString());

        editor.Completion.Items.Where(item => item.Item.Label == "for").Select(item => item.Item.Kind)
            .Should().Equal(CodeCompletionKind.Keyword, CodeCompletionKind.Snippet);
    }

    // ---- drawing --------------------------------------------------------------------------------------

    [Fact]
    public void ThePlaceholders_AreAnsweredWhileTheSessionLasts()
    {
        var editor = Inserted("${1:a} ${2:b}");

        editor.Snippet.PlaceholdersIn(0, 0).Should().Equal(Span(0, 0, 1), Span(0, 2, 3));
        Press(editor, "Escape");
        editor.Snippet.PlaceholdersIn(0, 0).Should().BeEmpty();
    }

    [Fact]
    public void TheEditorsBlock_WashesEveryPlaceholder_UntilTheSessionEnds()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor("", "csharp");
        editor.Editor.InsertSnippet(CodeSnippet.Parse("${1:a} ${2:b}"));

        var washes = Washes(Find<CodeBlock>(editor.Build(context))!);
        washes.Should().Equal(Span(0, 0, 1), Span(0, 2, 3));

        editor.Editor.Snippet.Leave();
        Washes(Find<CodeBlock>(editor.Build(context))!).Should().BeEmpty();

        static List<CodeRange> Washes(CodeBlock block) => block.Decorations
            .Where(mark => mark.Kind == CodeDecorationKind.Highlight
                && mark.Color == PhotonTheme.Instance.Colors(Variant.Info).Subtle)
            .Select(mark => mark.Range).ToList();
    }

    private static T? Find<T>(VisualNode node) where T : VisualNode
    {
        if (node is T match) return match;
        foreach (var child in Children(node))
            if (Find<T>(child) is { } found) return found;
        return null;
    }

    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        FlexNode flex => flex.Children,
        Box box when box.Child is { } child => [child],
        Stack stack => stack.Children,
        Flexible flexible => [flexible.Child],
        ScrollView scroll => [scroll.Child],
        Shortcut shortcut => [shortcut.Child],
        CodeSurface surface => [surface.Child],
        _ => [],
    };
}
