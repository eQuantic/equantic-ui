using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// M0 walking skeleton + first real cases: proves the harness loop end-to-end
/// (transpile → run JS → compare to .NET) and that integer vs floating division is correct.
/// Skips gracefully where no JS engine is available.
/// </summary>
public class ArithmeticConformanceTests
{
    [SkippableTheory]
    [InlineData("7 / 2")]      // int division truncates -> 3
    [InlineData("-7 / 2")]     // truncation toward zero -> -3
    [InlineData("7.0 / 2")]    // floating division -> 3.5 (must NOT truncate)
    [InlineData("7 % 3")]      // modulo -> 1
    [InlineData("-7 % 3")]     // modulo on negative -> -1
    [InlineData("3 + 4 * 2")]  // precedence -> 11
    [InlineData("(3 + 4) * 2")] // parens -> 14
    public void Arithmetic_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine (embedded Bun or Node) available on this machine.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
