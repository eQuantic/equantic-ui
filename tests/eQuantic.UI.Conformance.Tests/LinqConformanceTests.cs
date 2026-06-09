using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>Conformance: LINQ operators over in-memory sequences behave identically in emitted JS.</summary>
public class LinqConformanceTests
{
    [SkippableTheory]
    [InlineData("new[]{1,2,3}.Where(x => x > 1).ToList()")]
    [InlineData("new[]{1,2,3}.Select(x => x * 2).ToList()")]
    [InlineData("new[]{1,2,3}.Select(x => x * x).Sum()")]
    [InlineData("new[]{1,2,3}.First()")]
    [InlineData("new[]{1,2,3}.First(x => x > 1)")]
    [InlineData("new[]{1,2,3}.Last()")]
    [InlineData("new[]{1,2,3}.Count()")]
    [InlineData("new[]{1,2,3}.Count(x => x > 1)")]
    [InlineData("new[]{1,2,3}.Any()")]
    [InlineData("new[]{1,2,3}.Any(x => x > 2)")]
    [InlineData("new[]{1,2,3}.All(x => x > 0)")]
    [InlineData("new[]{1,2,3}.Sum()")]
    [InlineData("new[]{1,2,3}.Max()")]
    [InlineData("new[]{1,2,3}.Min()")]
    [InlineData("new[]{1,2,3,4}.Skip(2).ToList()")]
    [InlineData("new[]{1,2,3,4}.Take(2).ToList()")]
    [InlineData("new[]{1,2,3}.Contains(2)")]
    [InlineData("new[]{1,2,3}.ElementAt(1)")]
    [InlineData("new[]{3,1,2}.OrderBy(x => x).ToList()")]
    [InlineData("new[]{3,1,2}.OrderByDescending(x => x).ToList()")]
    [InlineData("new[]{1,2,2,3}.Distinct().ToList()")]
    [InlineData("new[]{1,2,3}.Reverse().ToList()")]
    public void Linq_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
