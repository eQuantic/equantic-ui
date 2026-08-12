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
}
