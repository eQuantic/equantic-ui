using eQuantic.UI.Code;
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

    /// <summary>
    /// The selection a Tab indents comes back as it was, each end moved by what its own line gained
    /// and in its own direction. It was rebuilt from the caret the edit had left, anchored on the
    /// first line and running to the end of the last, whatever had been selected.
    /// </summary>
    [Fact]
    public void TabKeepsTheSelectionItIndented_EachEndMovedWithItsLine()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Selection = new CodeRange(new CodePosition(2, 2), new CodePosition(0, 1));   // made upward
        editor.Indent();

        editor.Selection.Anchor.Should().Be(new CodePosition(2, 6));
        editor.Selection.Focus.Should().Be(new CodePosition(0, 5));
    }

    [Fact]
    public void AWholeLineSelectionStaysWhole_AcrossTabAndShiftTab()
    {
        var editor = Editor("one\ntwo\nthree");
        var whole = new CodeRange(new CodePosition(0, 0), new CodePosition(2, 0));
        editor.Selection = whole;

        editor.Indent();
        editor.Selection.Should().Be(whole, "an end at column 0 stays there, so the lines are still whole");
        editor.Outdent();
        editor.Document.Lines.Should().Equal("one", "two", "three");
        editor.Selection.Should().Be(whole);
    }

    /// <summary>Shift+Tab takes up to one step off each line, and a line indented by less than a
    /// step loses all of it: it used to lose one character.</summary>
    [Fact]
    public void ShiftTabMovesEachEndBackByWhatItsLineLost()
    {
        var editor = Editor("    one\n  two");
        editor.Selection = new CodeRange(new CodePosition(0, 6), new CodePosition(1, 1));
        editor.Outdent();

        editor.Document.Lines.Should().Equal("one", "two");
        editor.Selection.Anchor.Should().Be(new CodePosition(0, 2));
        editor.Selection.Focus.Should().Be(new CodePosition(1, 0), "a column inside what went lands where it began");
    }

    /// <summary>
    /// A closing brace typed where only indentation is before it steps back to its block, which is
    /// what the language's <c>OutdentOn</c> has always said and nothing ever read.
    /// </summary>
    [Fact]
    public void AClosingBraceTypedOnAnIndentedEmptyLineStepsBackToItsBlock()
    {
        var editor = Editor("if (x) {\n        ");
        editor.Selection = new CodeRange(new CodePosition(1, 8));
        editor.Type('}');

        editor.Document.Line(1).Should().Be("    }");
        editor.Caret.Should().Be(new CodePosition(1, 5));
    }

    [Fact]
    public void AClosingBraceAfterCodeStaysWhereItIsTyped()
    {
        var editor = Editor("    x = {");
        editor.Type('}');

        editor.Document.Line(0).Should().Be("    x = {}");
    }

    /// <summary>
    /// Typing over a selection is one step with the run typed after it: the history joined only an
    /// edit that removed nothing, so the first character over a selection began a step of its own
    /// and undo took two presses. Measured in Chromium while driving slice 1a.
    /// </summary>
    [Fact]
    public void TypingOverASelectionIsOneUndoStep_WithTheRestOfTheRun()
    {
        var editor = Editor("var name = 1;");
        editor.Selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 8));
        editor.Type('i');
        editor.Type('d');
        editor.Document.Line(0).Should().Be("var id = 1;");

        editor.Undo();

        editor.Document.Line(0).Should().Be("var name = 1;");
    }

    /// <summary>A paste is a step of its own, as the history's own comment says: typing after it
    /// joined it, so one undo took the paste and the typing together.</summary>
    [Fact]
    public void APasteIsAStepOfItsOwn_NotTheStartOfATypingRun()
    {
        var editor = Editor("x");
        editor.Paste("abc");
        editor.Type('d');

        editor.Undo();
        editor.Document.Text.Should().Be("xabc");
        editor.Undo();
        editor.Document.Text.Should().Be("x");
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

    /// <summary>⌘/ keeps the selection over what it commented, the markers inside it. It dropped
    /// the selection to the caret the edit left, so a second ⌘/ could not take the markers back.</summary>
    [Fact]
    public void ToggleCommentKeepsTheSelection_OverWhatItCommented()
    {
        var editor = Editor("    one\n    two");
        var before = new CodeRange(new CodePosition(0, 4), new CodePosition(1, 7));
        editor.Selection = before;

        editor.ToggleLineComment();
        editor.Document.Lines.Should().Equal("    // one", "    // two");
        editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 4), new CodePosition(1, 10)));

        editor.ToggleLineComment();
        editor.Document.Lines.Should().Equal("    one", "    two");
        editor.Selection.Should().Be(before);
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

    // ---- rows ------------------------------------------------------------------------------------

    /// <summary>Ten lines, "line 0" to "line 9", with lines 3 to 6 folded behind one placeholder row
    /// (docs/CODE-EDITOR-PLAN.md, the shape, §9).</summary>
    private static CodeEditorController Folded(CodeCollapse fold)
    {
        var editor = Editor(string.Join("\n", Enumerable.Range(0, 10).Select(i => $"line {i}")));
        editor.Grid = new CodeGrid(Point.Zero, new Size(8, 18), new CodeRows(10, [], [fold]));
        return editor;
    }

    [Fact]
    public void AnArrowStepsOverTheLinesAFoldHides()
    {
        var editor = Folded(new CodeCollapse(3, 6));
        editor.Selection = new CodeRange(new CodePosition(2, 4));

        editor.Move(CodeMotion.Line, CodeDirection.Forward);
        editor.Caret.Should().Be(new CodePosition(7, 4), "lines 3 to 6 are folded, as an editor steps over a fold");
        editor.Move(CodeMotion.Line, CodeDirection.Backward);
        editor.Caret.Line.Should().Be(2);
    }

    [Fact]
    public void APageCountsTheLinesItShows()
    {
        var editor = Folded(new CodeCollapse(3, 6));
        editor.Selection = new CodeRange(CodePosition.Start);

        editor.Move(CodeMotion.Page, CodeDirection.Forward, pageLines: 3);

        editor.Caret.Line.Should().Be(7, "a page of three from line 0 shows 1, 2 and 7");
    }

    [Fact]
    public void AStepPastTheLastLineShown_StaysOnIt()
    {
        var editor = Folded(new CodeCollapse(7, 9, Placeholder: false));
        editor.Selection = new CodeRange(new CodePosition(6, 0));

        editor.Move(CodeMotion.Line, CodeDirection.Forward);

        editor.Caret.Line.Should().Be(6, "every line below is folded");
    }

    [Fact]
    public void ASelection_DrawsNoBandForTheLinesAFoldHides()
    {
        var editor = Folded(new CodeCollapse(3, 6));
        editor.Selection = new CodeRange(CodePosition.Start, editor.Document.End);

        var bands = editor.SelectionBandsIn(0, 9);

        bands.Should().HaveCount(6, "lines 0 to 2 and 7 to 9: the folded ones have no row to draw on");
        bands.Select(band => band.Y).Should().OnlyHaveUniqueItems("no band lies over the placeholder");
        bands[3].Y.Should().Be(4 * 18, "line 7 is on row 4: rows 0 to 2 are its first lines, 3 the placeholder");
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
