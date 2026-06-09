using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>Conformance: dictionaries, sets and aggregations behave identically in emitted JS.</summary>
public class CollectionConformanceTests
{
    [SkippableTheory]
    // Dictionary
    [InlineData("new Dictionary<string,int>{ {\"a\",1}, {\"b\",2} }[\"a\"]")]
    [InlineData("new Dictionary<string,int>{ {\"a\",1}, {\"b\",2} }.Count")]
    [InlineData("new Dictionary<string,int>{ {\"a\",1}, {\"b\",2} }.ContainsKey(\"a\")")]
    [InlineData("new Dictionary<string,int>{ {\"a\",1}, {\"b\",2} }.ContainsKey(\"z\")")]
    [InlineData("new Dictionary<string,int>{ {\"a\",1}, {\"b\",2} }")]
    // HashSet
    [InlineData("new HashSet<int>{1,2,3}.Count")]
    [InlineData("new HashSet<int>{1,1,2}.Count")]
    [InlineData("new HashSet<int>{1,2,3}.Contains(2)")]
    [InlineData("new HashSet<int>{1,2,3}.Contains(9)")]
    // Aggregations
    [InlineData("new[]{1,2,3,4}.Aggregate((a, b) => a + b)")]
    [InlineData("new[]{1,2,3}.Aggregate(100, (a, b) => a + b)")]
    [InlineData("new[]{1,2,3}.Average()")]
    [InlineData("new[]{1,2}.Average()")]
    public void Collections_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
