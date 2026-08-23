using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The harness checked against itself. A differential suite is only worth its runtime if a
/// program it CANNOT run fails loudly — a silent skip turns every unsupported construct into a
/// green tick, which is the one outcome worse than a red one.
/// </summary>
public class HarnessSelfChecksTests
{
    [SkippableFact]
    public void AProgramThatDoesNotCompile_FailsRatherThanPasses()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");

        // Not valid C# in any language version, so neither side can produce a value.
        Assert.ThrowsAny<Exception>(() =>
            ConformanceRunner.AssertStatementsSameAsDotNet("var x = ; return $\"{x}\";", ""));
    }

    /// <summary>
    /// Both sides parse with <c>LanguageVersion.Preview</c>, the same as eqc. On Roslyn's default
    /// — the latest RELEASED version — a construct eqc accepts would fail to parse in the harness
    /// and read as a translation bug rather than as a harness that lags.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = -3; var b = a >>> 1; return $\"{b}\";")]              // C# 11
    [InlineData("var xs = new[] { 1, 2, 3 }; return $\"{xs[^1]}{xs[1..].Length}\";")]  // C# 8
    [InlineData("int Twice(int v) => v * 2; return $\"{Twice(21)}\";")]        // local function
    public void TheHarnessAcceptsWhatEqcAccepts(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program, "");
    }
}
