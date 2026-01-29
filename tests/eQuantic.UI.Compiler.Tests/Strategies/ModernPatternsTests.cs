using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class ModernPatternsTests
{
    [Fact]
    public void CollectionExpression_Simple_ConvertsToArray()
    {
        var code = "[1, 2, 3]";
        var js = ConvertExpression(code);
        Assert.Equal("[1, 2, 3]", js);
    }

    [Fact]
    public void CollectionExpression_WithSpread_ConvertsToSpread()
    {
        var code = "[..items, 4]";
        var js = ConvertExpression(code);
        Assert.Equal("[...items, 4]", js);
    }
    
    [Fact]
    public void ListPattern_ExactMatch_ConvertsToLengthAndElementChecks()
    {
        var code = "x is [1, 2]";
        var js = ConvertExpression(code);
        Assert.Equal("Array.isArray(x) && x.length === 2 && x[0] === 1 && x[1] === 2", js);
    }

    [Fact]
    public void ListPattern_WithSlice_ConvertsToMinLengthAndSliceChecks()
    {
        var code = "x is [1, ..]";
        var js = ConvertExpression(code);
        Assert.Equal("Array.isArray(x) && x.length >= 1 && x[0] === 1", js);
    }
    
    [Fact]
    public void ListPattern_WithSliceAndMiddle_ConvertsToChecks()
    {
        var code = "x is [1, .., 9]";
        var js = ConvertExpression(code);
        Assert.Equal("Array.isArray(x) && x.length >= 2 && x[0] === 1 && x[x.length - 1] === 9", js);
    }

    [Fact]
    public void ListPattern_WithVariableCapture_GeneratesIIFE()
    {
        var code = "x is [.. var rest]";
        var js = ConvertExpression(code);
        // Expecting IIFE due to assignment
        Assert.Equal("Array.isArray(x) && x.length >= 0 && ((() => { rest = x.slice(0); return true; })())", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
