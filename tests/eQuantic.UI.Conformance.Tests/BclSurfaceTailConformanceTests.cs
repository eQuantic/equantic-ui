using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The audited tail: primitive <c>Equals</c>, the modern Enumerable shapes, string leftovers and
/// Dictionary/List quality-of-life members — every translation the two tail strategies admit,
/// executed on both sides. The edge cases the tables were written around are all here:
/// <c>SkipLast(0)</c>/<c>TakeLast(0)</c> (where a naive slice(−0) flips meaning), both TryAdd
/// branches, the *By operators' distinct-by-key semantics.
/// </summary>
public class BclSurfaceTailConformanceTests
{
    [SkippableTheory]
    // Equals on exactly-comparable primitives
    [InlineData("(3).Equals(3)")]                                            // true
    // (3).Equals((object)3.0): C# false, erased JS true — the systemic numeric-erasure caveat, deliberately untested.
    [InlineData("'a'.Equals('a')")]                                          // true
    [InlineData("true.Equals((object)1)")]                                   // false
    [InlineData("3L.Equals(3L)")]                                            // true
    // string tail
    [InlineData("\"a\\nb\".ReplaceLineEndings(\"; \")")]
    [InlineData("\"banana\".IndexOfAny(new[] { 'n', 'x' })")]                // 2
    [InlineData("\"banana\".IndexOfAny(new[] { 'n' }, 3)")]                  // 4
    [InlineData("\"banana\".LastIndexOfAny(new[] { 'n' })")]                 // 4
    [InlineData("\"banana\".IndexOfAny(new[] { 'z' })")]                     // -1
    [InlineData("string.CompareOrdinal(\"a\", \"b\")")]                      // -1
    [InlineData("string.CompareOrdinal(\"b\", \"a\")")]                      // 1
    [InlineData("string.CompareOrdinal(\"a\", \"a\")")]                      // 0
    [InlineData("string.Intern(\"xy\")")]                                    // "xy"
    [InlineData("\"abc\".Clone()")]                                          // "abc"
    // char statics
    [InlineData("char.IsBetween('m', 'a', 'z')")]                            // true
    [InlineData("char.IsBetween('M', 'a', 'z')")]                            // false
    [InlineData("char.Parse(\"k\")")]                                        // 'k'
    [InlineData("char.ConvertFromUtf32(0x41)")]                              // "A"
    [InlineData("char.ConvertFromUtf32(0x1F600).Length")]                    // 2 — the surrogate pair, counted
    public void SurfaceTailExpressions_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
    /// <summary>
    /// The expressions whose .NET answer is the HOST's newline.
    ///
    /// <para>
    /// `Environment.NewLine` is `\r\n` on Windows and `\n` elsewhere, and a browser has no
    /// environment — so the SDK decided its newline is `\n`, and said so in both places that
    /// implement it. The decision was written down twice and held until the suite ran on a Windows
    /// runner for the first time.
    /// </para>
    ///
    /// <para>
    /// Folded on both sides, so a translation defect still fails. What this does NOT do is make the
    /// divergence go away. WHERE it shows was measured, not assumed — see
    /// <see cref="ConformanceRunner.AssertSameAsDotNetExceptTheHostsNewline"/>: a browser folds CR LF in
    /// markup before the DOM exists, so a Windows-hosted server's SSR hydrates clean, and what differs
    /// is DATA carried to the client in a payload. The product answer under discussion is the SDK
    /// normalising its own strings to `\n`; it is open.
    /// </para>
    /// </summary>
    [SkippableTheory]
    [InlineData("\"ab\\r\\ncd\\ref\".ReplaceLineEndings()",
        "ReplaceLineEndings() with no argument replaces with Environment.NewLine")]
    public void SurfaceTail_WhereDotNetAnswersTheHostsNewline(string expression, string why)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNetExceptTheHostsNewline(expression, why);
    }

    /// <summary>
    /// The no-argument <c>ReplaceLineEndings()</c> replaces with <c>Environment.NewLine</c>, which
    /// is the HOST's — so .NET answers "\r\n" on Windows while the twin answers "\n" everywhere.
    /// The twin is the one that must not vary: it runs in a browser, where there is no host
    /// newline to read. The one-argument overload takes its replacement from the caller and stays
    /// in the table above, because nothing about it depends on the machine.
    /// </summary>
    [SkippableFact]
    public void ReplaceLineEndings_NormalizesToTheTwinsNewline()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNetIgnoringHostNewline(
            "\"ab\\r\\ncd\\ref\".ReplaceLineEndings()");
    }

    [SkippableTheory]
    // Enumerable tail — the zero-count traps first.
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.SkipLast(0).Sum();")]        // 6
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.SkipLast(2).Sum();")]        // 1
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.SkipLast(9).Count();")]      // 0
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.TakeLast(0).Count();")]      // 0
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.TakeLast(2).Sum();")]        // 5
    [InlineData("var l = new List<int> { 1, 2 }; return l.Append(9).Sum();")]             // 12
    [InlineData("var l = new List<int> { 1, 2 }; return l.Prepend(9).First();")]          // 9
    [InlineData("var l = new List<int> { 3, 1, 2 }; return string.Join(\",\", l.Order());")]            // 1,2,3
    [InlineData("var l = new List<int> { 3, 1, 2 }; return string.Join(\",\", l.OrderDescending());")]  // 3,2,1
    [InlineData("var l = new List<int> { 1, 2, 2, 3 }; return l.ToHashSet().Count;")]     // 3
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.AsEnumerable().Sum();")]     // 6
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.LongCount().ToString();")]              // 3L
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.LongCount(x => x > 1).ToString();")]    // 2L
    // *By operators: distinct-by-key, second operand of Except/Intersect is the KEY sequence.
    [InlineData("var l = new List<int> { 1, 2, 3, 4, 14 }; return string.Join(\",\", l.ExceptBy(new List<int> { 2 }, x => x % 10));")]     // 1,3,4
    [InlineData("var l = new List<int> { 1, 2, 3, 12 }; return string.Join(\",\", l.IntersectBy(new List<int> { 2, 3 }, x => x % 10));")]  // 2,3
    [InlineData("var a = new List<int> { 1, 2 }; return string.Join(\",\", a.UnionBy(new List<int> { 12, 3 }, x => x % 10));")]            // 1,2,3
    // Dictionary / List QoL
    // (added ? 1 : 0) rather than interpolating the bool: C# writes "True", JS "true" — the
    // format helper owns that divergence, not TryAdd.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var added = d.TryAdd(\"a\", 9); return (added ? 1 : 0) * 100 + d[\"a\"];")]   // 1
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var added = d.TryAdd(\"b\", 9); return (added ? 1 : 0) * 100 + d[\"b\"];")]   // 109
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; return d.ContainsValue(2);")]                            // true
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.ContainsValue(9);")]                                         // false
    [InlineData("var l = new List<int> { 10, 20, 30, 40 }; return l.Slice(1, 2).Sum();")]  // 50
    [InlineData("var l = new List<int> { 1, 2, 3 }; return l.AsReadOnly().Count;")]        // 3
    public void SurfaceTailStatements_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
