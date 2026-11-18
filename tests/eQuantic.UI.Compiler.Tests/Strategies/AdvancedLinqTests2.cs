using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class AdvancedLinqTests2 : StrategyTestBase
{
    /// <summary>TakeWhile is the runtime's lazy query, whose walk stops at the first element that fails (#685).</summary>
    [Fact]
    public void TakeWhile_IsALazyQuery()
    {
        var code = "list.TakeWhile(x => x < 5)";
        var js = Convert(code);

        Assert.Contains("$eq.query.takeWhile(list, (x) => x < 5)", js);
    }

    /// <summary>SkipWhile is the runtime's lazy query, which skips while the predicate holds when walked (#685).</summary>
    [Fact]
    public void SkipWhile_IsALazyQuery()
    {
        var code = "list.SkipWhile(x => x < 5)";
        var js = Convert(code);

        Assert.Contains("$eq.query.skipWhile(list, (x) => x < 5)", js);
    }

    [Fact]
    public void DistinctBy_ConvertsToFilterWithSet()
    {
        var code = "list.DistinctBy(x => x.Id)";
        var js = Convert(code);
        
        Assert.Contains("const $seen = new Set()", js);
        Assert.Contains("$seen.add($k)", js);
        Assert.Contains("$seen.has($k)", js);
        Assert.Contains("x.id", js.ToLower()); // Property access case normalization
    }
}
