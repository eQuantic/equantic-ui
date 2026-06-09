using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for the join/grouping LINQ operators: <c>Join</c> (inner, order-preserving hash join),
/// <c>GroupJoin</c> (left/group join — every outer once, with its matching group) and <c>ToLookup</c>
/// (grouping by key). Joins use primitive keys (the common case).
/// </summary>
public class LinqJoinConformanceTests
{
    [SkippableTheory]
    // Join — inner join, order preserved; result string list serialized.
    [InlineData("new[] { 1, 2, 3 }.Join(new[] { 2, 3, 4 }, o => o, i => i, (o, i) => o + \"-\" + i)")]            // ["2-2","3-3"]
    [InlineData("new[] { 1, 2, 2 }.Join(new[] { 2 }, o => o, i => i, (o, i) => o * 10 + i).Sum()")]                // 44
    [InlineData("new[] { 1, 2, 3 }.Join(new[] { 5, 6 }, o => o, i => i, (o, i) => o).Count()")]                    // 0 (no matches)
    // Many-to-many: outer key 1 matches two inners.
    [InlineData("new[] { 1 }.Join(new[] { 1, 1 }, o => o, i => i, (o, i) => o + i).Count()")]                      // 2
    // GroupJoin — every outer once, with its group (possibly empty).
    [InlineData("new[] { 1, 2, 3 }.GroupJoin(new[] { 2, 2, 3 }, o => o, i => i, (o, g) => o + \":\" + g.Count())")] // ["1:0","2:2","3:1"]
    [InlineData("new[] { 1, 2 }.GroupJoin(new[] { 2, 2 }, o => o, i => i, (o, g) => g.Sum()).Sum()")]               // 4
    [InlineData("new[] { 1, 2, 3 }.GroupJoin(new[] { 2, 3 }, o => o, i => i, (o, g) => g.Any()).Count(x => x)")]    // 2
    // ToLookup — number of distinct key groups.
    [InlineData("new[] { 1, 2, 3, 4, 5 }.ToLookup(x => x % 2).Count")]                                              // 2
    [InlineData("new[] { \"apple\", \"avocado\", \"banana\" }.ToLookup(s => s[0]).Count")]                          // 2
    public void LinqJoin_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
