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
    // X and B write a negative integer as its two's complement at its type's width, and B is binary
    // (found in review, #445): an int's -1 is FFFFFFFF and a short's FFFF, where JavaScript wrote -1.
    [InlineData("return ((short)-1).ToString(\"X\") + \"|\" + ((sbyte)-2).ToString(\"x\") + \"|\" + (-255).ToString(\"X4\") + \"|\" + ((byte)255).ToString(\"X\") + \"|\" + 4294967295u.ToString(\"X\") + \"|\" + ((ushort)65535).ToString(\"x\");")]
    [InlineData("short s = -1; sbyte b = -2; return $\"{s:X}\" + \"|\" + string.Format(\"{0:X4}|{1:x}\", b, s) + \"|\" + 42.ToString(\"B\") + \"|\" + s.ToString(\"B\") + \"|\" + 42.ToString(\"B8\") + \"|\" + (-2L).ToString(\"b\");")]
    // D, X and B take an integer only: a fraction, a float and a decimal throw, as .NET throws; and so
    // does a letter no number takes, and a precision past .NET's limit, whose leading zeros are allowed.
    [InlineData("try { return (2.5).ToString(\"D\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return (1.5f).ToString(\"X\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return (42m).ToString(\"B\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return (5).ToString(\"Z\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return (1.5).ToString(\"F1000000000\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return (1.5).ToString(\"F0000000002\");")]
    // A precision past the 100 digits Intl writes after the point, which was cut to 100 (found in
    // review, #445), rounded where the cut falls: inside the digits, at their first, and before it.
    [InlineData("return (0.1).ToString(\"F101\") + \"|\" + (1.0 / 3.0).ToString(\"N105\") + \"|\" + (0.125).ToString(\"P101\");")]
    [InlineData("return (1.0 / 3.0).ToString(\"E150\") + \"|\" + 42.ToString(\"D150\") + \"|\" + (1.5m).ToString(\"C101\") + \"|\" + (-0.5m).ToString(\"F102\") + \"|\" + (9.5).ToString(\"F101\");")]
    [InlineData("return (5e-324).ToString(\"F330\") + \"|\" + (-1e-200).ToString(\"F150\") + \"|\" + (7e-102).ToString(\"F101\") + \"|\" + (3e-102).ToString(\"F101\") + \"|\" + (-7e-102).ToString(\"N101\");")]
    // A custom picture is drawn as .NET draws it: percent and per mille, text quoted, escaped or as
    // it stands, sections, scaling commas and exponents (found in review, #445).
    [InlineData("return (0.25).ToString(\"0%\") + \"|\" + (0.0125).ToString(\"0.0‰\") + \"|\" + (12.5).ToString(\"0.0 'KB'\") + \"|\" + (12.5).ToString(\"0.0 KB\") + \"|\" + (5.0).ToString(\"\\\\#0\") + \"|\" + (0.5).ToString(\"# %\");")]
    [InlineData("return (-1.0).ToString(\"0.00;(0.00)\") + \"|\" + (0.0).ToString(\"0.00;(0.00);'zero'\") + \"|\" + (0.001).ToString(\"0.00;(0.00);zero\") + \"|\" + (-0.001).ToString(\"0.00;(0.00);zero\") + \"|\" + (-5.0).ToString(\"0;\") + \"|\" + (-1234.5).ToString(\"#,##0.0;-#,##0.0\");")]
    [InlineData("return (-0.001).ToString(\"0.00\") + \"|\" + (-0.0).ToString(\"0.00\") + \"|\" + (-0.001m).ToString(\"0.00\") + \"|\" + (-0.4).ToString(\"0\") + \"|\" + (-0.4).ToString(\"0;(0)\") + \"|\" + (-0.0).ToString(\"0;(0);z\") + \"|\" + (0.4).ToString(\"#\") + \"[\" + (-0.4).ToString(\"#\") + \"]\";")]
    [InlineData("return (1234567.0).ToString(\"#,##0,\") + \"|\" + (1234567.0).ToString(\"#,##0,,\") + \"|\" + (1234567.0).ToString(\"0.00E+00\") + \"|\" + (0.000123).ToString(\"0.0e0\") + \"|\" + (1234567.0).ToString(\"##0.0E-0\") + \"|\" + (1e20).ToString(\"#,##0\");")]
    // A picture's exponent takes at most ten digits, however many zeros it asks for: .NET caps it
    // (`if (i > 10) i = 10;` in NumberToStringFormat), measured in review (#445).
    [InlineData("return (1234567.0).ToString(\"0.0E+00000000000\") + \"|\" + (1234567.0).ToString(\"0.0E-000000000000\");")]
    [InlineData("return (5).ToString(\"Total\") + \"|\" + (-5).ToString(\"abc\") + \"|\" + (1.5).ToString(\"N2x\") + \"|\" + (0.1).ToString(\"#.##\") + \"|\" + (12345.6789).ToString(\"#,##0.00##\") + \"|\" + (12345678.9f).ToString(\"#,##0.00\") + \"|\" + 1234567.ToString(\"0,0\");")]
    // G at the edges of its fixed notation, and R on a decimal, an int and a long, which .NET 10 takes
    // (measured in review, #445: neither diverged).
    [InlineData("return (1e15).ToString() + \"|\" + (1e16).ToString() + \"|\" + (1e17).ToString() + \"|\" + (0.00001).ToString() + \"|\" + (99.9).ToString(\"G2\") + \"|\" + (0.00001).ToString(\"G2\") + \"|\" + (123456.0).ToString(\"G5\") + \"|\" + (1e7f).ToString() + \"|\" + (1e16).ToString(\"G17\");")]
    [InlineData("return (12.50m).ToString(\"R\") + \"|\" + 42.ToString(\"R\") + \"|\" + 42L.ToString(\"R\") + \"|\" + (1e15).ToString(\"R\") + \"|\" + 125.ToString(\"G2\");")]
    public void ANumber_PrintsThroughItsSpecifierAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
