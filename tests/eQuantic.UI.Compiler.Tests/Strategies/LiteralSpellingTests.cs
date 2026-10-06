using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// How a string or a char is SPELLED in the module (#520). The conformance suite proves the values on
/// both sides; these pin the rule the one writer follows, so a change to it shows as a spelling: a code
/// unit is written as itself when it shows as itself, and as an escape when it does not.
/// </summary>
public class LiteralSpellingTests
{
    [Theory]
    [InlineData("\"x\\uD83D\"", "'x\\uD83D'")]                  // a high half with nothing after it
    [InlineData("\"\\uDE00x\"", "'\\uDE00x'")]                  // a low half with nothing before it
    [InlineData("\"\\uD83D\\uDE00\"", "'\uD83D\uDE00'")]        // a pair is one code point, written raw
    [InlineData("\"a\\tb\\r\\n\"", "'a\\tb\\r\\n'")]            // the three short escapes
    [InlineData("\"\\01\"", "'\\u00001'")]                       // a NUL before a digit, which \0 cannot spell
    [InlineData("\"\\a\\e\\u007F\\u0085\"", "'\\u0007\\u001B\\u007F\\u0085'")] // C0, DEL and C1 controls
    [InlineData("\"\\u2028\\u2029\"", "'\\u2028\\u2029'")]      // the separators JavaScript counts as line breaks
    [InlineData("\"\\u200D\\uFEFF\\u202E\"", "'\\u200D\\uFEFF\\u202E'")] // format characters, a bidi override among them
    [InlineData("\"\\uE000\\u00A0 \"", "'\\uE000\\u00A0 '")]    // private use, and a space that is not U+0020
    [InlineData("\"\\u0301e\\u0301\"", "'\\u0301e\u0301'")]     // a mark with nothing to sit on, then one on its letter
    [InlineData("\"a\\u00E7\\u00E3o\"", "'a\u00E7\u00E3o'")]    // text shows as itself
    [InlineData("@\"a\\b\"\"c\"", "'a\\\\b\"c'")]                // verbatim: a backslash is text
    [InlineData("'\\a'", "'\\u0007'")]                           // C#'s alert, 'a' to JavaScript
    [InlineData("'\\x041'", "'A'")]                              // C#'s hexadecimal escape of any length
    [InlineData("'\\U00000041'", "'A'")]                         // C#'s eight-digit escape
    [InlineData("'\\''", "'\\''")]
    [InlineData("'\\uD83D'", "'\\uD83D'")]
    public void AStringOrAChar_IsSpelledFromItsValue(string literal, string spelled)
    {
        TestHelper.ConvertExpression($"var s = {literal}").Should().Contain($" = {spelled};");
    }

    [Theory]
    [InlineData("$\"a\\u2028{1}\\a\"", "`a\\u2028${1}\\u0007`")]   // the template's text by the same rule
    [InlineData("$\"`${{x}}{1}\"", "`\\`\\${x}${1}`")]             // a backtick and a ${ are text
    [InlineData("$$$\"\"\"a{{b}}{{{1}}}\"\"\"", "`a{{b}}${1}`")]  // a raw string's braces are its text
    [InlineData("$\"{{b}}{1}\"", "`{b}${1}`")]                     // a regular one's doubled brace is one
    public void ATemplatesText_IsSpelledFromItsValue(string interpolated, string spelled)
    {
        TestHelper.ConvertExpression($"var s = {interpolated}").Should().Contain($" = {spelled};");
    }

    [Fact]
    public void AFormat_IsQuotedAsAnyString()
    {
        TestHelper.ConvertExpression("var s = $\"{System.DateTime.Now:dd 'de' MMMM}\"")
            .Should().Contain(@"'dd \'de\' MMMM'");
    }

    [Fact]
    public void ASkippedParametersDefault_IsQuotedAsAnyString()
    {
        TestHelper.ConvertCodeBlock("string Join(string a = \"it's\", char c = ',', int b = 0) => a + c + b; var r = Join(b: 1);")
            .Should().Contain(@"join('it\'s', ',', 1)");
    }

    [Fact]
    public void AClassConstructorsDefault_IsQuotedAsAnyString()
    {
        // A class's parameter that a named argument skips arrives undefined, and the twin's constructor
        // gives it its default (#583), quoted as any string is.
        var ts = TestHelper.ConvertClass("""public Inner(string a = "it's\n", char c = '\'', int b = 0) { }""", "Inner");

        ts.Should().Contain(@"a: string = 'it\'s\n'").And.Contain(@"= '\''");
    }

    [Fact]
    public void Nameof_IsTheNameAndNotItsSpelling()
    {
        TestHelper.ConvertCodeBlock("var @class = 1; var n = nameof(@class);")
            .Should().Contain("n = 'class';");
    }

    [Fact]
    public void Nameof_WithNoModel_IsTheIdentifiersValue()
    {
        // The playground compiles a buffer alone: the identifier's value, not its text, answers.
        new CSharpToJsConverter()
            .ConvertExpression(SyntaxFactory.ParseExpression("nameof(@class)"))
            .Should().Be("'class'");
    }

    [Fact]
    public void AUtf8Literal_IsRefused()
    {
        TestHelper.DiagnosticsFor("var n = \"ab\"u8.Length")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("matched the literal translation"));
    }
}
