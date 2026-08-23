using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A Task is a Promise on the other side and `await` is `await`, but until now the harness could
/// not RUN either: it wrapped every block in a plain arrow, where `await` is a SyntaxError, so bun
/// exited before printing and the failure read as a translation bug. These are the first executed
/// async cases in the suite, and the first run found a gap nobody had written down.
/// </summary>
public class AsyncConformanceTests
{
    [SkippableTheory]
    [InlineData("async Task<int> F(int v) => v * 2; var r = await F(21); return $\"{r}\";")]
    [InlineData("async Task<int> F(int v) => v + 1; var a = await F(1); var b = await F(a); return $\"{a}{b}\";")]
    [InlineData("var r = await Task.FromResult(7); return $\"{r}\";")]
    [InlineData("async Task<int> F(int v) => v * 3; var xs = await Task.WhenAll(F(1), F(2)); return $\"{xs[0]}{xs[1]}\";")]
    [InlineData("async Task<int> F(int v) => v; var t = 0; for (var i = 0; i < 3; i++) t += await F(i); return $\"{t}\";")]
    public void AsyncPrograms_MatchDotNet(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program);
    }

    /// <summary>
    /// The gap the first async run found: the TYPE of an awaited call does not reach the value it
    /// produces. `var s = await F();` where F returns a Task&lt;string&gt; leaves `s` untyped, so a
    /// member on it is name-guessed (`.toUpperInvariant()`, which exists nowhere) and a conversion
    /// on it never fires (`l + 2` stays a BigInt beside a Number, which throws). Both work the
    /// moment the same call is synchronous, so it is the await that loses the type and not the
    /// local function. An async local function is also not emitted `async`, so a body that awaits
    /// is a SyntaxError in its own right.
    /// <para>
    /// Held as a LEDGER rather than a skip: this fails when one of them starts working, which is
    /// the direction that would otherwise go unnoticed. Move the case up to the theory above and
    /// delete it here.
    /// </para>
    /// </summary>
    [SkippableFact]
    public void TheAwaitedValueLosesItsType_AndTheseAreTheCasesThatProveIt()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");

        string[] known =
        [
            // a member lookup on an awaited string
            "async Task<string> F() => \"ok\"; var s = await F(); return s.ToUpperInvariant();",
            // a conversion onto an awaited long
            "async Task<long> F() => 40L; var l = await F(); return (l + 2).ToString();",
            // an async local function whose body awaits is not marked async
            "async Task<int> F() { await Task.Yield(); throw new InvalidOperationException(\"x\"); } "
            + "var r = 0; try { r = await F(); } catch { r = -1; } return $\"{r}\";",
        ];

        var nowWorking = known.Where(program =>
        {
            try { ConformanceRunner.AssertStatementsSameAsDotNet(program); return true; }
            catch { return false; }
        }).ToList();

        Assert.True(nowWorking.Count == 0,
            "these async cases now match .NET — move them into AsyncPrograms_MatchDotNet and delete "
            + "them here, so the suite records that the gap closed:\n  " + string.Join("\n  ", nowWorking));
    }
}
