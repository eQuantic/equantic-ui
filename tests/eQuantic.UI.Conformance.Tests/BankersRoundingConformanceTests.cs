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
    // The midpoint is EXACT: a value near a half is not a half. A tolerance once took both of these.
    [InlineData("Math.Round(2.5000000001)")]  // -> 3
    [InlineData("Math.Round(0.5015, 3)")]     // -> 0.501: 0.5015 * 1000 is 501.49999999999994
    // Every mode, by the overload the argument COUNT cannot tell from Round(x, digits).
    [InlineData("Math.Round(2.5, MidpointRounding.AwayFromZero)")]      // -> 3
    [InlineData("Math.Round(-2.5, MidpointRounding.AwayFromZero)")]     // -> -3
    [InlineData("Math.Round(2.345, 2, MidpointRounding.AwayFromZero)")] // -> 2.35 (2.345 * 100 is 234.50000000000003)
    [InlineData("Math.Round(2.5, MidpointRounding.ToEven)")]            // -> 2
    [InlineData("Math.Round(1.7, MidpointRounding.ToZero)")]            // -> 1
    [InlineData("Math.Round(-1.5, MidpointRounding.ToNegativeInfinity)")] // -> -2
    [InlineData("Math.Round(1.2, MidpointRounding.ToPositiveInfinity)")]  // -> 2
    public void BankersRounding_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
