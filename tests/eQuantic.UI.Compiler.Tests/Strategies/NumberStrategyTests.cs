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
    public void FloatParse_IsASingle()
    {
        // A float parsed from text is a single, like every float this side produces.
        var result = TestHelper.ConvertExpression("float.Parse(str)");
        result.Should().Be("Math.fround(parseFloat(this.str))");
    }

    [Fact]
    public void DecimalParse_IsTheRuntimesReader()
    {
        // A decimal read from text is a Decimal, rounded as .NET's parser rounds it (#358): the
        // double parseFloat made lost 0.1 on the way in and had no `add` for the next operation.
        var result = TestHelper.ConvertExpression("decimal.Parse(str)");
        result.Should().Be("$eq.num.decParse(this.str)");
    }

    [Fact]
    public void DecimalTryParse_LeavesZeroInTheOutWhenItFails()
    {
        var result = TestHelper.ConvertExpression("decimal.TryParse(str, out var value)");
        result.Should().Be("((value = $eq.num.decTryParse(this.str)) !== undefined || ((value = $eq.num.dec(0)), false))");
    }

    [Fact]
    public void IntTryParse_IntoADiscard_AssignsNothing()
    {
        // `_ = parseInt(…)` assigned a global nobody declared, which a module refuses at run time.
        var result = TestHelper.ConvertExpression("int.TryParse(str, out _)");
        result.Should().Be("(!isNaN(parseInt(this.str)))");
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
