using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET banker's rounding (MidpointRounding.ToEven), now backed by the runtime
/// `round` compat helper. These midpoint cases previously diverged (JS Math.round is half-up) and
/// were kept out of the corpus; with the helper they match .NET. First proof of the eq compat-runtime
/// mechanism end-to-end (TS helper + strategy + harness importing the real helper).
/// </summary>
public class BankersRoundingConformanceTests
{
    [SkippableTheory]
    [InlineData("Math.Round(2.5)")]    // -> 2 (even), not 3
    [InlineData("Math.Round(3.5)")]    // -> 4
    [InlineData("Math.Round(0.5)")]    // -> 0
    [InlineData("Math.Round(1.5)")]    // -> 2
    [InlineData("Math.Round(-2.5)")]   // -> -2
    [InlineData("Math.Round(-3.5)")]   // -> -4
    [InlineData("Math.Round(2.4)")]    // -> 2 (non-midpoint, normal)
    [InlineData("Math.Round(2.6)")]    // -> 3
    [InlineData("Math.Round(3.14159, 2)")] // -> 3.14
    [InlineData("Convert.ToInt32(2.5)")]   // -> 2 (Convert uses banker's too)
    [InlineData("Convert.ToInt32(3.5)")]   // -> 4
    public void BankersRounding_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
