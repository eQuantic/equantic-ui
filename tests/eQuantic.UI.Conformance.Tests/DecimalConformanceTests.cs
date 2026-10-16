using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET `decimal` — exact base-10 arithmetic via the runtime Decimal compat class.
/// Naive doubles diverge (0.1 + 0.2 == 0.30000000000000004); the Decimal type matches .NET.
/// Arithmetic results are compared via ToString() to preserve decimal scale/trailing zeros
/// (which JSON-number serialization would normalize away); comparisons return bool.
/// </summary>
public class DecimalConformanceTests
{
    [SkippableTheory]
    [InlineData("(0.1m + 0.2m).ToString()")]   // "0.3", not 0.30000000000000004
    [InlineData("(1.1m + 2.2m).ToString()")]   // "3.3"
    [InlineData("(5m - 2.5m).ToString()")]     // "2.5"
    // A decimal CONCATENATED into text is text: "v=" + 2.5m once emitted 'v='.add(...).
    [InlineData("\"v=\" + 2.5m")]              // "v=2.5"
    [InlineData("2.5m + \"|\"")]               // "2.5|"
    [InlineData("(1.5m * 2m).ToString()")]     // "3.0" (scale preserved)
    [InlineData("(10m / 4m).ToString()")]      // "2.5"
    [InlineData("(1m / 4m).ToString()")]       // "0.25"
    [InlineData("(1.1m + 1).ToString()")]      // mixed decimal+int -> "2.1"
    [InlineData("(100.50m).ToString()")]       // literal scale preserved -> "100.50"
    // Comparisons (bool result)
    [InlineData("0.1m + 0.2m == 0.3m")]        // true (exact!) — double would be false
    [InlineData("1.1m + 2.2m == 3.3m")]        // true
    [InlineData("2.5m < 2.6m")]                // true
    [InlineData("3m > 2.6m")]                  // true
    [InlineData("2.5m == 2.50m")]              // true
    public void Decimal_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>
    /// A decimal REMAINDER is exact and stays a decimal, with the dividend's sign, binary, lifted
    /// and compound. The runtime's Decimal had no remainder, so `%` computed on the two values as
    /// doubles: `0.3m % 0.1m` was 0.09999999999999998, and the result was a plain number from then on.
    /// </summary>
    [SkippableTheory]
    [InlineData("decimal a = 5.5m, b = 2m; return (a % b).ToString();")]                                  // "1.5"
    [InlineData("decimal a = 0.3m, b = 0.1m; return (a % b).ToString();")]                                // "0.0"
    [InlineData("decimal a = -5.5m; return (a % 2m).ToString();")]                                       // "-1.5"
    [InlineData("decimal a = 5.50m; return (a % 2m).ToString();")]                                       // "1.50"
    [InlineData("decimal a = -4.0m; return (a % 2m).ToString();")]
    [InlineData("decimal a = 5.5m; return ((a % 2m) + 1m).ToString();")]                                 // "2.5": still a decimal
    [InlineData("decimal m = 0.3m; m %= 0.1m; return m.ToString();")]                                     // "0.0"
    [InlineData("decimal a = 1m, z = 0m; try { return (a % z).ToString(); } catch (DivideByZeroException) { return \"div0\"; }")]
    [InlineData("decimal? a = 0.3m, b = 0.1m; return (a % b).ToString();")]                               // "0.0"
    [InlineData("decimal? a = null, b = 0.1m; var r = a % b; return r == null ? \"null\" : r.ToString();")]
    [InlineData("decimal? m = 0.3m; m %= 0.1m; return m.ToString();")]                                    // "0.0"
    [InlineData("decimal? m = null; m %= 2m; return m == null ? \"null\" : m.ToString();")]
    [InlineData("decimal a = 10m; return (a % 3m).ToString();")]                                         // "1": control
    public void ADecimalRemainder_IsExactAndStaysADecimal(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A decimal's <c>Scale</c> is the runtime Decimal's own <c>scale</c>, which keeps the digits .NET
    /// keeps: a literal's, a sum's and a product's, a quotient's, a remainder's, a parsed text's and a
    /// rounding's. It is read by name, so the build's fence on a .NET member nothing carries lets it
    /// through (#847), and these hold the claim it rests on.
    /// </summary>
    [SkippableTheory]
    [InlineData("return (int)100.50m.Scale;")]                                                            // 2
    [InlineData("decimal a = 1.5m; return (int)(a * 2m).Scale;")]                                         // 1
    [InlineData("decimal a = 1.10m, b = 2.2m; return (int)(a + b).Scale;")]                               // 2
    [InlineData("decimal a = 1m; return (int)(a / 3m).Scale;")]                                           // 28
    [InlineData("decimal a = 10m; return (int)(a / 4m).Scale;")]                                          // 1
    [InlineData("decimal a = 0.3m; return (int)(a % 0.1m).Scale;")]                                       // 1
    [InlineData("return (int)decimal.Parse(\"1.230\", System.Globalization.CultureInfo.InvariantCulture).Scale;")] // 3
    [InlineData("decimal a = 1.2345m; return (int)Math.Round(a, 2).Scale;")]                              // 2
    [InlineData("decimal? a = 2.50m; return a?.Scale ?? 99;")]                                            // 2
    [InlineData("decimal a = 7m; return (int)a.Scale;")]                                                  // 0
    public void ADecimalsScale_IsTheOneDotNetKeeps(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
