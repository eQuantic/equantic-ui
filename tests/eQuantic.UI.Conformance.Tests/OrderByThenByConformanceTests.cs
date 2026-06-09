using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for ordering: <c>OrderBy</c>/<c>OrderByDescending</c> and their <c>ThenBy</c>/
/// <c>ThenByDescending</c> continuations — a single stable composite sort that never mutates the
/// source. Numeric keys keep ordering unambiguous (independent of culture/code-unit string order).
/// </summary>
public class OrderByThenByConformanceTests
{
    [SkippableTheory]
    // OrderBy / OrderByDescending
    [InlineData("string.Join(\",\", new[] { 3, 1, 2 }.OrderBy(x => x))")]                       // "1,2,3"
    [InlineData("string.Join(\",\", new[] { 3, 1, 2 }.OrderByDescending(x => x))")]             // "3,2,1"
    // ThenBy: primary by x/10, secondary by x%10 ascending.
    [InlineData("string.Join(\",\", new[] { 12, 11, 22, 21 }.OrderBy(x => x / 10).ThenBy(x => x % 10))")]            // "11,12,21,22"
    // ThenByDescending: secondary descending within equal primary.
    [InlineData("string.Join(\",\", new[] { 12, 11, 22, 21 }.OrderBy(x => x / 10).ThenByDescending(x => x % 10))")]  // "12,11,22,21"
    // Three levels.
    [InlineData("string.Join(\",\", new[] { 121, 112, 111, 122 }.OrderBy(x => x / 100).ThenBy(x => (x / 10) % 10).ThenByDescending(x => x % 10))")] // "112,111,122,121"
    // Stability: equal keys keep input order (sort by parity keeps within-group order).
    [InlineData("string.Join(\",\", new[] { 4, 1, 6, 3, 2 }.OrderBy(x => x % 2))")]             // "4,6,2,1,3"
    // OrderBy does not mutate the source.
    [InlineData("var src = new[] { 3, 1, 2 }; var _ = src.OrderBy(x => x).ToList(); return string.Join(\",\", src);")] // "3,1,2"
    public void OrderByThenBy_MatchesDotNet(string code)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        if (code.TrimStart().StartsWith("var ") || code.Contains("return "))
            ConformanceRunner.AssertStatementsSameAsDotNet(code);
        else
            ConformanceRunner.AssertSameAsDotNet(code);
    }
}
