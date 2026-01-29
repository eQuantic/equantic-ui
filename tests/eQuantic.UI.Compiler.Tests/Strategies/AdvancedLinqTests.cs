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
        Assert.Contains(".reduce((map, item) =>", js);
        // Lambda parameter name is preserved, but properties used in it are camelCased
        Assert.Matches(@"(x\.type|x\.Type)", js);
    }
    
    [Fact]
    public void Zip_ConvertsToMap()
    {
        var code = "list.Zip(other, (a, b) => a + b)";
        var js = ConvertExpression(code);
        Assert.Contains("list.map((e, i) =>", js);
        Assert.Contains("other[i]", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
