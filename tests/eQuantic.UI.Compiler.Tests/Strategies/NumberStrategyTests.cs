using FluentAssertions;
using eQuantic.UI.Compiler.CodeGen;
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
    /// The browser reads a number in the invariant culture and no other. A provider that names it
    /// is left out without a word; no provider is EQ2110, because C# then reads in the request's
    /// culture ("1,5" is 1.5 in pt) and the browser reads 15; any other provider is EQ2108.
    /// </summary>
    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decParse(this.str)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decConvert(this.str)")]
    [InlineData("Convert.ToDecimal((object)str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decConvert(this.str)")]
    public void ADecimalReadInTheInvariantCulture_LeavesTheProviderOut(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110" || d.Code == "EQ1004");
    }

    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.CurrentCulture)")]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"))")]
    [InlineData("decimal.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"), out var v)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.CurrentCulture)")]
    // A value that may be text when the call runs is read in the culture the call names.
    [InlineData("Convert.ToDecimal((object)str, System.Globalization.CultureInfo.CurrentCulture)")]
    [InlineData("Convert.ToDecimal((IConvertible)str, System.Globalization.CultureInfo.CurrentCulture)")]
    public void ADecimalReadInAnotherCulture_IsABuildError(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2108");
    }

    [Theory]
    [InlineData("decimal.Parse(str)")]
    [InlineData("decimal.Parse(str, null)")]
    [InlineData("decimal.TryParse(str, out var v)")]
    [InlineData("Convert.ToDecimal(str)")]
    [InlineData("Convert.ToDecimal((object)str)")]
    public void ADecimalReadWithNoCulture_IsWarnedAbout(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2110" && d.Severity == ConversionSeverity.Warning);
    }

    /// <summary>A number converts into a decimal with no culture involved, so it is not asked for one.</summary>
    [Theory]
    [InlineData("Convert.ToDecimal(Value)")]
    [InlineData("Convert.ToDecimal(Amount)")]
    [InlineData("Convert.ToDecimal(Active)")]
    public void ANumberConvertedToDecimal_NeedsNoCulture(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110");
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
