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

    /// <summary>
    /// A second <c>OrderBy</c> REPLACES the primary key; it does not refine the one under it. The
    /// earlier ordering survives only as the tiebreak a stable sort gives it, so
    /// <c>OrderBy(a).OrderBy(b)</c> and <c>OrderBy(a).ThenBy(b)</c> are different orders whenever
    /// b has ties that a would break. Both were emitted as one comparator with a as the primary,
    /// which is what ThenBy means — found by the differential generator, which reached the shape
    /// three times in two thousand programs before anyone wrote it down.
    /// </summary>
    [SkippableTheory]
    [InlineData("return string.Join(\"-\", new[]{4,15,3,15}.OrderBy(x => x % 2).OrderBy(x => x % 6));")]
    [InlineData("return string.Join(\"-\", new[]{4,15,3,15}.OrderBy(x => x % 2).ThenBy(x => x % 6));")]
    [InlineData("return string.Join(\"-\", new[]{5,3,8,1,9}.OrderBy(x => x % 3).OrderBy(x => x % 2));")]
    [InlineData("return string.Join(\"-\", new[]{5,3,8,1,9}.OrderByDescending(x => x % 3).OrderBy(x => x % 2));")]
    [InlineData("return string.Join(\"-\", new[]{5,3,8,1,9}.OrderBy(x => x % 3).ThenBy(x => x).OrderBy(x => x % 2));")]
    [InlineData("return string.Join(\"-\", new[]{7,2,9,4}.OrderBy(x => x % 2).OrderBy(x => x % 3).ThenBy(x => x));")]
    [InlineData("return new[]{4,15,3,15}.OrderBy(x => x % 2).OrderBy(x => x % 6).ElementAtOrDefault(3);")]
    public void ASecondOrderByRestartsTheOrdering(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
