using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance sweep across numeric operators, bitwise ops, parsing and conversions.
/// Drives the .NET coverage program (docs/DOTNET-COVERAGE-PROGRAM.md): failures are triaged into a
/// native strategy fix, a TS compat helper, or a fail-on-unsupported diagnostic.
/// </summary>
public class NumericConformanceTests
{
    [SkippableTheory]
    // Bitwise & shift (JS operates on 32-bit ints, matching C# int)
    [InlineData("1 << 4")]
    [InlineData("256 >> 2")]
    [InlineData("12 & 10")]
    [InlineData("12 | 3")]
    [InlineData("12 ^ 10")]
    [InlineData("~5")]
    // Comparison / boolean
    [InlineData("7 > 3 && 2 < 5")]
    [InlineData("7 < 3 || 2 < 5")]
    [InlineData("!(3 == 4)")]
    // Double arithmetic (IEEE-754, shared with JS)
    [InlineData("1.5 + 2.25")]
    [InlineData("0.1 + 0.2")]
    [InlineData("10.0 / 4.0")]
    // Parsing (invariant)
    [InlineData("int.Parse(\"42\")")]
    [InlineData("int.Parse(\"-7\")")]
    [InlineData("double.Parse(\"3.14\")")]
    [InlineData("bool.Parse(\"true\")")]
    [InlineData("bool.Parse(\"false\")")]
    // Limits
    [InlineData("int.MaxValue")]
    [InlineData("int.MinValue")]
    // Convert
    [InlineData("Convert.ToInt32(\"42\")")]
    [InlineData("Convert.ToDouble(\"2.5\")")]
    [InlineData("Convert.ToString(42)")]
    [InlineData("Convert.ToBoolean(1)")]
    public void Numeric_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
