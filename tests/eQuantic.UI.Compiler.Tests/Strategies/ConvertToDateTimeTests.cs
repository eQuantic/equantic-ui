using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using eQuantic.UI.Compiler.CodeGen;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// <c>Convert.ToDateTime</c> by the overload C# bound (#844), where every overload was
/// <c>String(value)</c>. What each one answers is the conformance suite's
/// (<c>DateTextInTheCultureConformanceTests</c>); these pin where each overload lands and what is refused.
/// </summary>
public class ConvertToDateTimeTests
{
    private const string Globalization = "System.Globalization.";

    /// <summary>A text and an object are read by the twin, in the culture the call names; a date is itself.</summary>
    [Theory]
    [InlineData("Convert.ToDateTime(str)", "$eq.time.dateTime.convert(this.str)")]
    [InlineData("Convert.ToDateTime(str, null)", "$eq.time.dateTime.convert(this.str)")]
    [InlineData("Convert.ToDateTime(str, " + Globalization + "CultureInfo.InvariantCulture)", "$eq.time.dateTime.convert(this.str, true)")]
    [InlineData("Convert.ToDateTime(provider: " + Globalization + "CultureInfo.InvariantCulture, value: str)",
        "$eq.time.dateTime.convert(this.str, true)")]
    [InlineData("Convert.ToDateTime((object)str)", "$eq.time.dateTime.convert(this.str)")]
    [InlineData("Convert.ToDateTime(DateTime.Now)", "$eq.time.dateTime.now()")]
    public void AConvertToDateTime_ReadsByItsOverload(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>A value of a type with no conversion to a date throws .NET's InvalidCastException,
    /// after it is evaluated.</summary>
    [Theory]
    [InlineData("Convert.ToDateTime(Value)", "Int32")]
    [InlineData("Convert.ToDateTime(Active)", "Boolean")]
    [InlineData("Convert.ToDateTime(Amount)", "Double")]
    [InlineData("Convert.ToDateTime(1.5m)", "Decimal")]
    public void AConvertToDateTime_OfAnotherType_Throws(string code, string type) =>
        TestHelper.ConvertExpression(code).Should()
            .Contain($"throw $eq.exceptions.of('System.InvalidCastException', \"Invalid cast from '{type}' to 'DateTime'.\")");

    /// <summary>Any other culture fails the build, as a date's Parse does.</summary>
    [Fact]
    public void AConvertToDateTime_InAnotherCulture_IsRefused() =>
        TestHelper.DiagnosticsFor("Convert.ToDateTime(str, " + Globalization + "CultureInfo.GetCultureInfo(\"pt-BR\"))")
            .Should().Contain(d => d.Code == "EQ2108" && d.Message.Contains("reading a date or a time"));

    /// <summary>With no model, what the value holds is the twin's to read when the call runs.</summary>
    [Fact]
    public void AConvertToDateTime_WithNoModel_IsReadWhenItRuns() =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression("Convert.ToDateTime(s)"))
            .Should().Be("$eq.time.dateTime.convert(s)");
}
