using FluentAssertions;
using Xunit;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class NewExpressionStrategyTests
{
    private string Convert(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }

    [Fact]
    public void Lambda_Simple_ConvertsToArrowFunction()
    {
        var js = Convert("x => x + 1");
        Assert.Equal("(x) => x + 1", js);
    }

    [Fact]
    public void Lambda_Parenthesized_ConvertsToArrowFunction()
    {
        var js = Convert("(a, b) => a + b");
        Assert.Equal("(a, b) => a + b", js);
    }

    [Fact]
    public void InterpolatedString_ConvertsToTemplateLiteral()
    {
        var js = Convert("$\"Hello {name}!\"");
        Assert.Equal("`Hello ${name}!`", js);
    }

    [Fact]
    public void Unary_Prefix_ConvertsCorrectly()
    {
        var js = Convert("++i");
        Assert.Equal("++i", js);
    }
    
    [Fact]
    public void Check_Await_Expression()
    {
        var js = Convert("await Task.Delay(100)");
        // Invocation might add this.Task or similar, but structure should be await ...
        Assert.StartsWith("await ", js);
    }

    [Fact]
    public void Initializer_Object_ConvertsToJSObject()
    {
        var js = Convert("new { A = 1, B = 2 }");
        Assert.Equal("{ a: 1, b: 2 }", js);
    }

    [Fact]
    public void Initializer_Dictionary_ConvertsToJSObject()
    {
        var js = Convert("new Dictionary<string, int> { { \"a\", 1 } }");
        // ObjectCreationStrategy might handle the new Dictionary part, but InitializerStrategy handles the body
        // The result depends on how ObjectCreationStrategy calls ConvertInitializer
        // Based on my fix, it should work.
        // Wait, ObjectCreationStrategy might output "new Dictionary... { ... }" if not handled?
        // No, ObjectCreationStrategy handles "new Dictionary<...>" -> "{}" or arguments.
        // It converts initializer and appends/returns it.
        // For new Dictionary { ... }, implicit creation logic might return just the initializer if it detects it.
        // But let's test just the initializer expression itself?
        // InitializerExpressionSyntax is usually part of ObjectCreation.
        // Let's test `new Dictionary<string,int> { {"A", 1} }` as a whole expression.
        Assert.Equal("{ 'a': 1 }", js);
    }

    [Fact]
    public void IsPattern_TypeCheck_ConvertsTo_Typeof()
    {
        var js = Convert("x is string");
        Assert.Equal("typeof x === 'string'", js);
    }
    
    [Fact]
    public void IsPattern_Declaration_ConvertsToIIFE()
    {
        var js = Convert("x is string s");
        Assert.Equal("((() => { s = x; return typeof x === 'string'; })())", js);
    }
}
