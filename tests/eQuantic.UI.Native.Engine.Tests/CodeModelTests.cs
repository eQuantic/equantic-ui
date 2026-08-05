using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor's MODEL, tested without a screen: a document made of lines, positions that survive
/// edits, and the arithmetic every caret movement depends on. None of this needs pixels, which is
/// exactly why it is worth having as a separate layer — an IDE built on this SDK gets to unit-test
/// its own commands the same way.
/// </summary>
public class CodeDocumentTests
{
    [Fact]
    public void LineEndingsAreNormalized_WhateverWasPasted()
    {
        CodeDocument.FromText("a\r\nb\rc\nd").Lines.Should().Equal("a", "b", "c", "d");
    }

    [Fact]
    public void OffsetsAndPositionsAreInverses()
    {
        var document = CodeDocument.FromText("one\ntwo\nthree");
        for (var offset = 0; offset <= document.Length; offset++)
            document.OffsetOf(document.PositionOf(offset)).Should().Be(offset, $"offset {offset}");
    }

    [Fact]
    public void ReplacingAcrossLinesJoinsWhatIsLeft()
    {
        var document = CodeDocument.FromText("first\nsecond\nthird");
        var range = new CodeRange(new CodePosition(0, 2), new CodePosition(2, 2));

        var next = document.Replace(range, "X", out var caret);

        next.Text.Should().Be("fiXird");
        caret.Should().Be(new CodePosition(0, 3));
    }

    [Fact]
    public void InsertingLinesPutsTheCaretAtTheEndOfWhatWasInserted()
    {
        var document = CodeDocument.FromText("ab");
        var next = document.Replace(new CodeRange(new CodePosition(0, 1)), "1\n2\n3", out var caret);

        next.Lines.Should().Equal("a1", "2", "3b");
        caret.Should().Be(new CodePosition(2, 1));
    }

    [Fact]
    public void HomeGoesToTheFirstRealCharacter_ThenToColumnZero()
    {
        var document = CodeDocument.FromText("    indented");

        var first = document.LineStart(new CodePosition(0, 8));
        first.Column.Should().Be(4, "the first press lands where the code starts");

        document.LineStart(first).Column.Should().Be(0, "the second takes the true start");
    }

    [Fact]
    public void AWordIsALetterRun_AndASymbolRunIsAWordToo()
    {
        var document = CodeDocument.FromText("foo.Bar => x");

        document.TextIn(document.WordAt(new CodePosition(0, 5))).Should().Be("Bar");
        document.TextIn(document.WordAt(new CodePosition(0, 8))).Should().Be("=>");
    }

    [Fact]
    public void ClampingKeepsACaretThatOutlivedAnEditInsideTheDocument()
    {
        var document = CodeDocument.FromText("short");
        document.Clamp(new CodePosition(99, 99)).Should().Be(new CodePosition(0, 5));
    }
}

/// <summary>
/// The tokenizers. What is tested is what a reader SEES — that a keyword is a keyword and a string
/// that opened two lines up is still a string — never the token count, which is an implementation
/// detail that would make every improvement a test failure.
/// </summary>
public class CodeTokenizerTests
{
    private static CodeTokenKind KindAt(ICodeLanguage language, string line, int column,
        int state = 0)
    {
        var tokens = new List<CodeToken>();
        language.Tokenize(line, state, tokens);
        foreach (var token in tokens)
            if (column >= token.Start && column < token.End) return token.Kind;
        return CodeTokenKind.Plain;
    }

    [Fact]
    public void CSharp_ReadsKeywordsTypesStringsAndCalls()
    {
        const string line = "public static string Name() => \"hi\";";

        KindAt(CodeLanguages.CSharp, line, 0).Should().Be(CodeTokenKind.Keyword);   // public
        KindAt(CodeLanguages.CSharp, line, 14).Should().Be(CodeTokenKind.Type);     // string
        KindAt(CodeLanguages.CSharp, line, 21).Should().Be(CodeTokenKind.Function); // Name(
        KindAt(CodeLanguages.CSharp, line, 32).Should().Be(CodeTokenKind.String);   // "hi"
    }

    [Fact]
    public void CSharp_AnAttributeIsAnAttribute()
    {
        KindAt(CodeLanguages.CSharp, "    [Fact]", 5).Should().Be(CodeTokenKind.Attribute);
    }

    [Fact]
    public void CSharp_AnInterpolatedStringKeepsItsDollar()
    {
        var tokens = new List<CodeToken>();
        CodeLanguages.CSharp.Tokenize("var x = $\"a{b}\";", 0, tokens);
        var text = tokens.First(t => t.Kind == CodeTokenKind.String);
        text.Start.Should().Be(8, "the $ opens the string and belongs to it");
    }

    [Fact]
    public void ABlockCommentSurvivesTheLineBreak()
    {
        var tokens = new List<CodeToken>();
        var state = CodeLanguages.CSharp.Tokenize("/* opened", 0, tokens);
        state.Should().NotBe(0, "the next line starts inside the comment");

        KindAt(CodeLanguages.CSharp, "still a comment", 3, state).Should().Be(CodeTokenKind.Comment);

        tokens.Clear();
        var closed = CodeLanguages.CSharp.Tokenize("done */ var x;", state, tokens);
        closed.Should().Be(0, "the comment ended, and the rest of the line is code again");
        KindAt(CodeLanguages.CSharp, "done */ var x;", 8, state).Should().Be(CodeTokenKind.Type);
    }

    [Fact]
    public void CSharp_AVerbatimStringRunsAcrossLines()
    {
        var tokens = new List<CodeToken>();
        var state = CodeLanguages.CSharp.Tokenize("var path = @\"C:\\one", 0, tokens);
        state.Should().NotBe(0);
        KindAt(CodeLanguages.CSharp, "two\";", 1, state).Should().Be(CodeTokenKind.String);
    }

    [Fact]
    public void TypeScript_TemplateLiteralsAndDecorators()
    {
        var tokens = new List<CodeToken>();
        var state = CodeLanguages.TypeScript.Tokenize("const t = `line one", 0, tokens);
        state.Should().NotBe(0, "a template literal spans lines");

        KindAt(CodeLanguages.TypeScript, "@Component", 0).Should().Be(CodeTokenKind.Attribute);
        KindAt(CodeLanguages.TypeScript, "let x: number = 1;", 7).Should().Be(CodeTokenKind.Type);
    }

    [Fact]
    public void Python_DocstringsCommentsAndDecorators()
    {
        var tokens = new List<CodeToken>();
        var state = CodeLanguages.Python.Tokenize("\"\"\"opened", 0, tokens);
        state.Should().NotBe(0);
        KindAt(CodeLanguages.Python, "still the docstring", 2, state).Should().Be(CodeTokenKind.String);

        KindAt(CodeLanguages.Python, "def run(self):", 0).Should().Be(CodeTokenKind.Keyword);
        KindAt(CodeLanguages.Python, "def run(self):", 4).Should().Be(CodeTokenKind.Function);
        KindAt(CodeLanguages.Python, "def run(self):", 8).Should().Be(CodeTokenKind.Constant);
        KindAt(CodeLanguages.Python, "# a note", 2).Should().Be(CodeTokenKind.Comment);
        KindAt(CodeLanguages.Python, "@property", 0).Should().Be(CodeTokenKind.Attribute);
    }

    [Fact]
    public void Json_TellsAKeyFromAValue()
    {
        const string line = "  \"name\": \"eQuantic\",";
        KindAt(CodeLanguages.Json, line, 3).Should().Be(CodeTokenKind.Property);
        KindAt(CodeLanguages.Json, line, 12).Should().Be(CodeTokenKind.String);
    }

    [Fact]
    public void Xml_TagsAttributesAndComments()
    {
        const string line = "<Project Sdk=\"eQuantic\">";
        KindAt(CodeLanguages.Xml, line, 2).Should().Be(CodeTokenKind.Keyword);   // Project
        KindAt(CodeLanguages.Xml, line, 10).Should().Be(CodeTokenKind.Property); // Sdk
        KindAt(CodeLanguages.Xml, line, 15).Should().Be(CodeTokenKind.String);

        var tokens = new List<CodeToken>();
        var state = CodeLanguages.Xml.Tokenize("<!-- opened", 0, tokens);
        KindAt(CodeLanguages.Xml, "still commented", 3, state).Should().Be(CodeTokenKind.Comment);
    }

    [Fact]
    public void AnUnknownLanguageFallsBackToPlainText_RatherThanFailing()
    {
        CodeLanguages.For("brainfuck").Should().BeSameAs(CodeLanguages.PlainText);
        CodeLanguages.For(".cs").Should().BeSameAs(CodeLanguages.CSharp);
        CodeLanguages.For(null).Should().BeSameAs(CodeLanguages.PlainText);
    }

    /// <summary>
    /// The whole point of the carried state: changing one line re-colours only what it affects.
    /// </summary>
    [Fact]
    public void TheHighlighterRecoloursOnlyWhatChanged()
    {
        var document = CodeDocument.FromText("var a = 1;\nvar b = 2;\nvar c = 3;");
        var highlighter = new CodeHighlighter(CodeLanguages.CSharp);
        highlighter.TokensFor(document, 2);                       // warm every line

        var edited = document.Replace(
            new CodeRange(new CodePosition(1, 0), new CodePosition(1, 10)), "var b = 22;", out _);

        highlighter.LineChanged(edited, 1).Should().Be(1, "nothing below line 1 means anything else");

        var opened = edited.Replace(
            new CodeRange(new CodePosition(0, 0), new CodePosition(0, 10)), "/* open", out _);
        highlighter.LineChanged(opened, 0).Should().BeGreaterThan(0,
            "an unterminated comment changes what every line below it means");
    }
}
