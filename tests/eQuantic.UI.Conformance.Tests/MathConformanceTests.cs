using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance: System.Math members map to behavior-identical JS. Covers the regressions for
/// Math.Truncate/Ceiling (which used to camelCase to non-existent functions) and Math.Round(x, n)
/// (which used to drop the precision argument). Banker's rounding (Math.Round(2.5)==2 in .NET) is a
/// documented gap and intentionally not asserted here.
/// </summary>
public class MathConformanceTests
{
    [SkippableTheory]
    [InlineData("Math.Truncate(3.7)")]      // regression: -> Math.trunc, not Math.truncate
    [InlineData("Math.Truncate(-3.7)")]
    [InlineData("Math.Ceiling(3.2)")]       // regression: -> Math.ceil, not Math.ceiling
    [InlineData("Math.Floor(3.8)")]
    [InlineData("Math.Round(3.14159, 2)")]  // regression: precision must be honored -> 3.14
    [InlineData("Math.Round(1.005, 1)")]
    [InlineData("Math.Abs(-5)")]
    [InlineData("Math.Abs(-5.5)")]
    [InlineData("Math.Max(3, 7)")]
    [InlineData("Math.Min(3, 7)")]
    [InlineData("Math.Sqrt(16)")]
    [InlineData("Math.Pow(2, 10)")]
    [InlineData("Math.Sign(-42)")]
    public void Math_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
