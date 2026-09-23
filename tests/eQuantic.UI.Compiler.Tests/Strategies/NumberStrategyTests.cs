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

    /// <summary>
    /// The format provider is left out, which is faithful only when C# evaluating it cannot be
    /// observed: a read of the BCL's own culture goes without a word, and a provider a call computes
    /// is a build error, because C# would run the call and the twin has no CultureInfo to run it on.
    /// </summary>
    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decParse(this.str)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decConvert(this.str)")]
    public void ADecimalConversion_LeavesOutAProviderWhoseReadCannotBeObserved(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ1004");
    }

    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"))")]
    [InlineData("decimal.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"), out var v)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"))")]
    public void ADecimalConversionWhoseProviderACallComputes_IsABuildError(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ1004");
    }

    [Fact]
    public void IntTryParse_IntoADiscard_AssignsNothing()
    {
        // Converted as a name, the discard read as a member: `this._ = parseInt(…)` gave the
        // component a property nobody declared.
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
