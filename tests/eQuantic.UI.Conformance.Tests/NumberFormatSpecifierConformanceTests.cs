using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A number prints through a format specifier as .NET prints it, on both sides (#393). .NET writes a
/// formatted double from its EXACT binary value and rounds a half by the type (a double's and a
/// float's to even, a decimal's and an integer's away from zero), where <c>toFixed</c> and
/// <c>Intl</c> start from the shortest text that reads back; and the <c>E</c> specifier, which the
/// resx subset admits (EQ2100), had no branch in the formatter at all, so <c>{0:E2}</c> passed the
/// build and printed <c>12345</c>.
/// </summary>
public class NumberFormatSpecifierConformanceTests
{
    [SkippableTheory]
    // E and e: the mantissa's digits, the sign, and an exponent of at least three digits.
    [InlineData("return (12345.0).ToString(\"E2\") + \"|\" + (12345.0).ToString(\"e2\") + \"|\" + (-12345.0).ToString(\"E\");")]
    [InlineData("return (0.0).ToString(\"E2\") + \"|\" + (0.000123).ToString(\"E3\") + \"|\" + (1.5).ToString(\"E2\") + \"|\" + (-0.0).ToString(\"E1\");")]
    [InlineData("return (1e300).ToString(\"E2\") + \"|\" + (1e-300).ToString(\"E2\") + \"|\" + (5e-324).ToString(\"E3\");")]
    [InlineData("return (1.5f).ToString(\"E3\") + \"|\" + 12345m.ToString(\"E2\") + \"|\" + (-0.5m).ToString(\"E1\") + \"|\" + 12345.ToString(\"E0\") + \"|\" + 12345L.ToString(\"E4\");")]
    [InlineData("return string.Format(\"{0:E2}\", 12345.678) + \"|\" + $\"{-0.00012:e1}\" + \"|\" + string.Format(\"{0:E}\", 42);")]
    // An integer's placeholder is found after an escaped brace too, and rounds as an integer (found in review, #445).
    [InlineData("return string.Format(\"{{{0:E1}}}\", 125) + \"|\" + string.Format(\"{{{{{0:E1}\", 125);")]
    // A half rounds by the type: a double's to even, a decimal's and an integer's away from zero.
    [InlineData("return (1.25).ToString(\"E1\") + \"|\" + (2.5).ToString(\"E0\") + \"|\" + (0.125).ToString(\"E1\") + \"|\" + (1.25m).ToString(\"E1\") + \"|\" + (2.5m).ToString(\"E0\") + \"|\" + 125.ToString(\"E1\");")]
    [InlineData("return (2.5).ToString(\"F0\") + \"|\" + (3.5).ToString(\"F0\") + \"|\" + (-2.5).ToString(\"F0\") + \"|\" + (2.5m).ToString(\"F0\") + \"|\" + (0.125).ToString(\"F2\") + \"|\" + (0.125m).ToString(\"F2\");")]
    // F and N from the exact binary value, past the shortest text.
    [InlineData("return (0.1).ToString(\"F20\") + \"|\" + (1.005).ToString(\"F2\") + \"|\" + (0.1f).ToString(\"F10\");")]
    [InlineData("return (1.2345678901234568E+19).ToString(\"N0\") + \"|\" + (16777217f).ToString(\"N0\") + \"|\" + 9007199254740993L.ToString(\"N0\");")]
    [InlineData("return 79228162514264337593543950335m.ToString(\"N0\") + \"|\" + 12345.678.ToString(\"N2\") + \"|\" + (-1234.5).ToString(\"N1\") + \"|\" + long.MinValue.ToString(\"N0\");")]
    // P multiplies the exact value by 100, and the invariant culture writes a space before the sign.
    [InlineData("return (0.125).ToString(\"P1\") + \"|\" + (0.5m).ToString(\"P0\") + \"|\" + (-0.03125).ToString(\"P2\");")]
    // G, R: the shortest text that reads back, or that many significant digits.
    [InlineData("return (0.1 + 0.2).ToString(\"G\") + \"|\" + (1234.5678).ToString(\"G3\") + \"|\" + (0.0001234).ToString(\"G2\") + \"|\" + (0.1).ToString(\"R\");")]
    // D and X: an integer's own digits, padded, and a long's two's complement.
    [InlineData("return 255.ToString(\"X4\") + \"|\" + (-1L).ToString(\"X\") + \"|\" + 42.ToString(\"D5\") + \"|\" + (-42).ToString(\"D5\") + \"|\" + 255.ToString(\"x\");")]
    // A custom picture, rounded from the exact value too.
    [InlineData("return (1234.5).ToString(\"#,##0.00\") + \"|\" + (0.5).ToString(\"0.#\") + \"|\" + (1.005).ToString(\"0.00\") + \"|\" + (2.5).ToString(\"0\") + \"|\" + (2.5m).ToString(\"0\");")]
    // What is not a finite number prints as .NET prints it, under any specifier.
    [InlineData("return double.PositiveInfinity.ToString(\"N2\") + \"|\" + double.NaN.ToString(\"F1\") + \"|\" + double.NegativeInfinity.ToString(\"E2\");")]
    public void ANumber_PrintsThroughItsSpecifierAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
