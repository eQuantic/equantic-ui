using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A Task is a Promise on the other side and `await` is `await`, but until recently the harness
/// could not RUN either, so none of this had ever executed.
/// <para>
/// The first run reported three failures as a compiler gap — "the TYPE of an awaited call does not
/// reach the value it produces". **Two of the three were the harness**, not the compiler: its
/// synthesized wrapper had no `using System.Threading.Tasks` and its `__Eval` was not `async`, so
/// `Task&lt;string&gt;` did not bind and `await` was not even valid C# there. The tree came back
/// full of errors and eqc did what it says it does when the model cannot answer — guessed a name.
/// The compiler was right and the instrument was wrong, which is the third time in one day.
/// </para>
/// <para>
/// The third was real: an async LOCAL FUNCTION (and `async delegate`) lost its `async`, so a body
/// that awaited became a SyntaxError and the module failed to parse. Lambdas and component methods
/// already carried it; those two dropped it.
/// </para>
/// </summary>
public class AsyncConformanceTests
{
    [SkippableTheory]
    [InlineData("async Task<int> F(int v) => v * 2; var r = await F(21); return $\"{r}\";")]
    [InlineData("async Task<int> F(int v) => v + 1; var a = await F(1); var b = await F(a); return $\"{a}{b}\";")]
    [InlineData("var r = await Task.FromResult(7); return $\"{r}\";")]
    [InlineData("async Task<int> F(int v) => v * 3; var xs = await Task.WhenAll(F(1), F(2)); return $\"{xs[0]}{xs[1]}\";")]
    [InlineData("async Task<int> F(int v) => v; var t = 0; for (var i = 0; i < 3; i++) t += await F(i); return $\"{t}\";")]
    // The awaited value keeps its TYPE: a member on it resolves rather than being name-guessed,
    // and a conversion onto it fires. Both were reported as broken and were the harness.
    [InlineData("async Task<string> F() => \"ok\"; var s = await F(); return s.ToUpperInvariant();")]
    [InlineData("async Task<long> F() => 40L; var l = await F(); return (l + 2).ToString();")]
    // The one that WAS a compiler bug: an async local function whose body awaits. Without `async`
    // on the emitted arrow this is a SyntaxError and the whole module fails to parse.
    //
    // The yield is awaited OUTSIDE any catch on purpose. Written with the throw inside a
    // try/catch, this case passed while `Task.Yield()` emitted `Task.yield()` — a name that
    // exists nowhere — because the catch swallowed the ReferenceError and the fold still read
    // -1. A green tick for the wrong reason is worse than a red one.
    [InlineData("async Task<int> F(int v) { await Task.Yield(); return v * 4; } "
        + "var a = await F(5); return $\"{a}\";")]
    [InlineData("async Task<int> F() { await Task.Yield(); throw new InvalidOperationException(\"x\"); } "
        + "var r = 0; try { r = await F(); } catch { r = -1; } return $\"{r}\";")]
    // `async delegate` is the same shape, and dropped it the same way.
    [InlineData("Func<Task<int>> f = async delegate { await Task.Yield(); return 9; }; "
        + "var r = await f(); return $\"{r}\";")]
    public void AsyncPrograms_MatchDotNet(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program);
    }

    /// <summary>
    /// The rest of the Task surface, swept after the first two bugs rather than waiting for the
    /// third to be reported. Three more were broken, and all three are everyday async C#:
    /// <list type="bullet">
    /// <item><c>ConfigureAwait</c> is an INSTANCE call, so the strategy's `Task.` gate never saw it
    /// and it emitted `.configureAwait(false)` — a method that exists nowhere.</item>
    /// <item><c>Task.CompletedTask</c> is a PROPERTY, so the invocation gate never saw it
    /// either.</item>
    /// <item><c>WhenAll</c>/<c>WhenAny</c> with ONE argument passed it to `Promise.all`/`race`
    /// unwrapped. A promise is not iterable, so a single-task call threw — and the model is what
    /// tells a lone task from a sequence, since the two are the same syntax.</item>
    /// </list>
    /// </summary>
    [SkippableTheory]
    [InlineData("async Task<int> F() => 7; var r = await F().ConfigureAwait(false); return $\"{r}\";")]
    [InlineData("async Task F() { await Task.CompletedTask; } await F(); return \"ok\";")]
    [InlineData("await Task.Delay(1); return \"ok\";")]
    [InlineData("var r = await Task.Run(() => 6 * 7); return $\"{r}\";")]
    // ONE task, not a sequence: the shape that threw.
    [InlineData("async Task<int> F(int v) => v; var t = await Task.WhenAny(F(1)); return $\"{await t}\";")]
    [InlineData("async Task<int> F(int v) => v * 2; var xs = await Task.WhenAll(F(3)); return $\"{xs[0]}\";")]
    // …and a SEQUENCE, which must still go through unwrapped.
    [InlineData("async Task<int> F(int v) => v; var ts = new[] { F(1), F(2) }.ToList(); "
        + "var xs = await Task.WhenAll(ts); return $\"{xs[0]}{xs[1]}\";")]
    [InlineData("var r = 0; try { await Task.FromException(new InvalidOperationException(\"x\")); } "
        + "catch { r = -1; } var ok = 5; return $\"{r}{ok}\";")]
    [InlineData("async ValueTask<int> F() => 3; var r = await F(); return $\"{r}\";")]
    [InlineData("async Task F(int v) { var _ = v; } await F(2); return \"done\";")]
    [InlineData("Func<Task<int>> f = async () => { await Task.Yield(); return 11; }; return $\"{await f()}\";")]
    public void TheTaskSurface_MatchesDotNet(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program);
    }
}
