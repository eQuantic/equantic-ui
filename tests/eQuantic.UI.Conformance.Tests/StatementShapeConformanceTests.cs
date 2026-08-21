using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The statement shapes that moved to the IR, executed on both sides — and the arrow whose
/// object-literal body used to come back as undefined.
/// </summary>
public class StatementShapeConformanceTests
{
    [SkippableTheory]
    // the arrow body that was a block with a label in it
    [InlineData("var xs = new List<string> { \"a\", \"bb\" }; return xs.Select(s => new { L = s.Length }).Select(o => o.L).Sum();")] // 3
    [InlineData("var xs = new List<int> { 1, 2 }; return xs.Select(n => new { Twice = n * 2, n }).Sum(o => o.Twice + o.n);")]    // 9
    // local functions, both body forms
    [InlineData("int Twice(int x) => x * 2; int Thrice(int x) { return x * 3; } return Twice(4) + Thrice(1);")]                  // 11
    // loops and switches
    [InlineData("var s = 0; for (int i = 0; i < 4; i++) s += i; return s;")]                                                        // 6
    [InlineData("var s = 0; foreach (var n in new[] { 1, 2, 3 }) { s += n; } return s;")]                                             // 6
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; var s = 0; foreach (var (k, v) in d) s += v; return s;")] // 3
    [InlineData("int k = 2; int r = 0; switch (k) { case 1: r = 10; break; case 2: r = 20; break; default: r = 0; break; } return r;")]    // 20
    [InlineData("object o = 5; switch (o) { case int n when n > 3: return n * 10; case int n: return n; default: return -1; }")]          // 50
    [InlineData("int k = 1; var r = 0; switch (k) { case 1: case 2: r = 7; break; } return r;")]                                           // 7
    // try/finally
    [InlineData("var r = 0; try { r = 1; } finally { r += 1; } return r;")]                                                            // 2
    [InlineData("var r = 0; try { throw new Exception(\"x\"); } catch (Exception e) { r = e.Message.Length; } return r;")]               // 1
    // assignment as a node: chains, compound forms, and an assignment used as an operand
    [InlineData("int a, b; a = b = 3; return a + b;")]                                                                  // 6
    [InlineData("int x = 1; var r = (x = 5) * 2; return r + x;")]                                                       // 15
    [InlineData("int t = 1; t += 2; t *= 3; t -= 1; return t;")]                                                         // 8
    [InlineData("var xs = new List<int> { 1, 2, 3 }; int last; int i = 0; while ((last = xs[i]) < 3) i++; return last + i;")] // 5
    public void StatementShapes_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
