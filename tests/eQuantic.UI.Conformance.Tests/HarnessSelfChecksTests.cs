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
    /// The bundle every helper-using case imports LOADS. When it did not — a hydration map named a
    /// class, as a static field initializer, inside an import cycle that had not defined it yet — a
    /// few hundred cases failed at once, each naming its own expression and none naming the one
    /// ReferenceError they shared.
    /// </summary>
    [SkippableFact]
    public void TheRuntimeBundle_Loads()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Assert.Equal("object", ConformanceRunner.ImportTheRuntimeBundle());
    }

    /// <summary>
    /// A representation the translation got wrong FAILS (#596). The .NET side writes a value as the
    /// runtime holds it, so a long that became a JS number, a decimal that became one, an enum written
    /// as its number, a float left a double and a tuple written as an object each print what .NET's
    /// value does not, where System.Text.Json's own JSON matched the first four. The right
    /// representation, printed the same way, matches.
    /// </summary>
    [SkippableTheory]
    [InlineData("return 5L;", "5", "5n")]
    [InlineData("return 1.5m;", "1.5", null)]
    [InlineData("return Day.Friday;", "1", "'friday'")]
    [InlineData("return 0.1f;", "0.1", "Math.fround(0.1)")]
    [InlineData("return (1, 2L);", "({ item1: 1, item2: 2n })", "[1, 2n]")]
    public void AWrongRepresentation_Fails(string statements, string wrong, string? right)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var expected = DotNetEvaluator.EvaluateToJson(statements, "public enum Day { Monday, Friday }");
        Assert.NotEqual(expected, JsExecutor.Run(ConformanceRunner.Print(wrong)));
        if (right is not null) Assert.Equal(expected, JsExecutor.Run(ConformanceRunner.Print(right)));
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
