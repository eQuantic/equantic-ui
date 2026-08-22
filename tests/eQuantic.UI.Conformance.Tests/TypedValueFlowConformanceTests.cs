using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Fase 5, slice 7 — the shapes the DEFENSIVE wraps used to carry. With the boundary typed
/// (hydration by spec), a decimal is a Decimal and a long a BigInt everywhere in the program, so
/// the per-use <c>$eq.num.dec/long</c> coercions can go — PROVIDED every seam that used to lean on
/// them is a typed conversion of its own: the value of a compound assignment, a negated literal,
/// an increment, an operand of Math.Round/Sum/Average, a dictionary key read back in a foreach.
/// These cases pass before and after the wraps go; a red here is a seam the removal missed.
/// </summary>
public class TypedValueFlowConformanceTests
{
    [SkippableTheory]
    // ---- compound assignments: the VALUE converts to the target's type ----
    [InlineData("decimal m = 1.5m; m += 1; return m.ToString();")]                       // "2.5" — int value into decimal
    [InlineData("decimal m = 10m; m /= 4; return m.ToString();")]                        // "2.5"
    [InlineData("decimal m = 1m; m -= 0.25m; return m.ToString();")]                     // "0.75"
    [InlineData("decimal m = 1.5m; m *= 2; return m.ToString();")]                       // "3.0"
    [InlineData("long l = 5; l += 2; return (l * 3000000000L).ToString();")]             // "21000000000" — stays exact
    [InlineData("long l = 10; l /= 3; return l.ToString();")]                            // "3"
    [InlineData("long l = 9007199254740992L; l += 1; return l.ToString();")]             // "9007199254740993" — past 2^53
    // ---- unary minus and plus keep the representation ----
    [InlineData("decimal m = -3.99m; return m.ToString();")]                             // "-3.99"
    [InlineData("decimal m = -3.99m; int i = (int)m; return i;")]                        // -3 — a negated literal is still a Decimal
    [InlineData("decimal m = 2.5m; decimal n = -m; return (n + 0.5m).ToString();")]      // "-2.0"
    [InlineData("decimal m = -0.1m; return (m + 0.2m == 0.1m);")]                        // true — exact base-10
    [InlineData("long l = -9007199254740993L; return (-l).ToString();")]                 // "9007199254740993"
    // ---- increments step by the type's one ----
    [InlineData("decimal m = 1.5m; m++; return m.ToString();")]                          // "2.5"
    [InlineData("decimal m = 1.5m; m--; return m.ToString();")]                          // "0.5"
    [InlineData("decimal m = 1m; var before = m++; return (before + m).ToString();")]    // "3" — postfix yields the old value
    [InlineData("long l = 9007199254740992L; l++; return l.ToString();")]                // "9007199254740993"
    [InlineData("long l = 5; var b = l++; return (b + l).ToString();")]                  // "11"
    // ---- the BCL surface that computes on decimals ----
    [InlineData("decimal m = 2.5m; return Math.Round(m).ToString();")]                   // "2" — banker's
    [InlineData("decimal m = 2.5m; return Math.Round(-m).ToString();")]                  // "-2" — on a computed value
    [InlineData("var xs = new List<decimal> { 0.1m, 0.2m }; return xs.Sum().ToString();")]        // "0.3" — exact
    [InlineData("var xs = new List<decimal> { 1m, 2m }; return xs.Average().ToString();")]        // "1.5"
    [InlineData("var xs = new List<long> { 3000000000L, 4000000000L }; return xs.Sum().ToString();")] // "7000000000"
    // ---- values that crossed a container keep their type ----
    [InlineData("var xs = new List<decimal> { 1.5m }; decimal m = xs[0]; return (m + 1m).ToString();")]   // "2.5"
    [InlineData("var d = new Dictionary<long, string> { [5L] = \"a\" }; long s = 0; foreach (var (k, v) in d) s += k; return (s * 2).ToString();")] // "10" — the key reads back as a long
    [InlineData("var d = new Dictionary<string, decimal> { [\"a\"] = 0.1m }; return (d[\"a\"] + 0.2m).ToString();")] // "0.3"
    // ---- long ↔ decimal cross the seam through a conversion, not a coercion ----
    [InlineData("long l = 5; decimal m = l; return (m + 0.5m).ToString();")]             // "5.5" — implicit long → decimal
    [InlineData("int i = 3; decimal m = i; return (m / 2).ToString();")]                 // "1.5" — implicit int → decimal
    [InlineData("decimal m = 7m; return (m + 1).ToString();")]                           // "8" — int literal into decimal arithmetic
    [InlineData("decimal m = 1m; return (2 * m).ToString();")]                           // "2" — int on the LEFT
    [InlineData("long l = 7; return (l > 5) && (l + 1 == 8L);")]                         // true
    public void TypedFlow_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
