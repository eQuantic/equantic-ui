using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// `ConfigureAwait` is dropped, because JavaScript has one context to resume on. Two things have
/// to be true for that to be safe, and neither was when the drop was first written:
/// the call must actually BE the BCL's (a name gate takes anybody's method of that name and
/// silently discards it), and its argument must not be able to do anything (dropping the call
/// drops the argument, and a side effect that stops happening surfaces nowhere near its cause).
/// </summary>
public class ConfigureAwaitGateTests
{
    [SkippableTheory]
    [InlineData("async Task<int> F() => 7; var r = await F().ConfigureAwait(false); return $\"{r}\";")]
    [InlineData("async Task<int> F() => 7; var r = await F().ConfigureAwait(true); return $\"{r}\";")]
    [InlineData("async ValueTask<int> F() => 8; var r = await F().ConfigureAwait(false); return $\"{r}\";")]
    // A constant that is not a literal is still a constant: nothing to evaluate.
    [InlineData("const bool Ctx = false; async Task<int> F() => 9; var r = await F().ConfigureAwait(Ctx); return $\"{r}\";")]
    public void TheBclConfigureAwait_IsDroppedAndTheValueSurvives(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program);
    }

    /// <summary>
    /// Somebody else's `ConfigureAwait` is a real method and has to KEEP running. Under the old
    /// name gate this returned the receiver and the method never ran at all — silently, since the
    /// emitted code is perfectly valid JavaScript that simply does less.
    /// </summary>
    [SkippableFact]
    public void AConfigureAwaitOnSomebodyElsesType_IsNotDropped()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");

        // A RECORD, because the harness emits those to the other side and a plain class it does
        // not — the type has to exist over there for the call to be observable at all.
        const string prelude = """
            public sealed record Meter(int Reads)
            {
                public Meter ConfigureAwait(bool flag) => new Meter(Reads + (flag ? 2 : 1));
            }
            """;

        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var m = new Meter(0).ConfigureAwait(false).ConfigureAwait(true); return $\"{m.Reads}\";",
            prelude);
    }
}
