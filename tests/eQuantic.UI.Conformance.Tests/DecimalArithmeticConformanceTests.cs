using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A decimal's <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> and <c>%</c> answer what .NET's <c>DecCalc</c>
/// answers where the exact result is not one a decimal holds (#764): past 96 bits or 28 places it
/// rounds half to even, to the places that fit, or throws .NET's OverflowException, and its scale is
/// .NET's. The runtime computed on an unbounded mantissa, so <c>decimal.MaxValue + decimal.MaxValue</c>
/// printed a number no decimal holds, and every caller followed: a <c>Sum</c>, an <c>Average</c>.
/// A division computed 28 places whatever the dividend, so a large one kept digits .NET rounds away,
/// and an exact quotient lost the scale .NET keeps.
/// </summary>
public class DecimalArithmeticConformanceTests
{
    /// <summary>A result no decimal holds is .NET's OverflowException, whoever computes it.</summary>
    [SkippableTheory]
    [InlineData("var m = decimal.MaxValue; return (m + m).ToString();")]
    [InlineData("var m = decimal.MinValue; return (m - 1m).ToString();")]
    [InlineData("var m = decimal.MaxValue; return (m * 2m).ToString();")]
    [InlineData("var m = decimal.MaxValue; return (m / 0.5m).ToString();")]
    [InlineData("var m = decimal.MaxValue; m += 1m; return m.ToString();")]
    [InlineData("return new[] { decimal.MaxValue, decimal.MaxValue }.Sum().ToString();")]
    [InlineData("return new[] { decimal.MaxValue, decimal.MaxValue }.Average().ToString();")]
    public void AResultNoDecimalHolds_IsAnOverflow(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"try {{ {statements} }} catch (OverflowException e) {{ return \"overflow: \" + e.Message; }}");
    }

    /// <summary>
    /// A sum or a product past 96 bits or 28 places loses the places that do not fit, rounded half
    /// to even, and so does a quotient, which keeps as many as fit.
    /// </summary>
    [SkippableTheory]
    // Past 96 bits, a place goes: MaxValue less a tenth rounds back to MaxValue.
    [InlineData("var m = decimal.MaxValue; return (m - 0.1m).ToString();")]
    [InlineData("var a = 7922.8162514264337593543950335m; return (a + 4.012027874412007892m).ToString();")]
    [InlineData("var a = 7922.8162514264337593543950335m; return (a * -4.012027874412007892m).ToString();")]
    // Past 28 places, and the halves: to even, and up when anything follows the 5.
    [InlineData("var a = 0.1234567890123456789m; return (a * a).ToString();")]
    [InlineData("var a = 0.0000000000000000000000000005m; return (a * 0.1m).ToString();")]
    [InlineData("var a = 0.0000000000000000000000000015m; return (a * 0.1m).ToString();")]
    [InlineData("var a = 0.0000000000000000000000000025m; return (a * 0.1m).ToString();")]
    [InlineData("var a = 0.0000000000000000000000000251m; return (a * 0.1m).ToString();")]
    // A large dividend keeps the 28 or 29 digits a decimal holds, and no more.
    [InlineData("var m = decimal.MaxValue; return (m / 6m).ToString();")]
    [InlineData("var a = 15518717730321859317m; return (a / 0.00000003512407616471289109m).ToString();")]
    [InlineData("var a = -55.4m; return (a / -7.9228162514264337593543950335m).ToString();")]
    [InlineData("var a = 2m; return (a / 3m).ToString();")]
    [InlineData("var a = 1m; return (a / 3m).ToString();")]
    public void AResultPastWhatADecimalHolds_RoundsAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// The scale of a result is .NET's: an exact quotient keeps the one its operands give, a
    /// remainder smaller than its divisor keeps its own, and a zero product keeps its scale on
    /// .NET's 32-bit path and is a plain 0 on its wide one.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = 4.00m; return (a / 2m).ToString();")]
    [InlineData("var a = 10m; return (a / 0.1m).ToString();")]
    [InlineData("var a = 1m; return (a / 0.5m).ToString();")]
    [InlineData("var a = 1.0m; return (a / 4m).ToString();")]
    [InlineData("return new[] { 2.00m, 4.00m }.Average().ToString();")]
    [InlineData("var a = 1.5m; return (a % 2.00m).ToString();")]
    [InlineData("var a = 0m; return (a % 1.50m).ToString();")]
    [InlineData("var a = -5.5m; return (a % 5.50m).ToString();")]
    [InlineData("var a = 0.00m; return (a * 1.5m).ToString();")]
    [InlineData("var a = 0.00m; return (a * 12345678901.5m).ToString();")]
    [InlineData("var a = 0.0000000000000000000000000001m; return (a * a).ToString();")]
    [InlineData("var a = 0.000000000000001m; return (a * 0.000000000000001m).ToString();")]
    public void AResultsScale_IsTheOneDotNetGives(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
