using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for format specifiers (regression for #12). x.ToString("F2") and {x:F2}
/// interpolation emit a call to the real runtime `format` helper (imported from the bundled
/// runtime.js), so this validates the actual shipped behavior. Culture-sensitive specifiers
/// (C/P and ambiguous half-rounding) are avoided so the comparison stays deterministic.
/// </summary>
public class FormatConformanceTests
{
    [SkippableTheory]
    [InlineData("(3.14159).ToString(\"F2\")")]   // -> "3.14"
    [InlineData("(3.14159).ToString(\"F0\")")]   // -> "3"
    [InlineData("(3.14159).ToString(\"F4\")")]   // -> "3.1416"
    [InlineData("(255).ToString(\"X\")")]        // -> "FF"
    [InlineData("(1000).ToString(\"N0\")")]      // -> "1,000" (en-US)
    [InlineData("$\"pi={3.14159:F2}\"")]         // -> "pi=3.14"
    [InlineData("$\"hex={255:X}\"")]             // -> "hex=FF"
    [InlineData("$\"{1 + 1} and {2 * 2}\"")]     // unformatted interpolation still works
    public void Format_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
