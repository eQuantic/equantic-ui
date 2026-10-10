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

    /// <summary>With no model to say what the values are, they go through the runtime, which reads any
    /// sequence: <c>join</c> is an array's alone, and a set or a linked list threw (#429).</summary>
    [Theory]
    [InlineData("String.Join(\",\", list)", "$eq.text.join(',', list)")]
    [InlineData("String.Join(\",\", a, b)", "$eq.text.join(',', [a, b])")]
    [InlineData("String.Join(\",\", \"a\")", "$eq.text.join(',', ['a'])")]
    public void String_Join_WithNoModel_GoesThroughTheRuntime(string code, string expected)
    {
        Assert.Equal(expected, ConvertExpression(code));
    }
    
    [Fact]
    public void String_Format_ConvertsToRuntimeHelper()
    {
        // string.Format routes to the $eq runtime helper, which substitutes {i}/{i:spec}
        // (the latter via the same formatter the interpolation path uses) and unescapes {{/}}.
        var code = "String.Format(\"Hello {0}\", name)";
        var js = ConvertExpression(code);
        Assert.Equal("$eq.text.stringFormat('Hello {0}', name)", js);
    }

    [Fact]
    public void String_Format_NoArgs_PassesTemplateOnly()
    {
        var code = "String.Format(\"literal\")";
        var js = ConvertExpression(code);
        Assert.Equal("$eq.text.stringFormat('literal')", js);
    }

    // Task Tests
    [Fact]
    public void Task_Delay_ConvertsToSetTimeout()
    {
        var code = "Task.Delay(100)";
        var js = ConvertExpression(code);
        Assert.Contains("new Promise(($resolve) => setTimeout($resolve, 100))", js);
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
    public void Int_Parse_IsTheRuntimesReader()
    {
        var code = "int.Parse(\"123\")";
        var js = ConvertExpression(code);
        Assert.Equal("$eq.num.intParse('123', 'int')", js);
    }
    
    [Fact]
    public void Double_Parse_IsTheRuntimesReader()
    {
        var code = "double.Parse(\"12.3\")";
        var js = ConvertExpression(code);
        Assert.Equal("$eq.num.realParse('12.3', 'double')", js);
    }
    
    [Fact]
    public void Int_TryParse_LeavesZeroWhenItFails()
    {
        var code = "int.TryParse(s, out var x)";
        var js = ConvertExpression(code);
        Assert.Equal("((x = $eq.num.intTryParse(s, 'int')) !== undefined || ((x = 0), false))", js);
    }
    
    [Fact]
    public void Int_TryParse_ExistingVar_LeavesZeroWhenItFails()
    {
        var code = "int.TryParse(s, out x)";
        var js = ConvertExpression(code);
        Assert.Equal("((x = $eq.num.intTryParse(s, 'int')) !== undefined || ((x = 0), false))", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
