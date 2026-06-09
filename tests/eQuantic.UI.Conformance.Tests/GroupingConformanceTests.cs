using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>IGrouping</c> usage from <c>GroupBy</c>/<c>ToLookup</c>: each group is usable as a
/// sequence (iterate, <c>g.Select</c>, <c>g.Sum</c>, <c>g.Count()</c>) and exposes <c>g.Key</c>, with
/// groups in first-occurrence key order. (The <c>ILookup[key]</c> indexer is out of scope.)
/// </summary>
public class GroupingConformanceTests
{
    [SkippableTheory]
    // g.Key in first-occurrence order
    [InlineData("string.Join(\",\", new[] { 10, 20, 11, 21 }.GroupBy(x => x / 10).Select(g => g.Key))")]      // "1,2"
    // g.Count() per group
    [InlineData("string.Join(\",\", new[] { 1, 2, 3, 4, 5, 6 }.GroupBy(x => x % 3).Select(g => g.Count()))")] // "2,2,2"
    // g.Sum() per group (odd first, then even — first-occurrence)
    [InlineData("string.Join(\",\", new[] { 1, 2, 3, 4, 5, 6 }.GroupBy(x => x % 2).Select(g => g.Sum()))")]   // "9,12"
    // iterate the group directly as a sequence
    [InlineData("string.Join(\";\", new[] { 1, 2, 3, 4 }.GroupBy(x => x % 2).Select(g => string.Join(\"-\", g)))")] // "1-3;2-4"
    // key + count combined
    [InlineData("string.Join(\",\", new[] { 1, 2, 3, 4 }.GroupBy(x => x % 2).Select(g => g.Key + \":\" + g.Count()))")] // "1:2,0:2"
    // g.First() / g.Max() within a group
    [InlineData("new[] { 5, 1, 5, 9, 1 }.GroupBy(x => x).Select(g => g.Count()).Max()")]                      // 2
    // ToLookup groups iterate the same way
    [InlineData("string.Join(\",\", new[] { 1, 2, 3, 4 }.ToLookup(x => x % 2).Select(g => g.Key + \":\" + g.Count()))")] // "1:2,0:2"
    // ordering inside a group then joining
    [InlineData("string.Join(\";\", new[] { 3, 1, 4, 2 }.GroupBy(x => x % 2).Select(g => string.Join(\",\", g.OrderBy(v => v))))")] // "1,3;2,4"
    public void Grouping_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
