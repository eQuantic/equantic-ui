using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class FinalLinqTests : StrategyTestBase
{
    [Fact]
    public void ElementAt_ConvertsToIndexer()
    {
        var code = "list.ElementAt(5)";
        var js = Convert(code);
        Assert.Contains("[5]", js);
    }

    [Fact]
    public void SequenceEqual_ConvertsToJSONStringify()
    {
        var code = "list.SequenceEqual(other)";
        var js = Convert(code);
        Assert.Contains("JSON.stringify(list)", js);
        Assert.Contains("JSON.stringify(other)", js);
    }

    /// <summary>DefaultIfEmpty is a ternary over its source, run when its query is walked (#685).</summary>
    [Fact]
    public void DefaultIfEmpty_DefersATernary()
    {
        var code = "list.DefaultIfEmpty(0)";
        var js = Convert(code);
        Assert.Contains("$eq.query.defer(($src) => ($src.length > 0 ? $src : [$1]), $0)", js);
        Assert.Contains("(list, 0)", js);
    }
}
