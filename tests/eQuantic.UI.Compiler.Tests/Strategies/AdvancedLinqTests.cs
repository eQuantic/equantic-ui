using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class AdvancedLinqTests
{
    [Fact]
    public void Aggregate_ConvertsToReduce()
    {
        var code = "list.Aggregate(0, (sum, x) => sum + x)";
        var js = ConvertExpression(code);
        // reduce((sum, x) => sum + x, 0)
        Assert.Contains("list.reduce", js);
        Assert.Contains("0", js);
    }

    [Fact]
    public void ToDictionary_ConvertsToFromEntries()
    {
        var code = "list.ToDictionary(k => k.Id, v => v.Name)";
        var js = ConvertExpression(code);
        Assert.StartsWith("Object.fromEntries(list.map", js);
    }
    
    [Fact]
    public void GroupBy_ConvertsToReduce()
    {
        var code = "list.GroupBy(x => x.Type)";
        var js = ConvertExpression(code);
        Assert.Contains(".reduce((groups, item) =>", js);
        // Each group is the items array with a `key` property (IGrouping usable as a sequence).
        Assert.Contains("g.key = key", js);
        // Lambda parameter name is preserved, but properties used in it are camelCased
        Assert.Matches(@"(x\.type|x\.Type)", js);
    }
    
    /// <summary>Zip stops with the SHORTER sequence. It used to map over the receiver, which walks
    /// the longer one and hands the selector undefined for the missing partner — a silent NaN for
    /// numbers — so the shape this asserts changed with the behaviour.</summary>
    [Fact]
    public void Zip_StopsWithTheShorterSequence()
    {
        var js = ConvertExpression("list.Zip(other, (a, b) => a + b)");
        Assert.Contains("$eq.zip(list, other,", js);
        Assert.DoesNotContain("[i]", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
