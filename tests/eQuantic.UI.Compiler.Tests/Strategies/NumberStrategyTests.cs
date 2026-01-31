using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class NumberStrategyTests
{
    [Fact]
    public void IntParse_MapsToParseInt()
    {
        var result = TestHelper.ConvertExpression("int.Parse(str)");
        result.Should().Be("parseInt(this.str)");
    }

    [Fact]
    public void DoubleParse_MapsToParseFloat()
    {
        var result = TestHelper.ConvertExpression("double.Parse(str)");
        result.Should().Be("parseFloat(this.str)");
    }

    [Fact]
    public void FloatParse_MapsToParseFloat()
    {
        var result = TestHelper.ConvertExpression("float.Parse(str)");
        result.Should().Be("parseFloat(this.str)");
    }

    [Fact]
    public void DecimalParse_MapsToParseFloat()
    {
        var result = TestHelper.ConvertExpression("decimal.Parse(str)");
        result.Should().Be("parseFloat(this.str)");
    }

    [Fact]
    public void LongParse_MapsToParseInt()
    {
        var result = TestHelper.ConvertExpression("long.Parse(str)");
        result.Should().Be("parseInt(this.str)");
    }

    [Fact]
    public void IntTryParse_MapsToParseIntWithNaNCheck()
    {
        var result = TestHelper.ConvertExpression("int.TryParse(str, out var result)");
        result.Should().Be("(result = parseInt(this.str), !isNaN(result))");
    }

    [Fact]
    public void DoubleTryParse_MapsToParseFloatWithNaNCheck()
    {
        var result = TestHelper.ConvertExpression("double.TryParse(str, out var value)");
        result.Should().Be("(value = parseFloat(this.str), !isNaN(value))");
    }
}
