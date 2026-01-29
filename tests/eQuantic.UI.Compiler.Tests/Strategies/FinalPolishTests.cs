using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class FinalPolishTests
{
    // String Static Tests
    [Fact]
    public void String_IsNullOrEmpty_ConvertsToNot()
    {
        var code = "String.IsNullOrEmpty(s)";
        var js = ConvertExpression(code);
        Assert.Equal("!s", js);
    }

    [Fact]
    public void String_Join_ConvertsToJoin()
    {
        var code = "String.Join(\",\", list)";
        var js = ConvertExpression(code);
        Assert.Equal("list.join(',')", js);
    }
    
    [Fact]
    public void String_Format_ConvertsToReplace()
    {
        // Simple test for basic transformation
        var code = "String.Format(\"Hello {0}\", name)";
        var js = ConvertExpression(code);
        Assert.Contains("replace", js);
        Assert.Contains("Hello {0}", js);
        Assert.Contains("name", js);
    }

    // Task Tests
    [Fact]
    public void Task_Delay_ConvertsToSetTimeout()
    {
        var code = "Task.Delay(100)";
        var js = ConvertExpression(code);
        Assert.Contains("new Promise(resolve => setTimeout(resolve, 100))", js);
    }
    
    [Fact]
    public void Task_WhenAll_ConvertsToPromiseAll()
    {
        var code = "Task.WhenAll(t1, t2)";
        var js = ConvertExpression(code);
        Assert.Equal("Promise.all([t1, t2])", js);
    }

    // Number Tests
    [Fact]
    public void Int_Parse_ConvertsToParseInt()
    {
        var code = "int.Parse(\"123\")";
        var js = ConvertExpression(code);
        Assert.Equal("parseInt('123')", js);
    }
    
    [Fact]
    public void Double_Parse_ConvertsToParseFloat()
    {
        var code = "double.Parse(\"12.3\")";
        var js = ConvertExpression(code);
        Assert.Equal("parseFloat('12.3')", js);
    }
    
    [Fact]
    public void Int_TryParse_ConvertsToSafeCheck()
    {
        var code = "int.TryParse(s, out var x)";
        var js = ConvertExpression(code);
        Assert.Contains("x = parseInt(s)", js);
        Assert.Contains("!isNaN(x)", js);
    }
    
    [Fact]
    public void Int_TryParse_ExistingVar_ConvertsToSafeCheck()
    {
        var code = "int.TryParse(s, out x)";
        var js = ConvertExpression(code);
        Assert.Contains("x = parseInt(s)", js);
        Assert.Contains("!isNaN(x)", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
