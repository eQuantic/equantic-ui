using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// THE EDITOR'S BEHAVIOUR, with no screen in sight. Everything here is what a person feels as
/// "this editor is good": the bracket that closes itself and gets out of the way, the undo that
/// takes back a word rather than a letter, the column a run of ↓ remembers, Tab over a selection.
/// An IDE built on this SDK inherits every one of them, and can test its own commands the same way.
/// </summary>
public class CodeEditorControllerTests
{
    private static CodeEditorController Editor(string text = "", ICodeLanguage? language = null)
    {
        var editor = new CodeEditorController(text, language ?? CodeLanguages.CSharp);
        editor.Selection = new CodeRange(editor.Document.End);
        return editor;
    }

    // ---- typing and pairs ----------------------------------------------------------------------

    [Fact]
    public void AnOpeningBracketClosesItself_AndTheCaretLandsInside()
    {
        var editor = Editor("var x = ");
        editor.Type('(');

        editor.Document.Text.Should().Be("var x = ()");
        editor.Caret.Column.Should().Be(9, "the caret sits between the pair, not after it");
    }

    [Fact]
    public void TypingTheClosingHalfStepsOverIt_InsteadOfDoublingIt()
    {
        var editor = Editor("var x = ");
        editor.Type('(');
        editor.Type(')');

        editor.Document.Text.Should().Be("var x = ()", "the pair was already there");
        editor.Caret.Column.Should().Be(10, "and the caret moved past it");
    }

    [Fact]
    public void AQuoteInsideAWordIsJustAnApostrophe()
    {
        var editor = Editor("don");
        editor.Type('\'');

        editor.Document.Text.Should().Be("don'", "`don't` must not become `don''`");
    }

    [Fact]
    public void WrappingASelectionInBracketsKeepsTheText()
    {
        var editor = Editor("value");
        editor.SelectAll();
        editor.Type('(');

        editor.Document.Text.Should().Be("(value)");
    }

    [Fact]
    public void DeletingTheOpeningHalfTakesTheClosingHalfWithIt()
    {
        var editor = Editor("");
        editor.Type('(');
        editor.DeleteBackward();

        editor.Document.Text.Should().BeEmpty("an auto-inserted pair leaves together");
    }

    // ---- newlines and indentation --------------------------------------------------------------

    [Fact]
    public void ANewLineInheritsTheIndentation()
    {
        var editor = Editor("    var x = 1;");
        editor.InsertNewLine();

        editor.Document.Line(1).Should().Be("    ", "code continues at the level it was at");
        editor.Caret.Should().Be(new CodePosition(1, 4));
    }

    [Fact]
    public void ANewLineAfterAnOpeningBraceIndentsOneMoreLevel()
    {
        var editor = Editor("    if (x) {");
        editor.InsertNewLine();

        editor.Document.Line(1).Should().Be("        ", "a block opened, so the body steps in");
    }

    [Fact]
    public void EnterBetweenAPairOpensTheBlockAndDropsTheCloser()
    {
        var editor = Editor("if (x) {}");
        editor.Selection = new CodeRange(new CodePosition(0, 8));   // between { and }
        editor.InsertNewLine();

        editor.Document.Lines.Should().Equal("if (x) {", "    ", "}");
        editor.Caret.Should().Be(new CodePosition(1, 4), "the caret waits on the empty body line");
    }

    [Fact]
    public void TabIndentsEveryLineOfASelection()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(2, 1));
        editor.Indent();

        editor.Document.Lines.Should().Equal("    one", "    two", "    three");
    }

    [Fact]
    public void ShiftTabTakesOneStepOff()
    {
        var editor = Editor("        deep");
        editor.Selection = new CodeRange(new CodePosition(0, 8));
        editor.Outdent();

        editor.Document.Line(0).Should().Be("    deep");
    }

    [Fact]
    public void TabWithACaretGoesToTheNextStop_NotAFixedWidth()
    {
        var editor = Editor("ab");                                  // column 2
        editor.Indent();

        editor.Document.Line(0).Should().Be("ab  ", "two spaces reach column 4, the next stop");
    }

    [Fact]
    public void BackspaceInLeadingWhitespaceTakesAWholeStep()
    {
        var editor = Editor("        x");
        editor.Selection = new CodeRange(new CodePosition(0, 8));
        editor.DeleteBackward();

        editor.Document.Line(0).Should().Be("    x", "indentation goes back the way it came");
    }

    // ---- comments -------------------------------------------------------------------------------

    [Fact]
    public void ToggleCommentAddsThenRemoves()
    {
        var editor = Editor("    var x = 1;");
        editor.ToggleLineComment();
        editor.Document.Line(0).Should().Be("    // var x = 1;");

        editor.Selection = new CodeRange(new CodePosition(0, 0));
        editor.ToggleLineComment();
        editor.Document.Line(0).Should().Be("    var x = 1;");
    }

    [Fact]
    public void ALanguageWithNoLineCommentDoesNothing()
    {
        var editor = Editor("{ \"a\": 1 }", CodeLanguages.Json);
        editor.ToggleLineComment().Should().BeFalse();
        editor.Document.Text.Should().Be("{ \"a\": 1 }", "a comment would make the file invalid");
    }

    [Fact]
    public void PythonCommentsWithAHash()
    {
        var editor = Editor("x = 1", CodeLanguages.Python);
        editor.ToggleLineComment();
        editor.Document.Line(0).Should().Be("# x = 1");
    }

    // ---- movement --------------------------------------------------------------------------------

    [Fact]
    public void MovingDownThroughAShortLineRemembersTheColumn()
    {
        var editor = Editor("a long first line\nsh\nanother long line");
        editor.Selection = new CodeRange(new CodePosition(0, 15));

        editor.Move(CodeMotion.Line, CodeDirection.Forward);
        editor.Caret.Should().Be(new CodePosition(1, 2), "the short line has nowhere else to be");

        editor.Move(CodeMotion.Line, CodeDirection.Forward);
        editor.Caret.Column.Should().Be(15, "and the column it wanted comes back");
    }

    [Fact]
    public void WordMovementStopsWhereAReaderWould()
    {
        var editor = Editor("foo.Bar(baz)");
        editor.Selection = new CodeRange(CodePosition.Start);

        editor.Move(CodeMotion.Word, CodeDirection.Forward);
        editor.Caret.Column.Should().Be(3, "after `foo`");

        editor.Move(CodeMotion.Word, CodeDirection.Forward);
        editor.Caret.Column.Should().Be(4, "over the dot");
    }

    [Fact]
    public void APlainArrowCollapsesASelectionToItsEdge()
    {
        var editor = Editor("selected text");
        editor.Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 8));

        editor.Move(CodeMotion.Character, CodeDirection.Forward);
        editor.Caret.Column.Should().Be(8, "→ puts the caret after the selection, not past it");
    }

    [Fact]
    public void ShiftArrowExtendsFromTheAnchor()
    {
        var editor = Editor("abcdef");
        editor.Selection = new CodeRange(new CodePosition(0, 2));

        editor.Move(CodeMotion.Character, CodeDirection.Forward, extend: true);
        editor.Move(CodeMotion.Character, CodeDirection.Forward, extend: true);

        editor.Document.TextIn(editor.Selection).Should().Be("cd");
    }

    // ---- history ---------------------------------------------------------------------------------

    [Fact]
    public void UndoTakesBackAWordTyped_NotOneLetter()
    {
        var editor = Editor("");
        foreach (var c in "hello") editor.Type(c);

        editor.Undo();

        editor.Document.Text.Should().BeEmpty("a run of typing is one thing a person did");
    }

    [Fact]
    public void MovingTheCaretEndsTheRun()
    {
        var editor = Editor("");
        foreach (var c in "one") editor.Type(c);
        editor.Move(CodeMotion.Character, CodeDirection.Backward);
        editor.Move(CodeMotion.Character, CodeDirection.Forward);
        foreach (var c in "two") editor.Type(c);

        editor.Undo();

        editor.Document.Text.Should().Be("one", "the second run undoes on its own");
    }

    [Fact]
    public void RedoPutsItBack_AndTypingKillsTheBranch()
    {
        var editor = Editor("");
        foreach (var c in "abc") editor.Type(c);
        editor.Undo();
        editor.Redo();
        editor.Document.Text.Should().Be("abc");

        editor.Undo();
        editor.Type('z');
        editor.History.CanRedo.Should().BeFalse("the future you did not take stops existing");
    }

    [Fact]
    public void EveryChangeRaisesTheEditThatCausedIt()
    {
        var editor = Editor("");
        var edits = new List<CodeEdit?>();
        editor.Changed += edits.Add;

        editor.Insert("hello");

        edits.Should().HaveCount(1);
        edits[0]!.InsertedText.Should().Be("hello");
        edits[0]!.RemovedText.Should().BeEmpty();
    }

    [Fact]
    public void AReadOnlyEditorRefusesEveryEdit()
    {
        var editor = Editor("fixed");
        editor.ReadOnly = true;

        editor.Type('x').Should().BeFalse();
        editor.InsertNewLine().Should().BeFalse();
        editor.DeleteBackward().Should().BeFalse();
        editor.Document.Text.Should().Be("fixed");
    }

    // ---- finding and matching --------------------------------------------------------------------

    [Fact]
    public void FindWalksForwardAndWraps()
    {
        var editor = Editor("one two one two");
        editor.Selection = new CodeRange(CodePosition.Start);

        editor.FindAll("one").Should().HaveCount(2);
        editor.FindNext("one")!.Value.Start.Column.Should().Be(0);

        editor.Selection = new CodeRange(new CodePosition(0, 4));
        editor.FindNext("one")!.Value.Start.Column.Should().Be(8);

        editor.Selection = new CodeRange(editor.Document.End);
        editor.FindNext("one")!.Value.Start.Column.Should().Be(0, "past the last match it wraps");
    }

    [Fact]
    public void BracketMatchingCountsNesting()
    {
        var editor = Editor("f(a(b), c)");

        editor.MatchingBracket(new CodePosition(0, 1)).Should().Be(new CodePosition(0, 9));
        editor.MatchingBracket(new CodePosition(0, 3)).Should().Be(new CodePosition(0, 5));
        editor.MatchingBracket(new CodePosition(0, 9)).Should().Be(new CodePosition(0, 1),
            "matching works backwards from the closer too");
    }

    [Fact]
    public void CopyWithNoSelectionTakesTheWholeLine()
    {
        var editor = Editor("first\nsecond");
        editor.Selection = new CodeRange(new CodePosition(0, 2));

        editor.CopyText().Should().Be("first\n");
    }

    /// <summary>
    /// An IDE applies edits nobody typed — a formatter, a refactor, a language server. They go
    /// through the same primitive, so they undo like anything else.
    /// </summary>
    [Fact]
    public void AProgrammaticEditUndoesLikeATypedOne()
    {
        var editor = Editor("var name = 1;");
        editor.Apply(new CodeRange(new CodePosition(0, 4), new CodePosition(0, 8)), "renamed");

        editor.Document.Text.Should().Be("var renamed = 1;");
        editor.Undo();
        editor.Document.Text.Should().Be("var name = 1;");
    }
}

/// <summary>Folding, which every editor of this class has, derived from indentation alone so it
/// works for a language nobody wrote a parser for.</summary>
public class CodeFoldingTests
{
    [Fact]
    public void AnIndentedBlockFoldsUnderItsHeader()
    {
        var document = CodeDocument.FromText("def run():\n    a = 1\n    b = 2\nprint(1)");

        var folds = new IndentationFoldProvider().FoldsFor(document);

        folds.Should().ContainSingle();
        folds[0].StartLine.Should().Be(0);
        folds[0].EndLine.Should().Be(2, "the region ends where the indentation comes back");
    }
}
