using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class DefaultKeywordTests
{
    // ============ Numeric Types ============

    [Fact]
    public void Default_Int_ReturnsZero()
    {
        var result = TestHelper.ConvertExpression("default(int)");
        result.Should().Be("0");
    }

    /// <summary>
    /// A default is the type's own, as the semantic model gives it (#380): a long is a BigInt and a
    /// decimal a Decimal on this side, so a plain 0 had none of their arithmetic.
    /// </summary>
    [Theory]
    [InlineData("default(long)", "$eq.num.long(0)")]
    [InlineData("default(System.Int64)", "$eq.num.long(0)")]
    [InlineData("default(double)", "0")]
    [InlineData("default(decimal)", "$eq.num.dec(0)")]
    // A nullable's default is null, where the spelling read past the `?` and answered 0.
    [InlineData("default(int?)", "null")]
    [InlineData("default(DayOfWeek)", "'sunday'")]
    [InlineData("default(DateTime)", "$eq.time.dateTime.minValue()")]
    [InlineData("default(TimeSpan)", "$eq.time.timeSpan.zero")]
    public void Default_IsTheTypesOwn(string expression, string expected)
    {
        TestHelper.ConvertExpression(expression).Should().Be(expected);
    }

    // ============ Boolean ============

    [Fact]
    public void Default_Bool_ReturnsFalse()
    {
        var result = TestHelper.ConvertExpression("default(bool)");
        result.Should().Be("false");
    }

    // ============ Char ============

    [Fact]
    public void Default_Char_IsTheNullChar()
    {
        // A char is a one-unit string on this side, and its default is U+0000, not the empty
        // string: `(int)default(char)` read no unit at all.
        var result = TestHelper.ConvertExpression("default(char)");
        result.Should().Be("'\\0'");
    }

    // ============ Reference Types ============

    [Fact]
    public void Default_String_ReturnsNull()
    {
        var result = TestHelper.ConvertExpression("default(string)");
        result.Should().Be("null");
    }

    [Fact]
    public void Default_Object_ReturnsNull()
    {
        var result = TestHelper.ConvertExpression("default(object)");
        result.Should().Be("null");
    }

    // ============ Contextual Default (literal) ============

    /// <summary>With no model to ask, the name is all there is, and a name qualified with
    /// <c>System.</c> is the same type as its keyword (found in review, #405).</summary>
    [Theory]
    [InlineData("default(System.Int64)", "$eq.num.long(0)")]
    [InlineData("default(System.Decimal)", "$eq.num.dec(0)")]
    [InlineData("default(System.Int32)", "0")]
    [InlineData("default(char)", "'\\0'")]
    [InlineData("default(System.Char)", "'\\0'")]
    [InlineData("default(int?)", "null")]
    public void Default_WithNoModel_IsWhatTheNameSays(string expression, string expected)
    {
        var converter = new eQuantic.UI.Compiler.CodeGen.CSharpToJsConverter();
        converter.ConvertExpression(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(expression))
            .Should().Be(expected);
    }

    [Fact]
    public void Default_Literal_ReturnsUndefined()
    {
        // A literal with nothing to convert it to names no type.
        var result = TestHelper.ConvertExpression("default");
        result.Should().Be("undefined");
    }

    /// <summary>The literal is the default of the type C# converts it to: `int x = default` left x
    /// undefined, and `x + 1` was NaN.</summary>
    [Theory]
    [InlineData("int x = default;", "let x = 0;")]
    [InlineData("long x = default;", "let x = 0n;")]      // a constant, folded to the BigInt it is
    [InlineData("int? x = default;", "let x: number | null = null;")]   // a local that starts null crosses with its declared type
    public void Default_Literal_IsItsTargetsDefault(string statement, string expected)
    {
        TestHelper.ConvertCodeBlock(statement).Should().Contain(expected);
    }
}
