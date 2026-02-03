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

    [Fact]
    public void DefaultIfEmpty_ConvertsToTernary()
    {
        var code = "list.DefaultIfEmpty(0)";
        var js = Convert(code);
        Assert.Contains("list.length > 0", js);
        Assert.Contains(": [0]", js);
    }
}
