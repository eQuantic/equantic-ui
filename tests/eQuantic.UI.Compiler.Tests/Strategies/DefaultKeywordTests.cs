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

    [Fact]
    public void Default_Long_ReturnsZero()
    {
        var result = TestHelper.ConvertExpression("default(long)");
        result.Should().Be("0");
    }

    [Fact]
    public void Default_Double_ReturnsZeroPointZero()
    {
        var result = TestHelper.ConvertExpression("default(double)");
        result.Should().Be("0.0");
    }

    [Fact]
    public void Default_Decimal_ReturnsZeroPointZero()
    {
        var result = TestHelper.ConvertExpression("default(decimal)");
        result.Should().Be("0.0");
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
    public void Default_Char_ReturnsEmptyString()
    {
        var result = TestHelper.ConvertExpression("default(char)");
        result.Should().Be("''");
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

    [Fact]
    public void Default_Literal_ReturnsUndefined()
    {
        var result = TestHelper.ConvertExpression("default");
        result.Should().Be("undefined");
    }
}
