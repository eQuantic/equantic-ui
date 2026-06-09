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
}
