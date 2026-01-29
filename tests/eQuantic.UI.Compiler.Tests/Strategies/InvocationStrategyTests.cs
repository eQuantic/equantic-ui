using FluentAssertions;
using Xunit;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class InvocationStrategyTests
{
    [Fact]
    public void ConsoleWriteLine_ConvertsToConsoleLog()
    {
        var code = "Console.WriteLine(\"Hello\")";
        var js = ConvertExpression(code);
        Assert.Equal("console.log('Hello')", js);
    }

    [Fact]
    public void ConsoleWrite_ConvertsToConsoleLog()
    {
        var code = "Console.Write(\"Hello\")";
        var js = ConvertExpression(code);
        Assert.Equal("console.log('Hello')", js);
    }
    
    [Fact]
    public void MathClamp_ConvertsToMinMax()
    {
        var code = "Math.Clamp(val, 0, 100)";
        var js = ConvertExpression(code);
        Assert.Equal("Math.min(Math.max(val, 0), 100)", js);
    }

    [Fact]
    public void DictionaryContainsKey_ConvertsToInOperator()
    {
        var code = "dict.ContainsKey(\"key\")";
        var js = ConvertExpression(code);
        Assert.Equal("'key' in dict", js);
    }
    
    [Fact]
    public void ServiceProvider_GetService_ConvertsToGetService()
    {
        var code = "provider.GetService<IMyService>()";
        var js = ConvertExpression(code);
        Assert.Equal("provider.getService('IMyService')", js);
    }

    [Fact]
    public void ServerAction_Invocation_ConvertsTo_ThisMethod()
    {
        // Server actions are generated as methods on the component class
        // Invoking them should result in strict 'this.methodName()'
        var code = "MyServerAction(arg1)";
        var converter = new CSharpToJsConverter();
        converter.SetCurrentClass("MyComponent"); // Context needed for 'this' detection
        
        var expr = SyntaxFactory.ParseExpression(code);
        var js = converter.ConvertExpression(expr);
        
        Assert.Equal("this.myServerAction(arg1)", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
