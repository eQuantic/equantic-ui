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
    public void DictionaryContainsKey_AsksTheClass()
    {
        // Unbound where no model answers: ContainsKey is a name only a dictionary has.
        var code = "dict.ContainsKey(\"key\")";
        var js = ConvertExpression(code);
        Assert.Equal("dict.has('key')", js);
    }
    
    [Fact]
    public void ServiceProvider_GetService_ConvertsToGetService()
    {
        var code = "provider.GetService<IMyService>()";
        var js = ConvertExpression(code);
        Assert.Equal("provider.getService('IMyService')", js);
    }

    /// <summary>The branch that used to answer this silently.
    /// <para>
    /// With neither a type argument nor an argument there is nothing to key the registry on — it is
    /// keyed by the interface NAME — and the strategy emitted <c>provider.getService()</c> anyway,
    /// under a comment that said "this is an error - we should warn / But fallback to empty call for
    /// backwards compatibility". The call cannot be answered at runtime, and nothing said so at
    /// build time. The branch twenty lines above already reported EQ2111 for the same question
    /// reached a different way.
    /// </para>
    /// Both halves are asserted here, because reporting while still emitting the call would leave
    /// the defect in place under a warning.</summary>
    [Fact]
    public void ServiceProvider_GetService_WithNothingToKeyOn_IsEQ2111_AndEmitsNoCall()
    {
        var converter = new CSharpToJsConverter();
        var js = converter.ConvertExpression(SyntaxFactory.ParseExpression("provider.GetService()"));

        js.Should().NotContain("getService(",
            "an unanswerable call must not be written — the silent fallback emitted one");
        converter.Diagnostics.Should().ContainSingle(d => d.Code == "EQ2111",
            "the question has one diagnostic, and this branch reports it like its sibling");
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
