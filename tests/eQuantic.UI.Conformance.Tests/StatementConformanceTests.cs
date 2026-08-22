using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for C# <b>control-flow statements</b> (not just expressions): if/else, for, foreach,
/// while, do-while, switch (statement form), break/continue, nested loops, try/catch/finally and local
/// functions. Each block runs to a <c>return</c>; the transpiled JS is executed in an IIFE and its
/// result is compared against the same block evaluated by .NET. Cases avoid runtime-divergent throws
/// (e.g. integer-divide-by-zero, out-of-range indexing) since JS would not raise them.
/// </summary>
public class StatementConformanceTests
{
    [SkippableTheory]
    // if / else
    [InlineData("int x = 7; if (x > 5) { return \"big\"; } else { return \"small\"; }")]   // "big"
    [InlineData("int x = 3; if (x > 5) return \"big\"; return \"small\";")]                 // "small" (braceless)
    // A brace-less body whose expression declares a pattern variable: the hoisted `let` and the
    // statement must BOTH stay inside the construct (the writer braces the pair).
    [InlineData("var xs = new[] { \"a\", \"\" }; string r = \"\"; foreach (var x in xs) r += x is { Length: > 0 } v ? v : \"-\"; return r;")] // "a-"
    [InlineData("int i = 0; string r = \"\"; while (i < 2) if (\"ab\"[i++] is var c) r += c; return r;")]   // "ab"
    [InlineData("int n = 0; for (int i = 0; i < 3; i++) if (i is > 0 and var k) n += k; return n;")]          // 3
    // for, accumulation
    [InlineData("int sum = 0; for (int i = 1; i <= 5; i++) { sum += i; } return sum;")]     // 15
    // foreach over an array
    [InlineData("var nums = new[] { 1, 2, 3, 4 }; int total = 0; foreach (var n in nums) { total += n; } return total;")] // 10
    // DECONSTRUCTING foreach — C# tuples are JS arrays, so `var (a, b)` is array destructuring.
    [InlineData("var pairs = new[] { (1, 2), (3, 4) }; int total = 0; foreach (var (a, b) in pairs) { total += a * b; } return total;")] // 14
    [InlineData("var pairs = new[] { (\"a\", 1), (\"b\", 2) }; string r = \"\"; foreach (var (k, v) in pairs) { r += k + v; } return r;")] // "a1b2"
    // the equivalent `(var a, var b)` spelling — a different Roslyn shape for the same thing
    [InlineData("var pairs = new[] { (2, 5), (3, 7) }; int total = 0; foreach ((var a, var b) in pairs) { total += a + b; } return total;")] // 17
    // nested deconstruction
    [InlineData("var rows = new[] { (1, (2, 3)) }; int total = 0; foreach (var (a, (b, c)) in rows) { total += a + b + c; } return total;")] // 6
    // TARGET-TYPED construction of the .NET compat types — `DateTime x = new(…)`, ordinary
    // modern C#. Only the explicit `new DateTime(…)` was recognized, so a page with a target-typed
    // field emitted the C# TYPE NAME into JavaScript and died at run time with "DateTime is not
    // defined", nowhere near the declaration that caused it.
    [InlineData("DateTime moment = new(2024, 1, 15); return moment.ToString();")]
    [InlineData("DateTime moment = new(2024, 1, 5, 9, 3, 7); return moment.AddDays(2).ToString();")]
    [InlineData("TimeSpan span = new(1, 30, 0); return span.ToString();")]
    [InlineData("DateOnly day = new(2024, 3, 9); return day.ToString();")]
    [InlineData("TimeOnly clock = new(14, 5, 9); return clock.ToString();")]
    [InlineData("DateTimeOffset stamp = new(new DateTime(2024, 1, 15), TimeSpan.Zero); return stamp.Year;")]
    // MULTI-DECLARATOR statements — `float x0, y0;` is several variables in one statement, with
    // and without initializers; only the first used to survive, and `y0 is not defined` waited
    // at runtime (found by the mermaid layout, the first shared code to write one).
    [InlineData("float x0, y0, x1, y1; x0 = 1; y0 = 2; x1 = 3; y1 = 4; return x0 + y0 + x1 + y1;")] // 10
    [InlineData("int a = 2, b = 3, c; c = a * b; return c;")] // 6
    // while
    [InlineData("int i = 0; int count = 0; while (i < 10) { i += 3; count++; } return count;")] // 4
    // do-while (body runs at least once)
    [InlineData("int n = 0; int c = 0; do { c++; n += 2; } while (n < 5); return c;")]      // 3
    // switch statement
    [InlineData("int x = 2; string r; switch (x) { case 1: r = \"one\"; break; case 2: r = \"two\"; break; default: r = \"other\"; break; } return r;")] // "two"
    [InlineData("int x = 9; string r; switch (x) { case 1: r = \"one\"; break; default: r = \"other\"; break; } return r;")] // "other"
    // break + continue
    [InlineData("int sum = 0; for (int i = 0; i < 5; i++) { if (i == 3) break; if (i % 2 == 0) continue; sum += i; } return sum;")] // 1
    // nested loops
    [InlineData("int result = 0; for (int i = 1; i <= 3; i++) { for (int j = 1; j <= 3; j++) { result += i * j; } } return result;")] // 36
    // try / catch (explicit throw — raised by both runtimes)
    [InlineData("string r; try { throw new Exception(\"boom\"); } catch { r = \"caught\"; } return r;")] // "caught"
    // try / finally (no exception)
    [InlineData("string r = \"\"; try { r += \"t\"; } finally { r += \"f\"; } return r;")] // "tf"
    // local function
    [InlineData("int Square(int n) { return n * n; } return Square(6);")]                   // 36
    public void Statements_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A <c>using</c> on a type of your own disposes it — the twin carries Dispose as
    /// <c>dispose()</c>, the statement form lowers to try/finally, and the DECLARATION form owns the
    /// rest of its block: disposed after the return value is taken, when the body throws, and in
    /// reverse order when several share a block.</summary>
    [Theory]
    [InlineData("var log = new List<string>(); using (var r = new Res(log, \"r\")) { log.Add(\"body\"); } return string.Join(\",\", log);")]   // "body,r"
    [InlineData("var log = new List<string>(); string F() { using var r = new Res(log, \"r\"); log.Add(\"body\"); return string.Join(\",\", log); } return F();")] // "body" — the value is taken before the dispose
    [InlineData("var log = new List<string>(); string F() { using var r = new Res(log, \"r\"); log.Add(\"body\"); return \"x\"; } F(); return string.Join(\",\", log);")] // "body,r" — disposed on the way out
    [InlineData("var log = new List<string>(); try { using var r = new Res(log, \"r\"); throw new Exception(); } catch { } return string.Join(\",\", log);")] // "r" — disposed when the body throws
    [InlineData("var log = new List<string>(); { using var a = new Res(log, \"a\"); using var b = new Res(log, \"b\"); log.Add(\"body\"); } return string.Join(\",\", log);")] // "body,b,a" — reverse order
    [InlineData("var log = new List<string>(); { using Res a = new(log, \"a\"), b = new(log, \"b\"); } return string.Join(\",\", log);")] // "b,a" — one declaration, two resources
    public void Using_DisposesAnInSourceType(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements,
            "public record struct Res(List<string> Log, string Name) : IDisposable { public void Dispose() => Log.Add(Name); }");

    /// <summary>
    /// The remaining substatement positions, for the bracing rule the writer applies
    /// (SubstatementBracingTests covers the writer's own contract, and the cases above cover
    /// foreach/while/for): a bare <c>if</c> body, a bare <c>else</c>, a <c>do</c> body, and a bare
    /// body nested inside another. Each holds one statement in C# and two after a pattern variable
    /// hoists its declaration.
    /// </summary>
    [Theory]
    [InlineData("var o = new List<string>(); string s = \"a\"; if (s != null) o.Add(s is { Length: > 0 } v ? v : \"-\"); return string.Join(\",\", o);")]                     // "a"
    [InlineData("var o = new List<string>(); string s = \"a\"; if (s == null) o.Add(\"n\"); else o.Add(s is { Length: > 0 } v ? v : \"-\"); return string.Join(\",\", o);")] // "a"
    [InlineData("var o = new List<string>(); string s = \"a\"; var i = 0; do o.Add(s is { Length: > 0 } v ? v : \"-\"); while (++i < 2); return string.Join(\",\", o);")]     // "a,a"
    [InlineData("var o = new List<string>(); string s = \"ab\"; foreach (var _ in new[] { 1 }) if (s is { Length: > 1 } v) o.Add(v); return string.Join(\",\", o);")]           // "ab"
    public void APatternVariableSurvivesEveryOtherUnbracedBody(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);

    /// <summary>
    /// OUT OF RANGE is an error in .NET and a shrug in JavaScript: `"ab".substring(9)` is "" and a
    /// missing key answers undefined, where the CLR throws. A program that stops loudly on the
    /// server kept going in the browser with an absent value spreading through it, surfacing later
    /// as a blank render rather than as the failure it was. Found by the differential generator
    /// once it learned to write a try/catch.
    /// </summary>
    [Theory]
    [InlineData("var s = \"qd\"; try { var bad = s.Substring(36); return 1; } catch { return -1; }")]          // -1
    [InlineData("var s = \"qd\"; try { var bad = s.Substring(1, 9); return 1; } catch { return -1; }")]        // -1
    [InlineData("var s = \"qd\"; try { var bad = s.Substring(-1); return 1; } catch { return -1; }")]          // -1
    [InlineData("var s = \"abcd\"; return s.Substring(1) + \"|\" + s.Substring(1, 2) + \"|\" + s.Substring(4);")] // "bcd|bc|"
    [InlineData("var m = new Dictionary<string, int> { [\"k\"] = 1 }; try { var v = m[\"nope\"]; return 1; } catch { return -1; }")] // -1
    [InlineData("var m = new Dictionary<string, int> { [\"k\"] = 1 }; return m[\"k\"];")]                     // 1
    [InlineData("var m = new Dictionary<string, int>(); m[\"new\"] = 5; return m[\"new\"];")]                 // 5 — a write CREATES
    [InlineData("var m = new Dictionary<string, int> { [\"k\"] = 1 }; m[\"k\"] += 4; return m[\"k\"];")]     // 5
    public void OutOfRangeFailsWhereDotNetFails(string statements) =>
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
}
