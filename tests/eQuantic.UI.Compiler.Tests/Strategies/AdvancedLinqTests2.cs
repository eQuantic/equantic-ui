using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class AdvancedLinqTests2 : StrategyTestBase
{
    [Fact]
    public void TakeWhile_ConvertsToLoop()
    {
        var code = "list.TakeWhile(x => x < 5)";
        var js = Convert(code);
        
        Assert.Contains("function(arr)", js);
        Assert.Contains("return res", js);
        // We verify logic structure roughly
        Assert.Contains("else break", js);
        Assert.Contains("x < 5", js);
    }

    [Fact]
    public void SkipWhile_ConvertsToLoop()
    {
        var code = "list.SkipWhile(x => x < 5)";
        var js = Convert(code);
        
        Assert.Contains("let skipping = true", js);
        Assert.Contains("continue", js);
        Assert.Contains("x < 5", js);
    }

    [Fact]
    public void DistinctBy_ConvertsToFilterWithSet()
    {
        var code = "list.DistinctBy(x => x.Id)";
        var js = Convert(code);
        
        Assert.Contains("const seen = new Set()", js);
        Assert.Contains("seen.add(k)", js);
        Assert.Contains("seen.has(k)", js);
        Assert.Contains("x.id", js.ToLower()); // Property access case normalization
    }
}
