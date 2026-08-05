using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class MissingStrategiesTests
{
    /// <summary>
    /// A range used as a VALUE. It used to convert to `{ start: 1, end: 5 }` and this test blessed
    /// it — but nothing on the JavaScript side ever read that object, so the only thing the shape
    /// bought was silence. Slicing with a range is <see cref="Strategies.RangeIndexerTests"/>;
    /// storing one is reported.
    /// </summary>
    [Fact]
    public void RangeAsAValue_IsReportedRatherThanFaked()
    {
        TestHelper.DiagnosticsFor("var r = 1..5")
            .Should().Contain(d => d.Code == "EQ2004");
    }

    [Fact]
    public void ThrowExpression_ConvertsToIIFE()
    {
        var code = "x ?? throw new Exception()";
        // Assuming NullCoalescingStrategy works and delegates
        // convert(x) ?? convert(throw)
        // Null coalescing in JS is ??
        var js = ConvertExpression(code);
        Assert.Contains("(() => { throw new Error(); })()", js);
    }

    [Fact]
    public void StackAlloc_Int_ConvertsToInt32Array()
    {
        var code = "stackalloc int[10]";
        var js = ConvertExpression(code);
        Assert.Equal("new Int32Array(10)", js);
    }
    
    [Fact]
    public void StackAlloc_Initializer_ConvertsToTypedArrayWithValues()
    {
        var code = "stackalloc int[] { 1, 2, 3 }";
        var js = ConvertExpression(code);
        Assert.Equal("new Int32Array([1, 2, 3])", js);
    }

    [Fact]
    public void LockStatement_UnwrapsBody()
    {
        var code = "lock(obj) { x = 1; }";
        var js = ConvertStatement(code);
        // Should preserve block structure
        var expected = "{x=1;}";
        Assert.Contains(expected, js.Replace(" ", ""));
        Assert.DoesNotContain("lock", js);
    }
    
    [Fact]
    public void YieldReturn_ConvertsToYield()
    {
        var code = "yield return 1;";
        var js = ConvertStatement(code);
        Assert.Equal("yield 1;", js);
    }

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }

    private string ConvertStatement(string code)
    {
        var converter = new CSharpToJsConverter();
        // Parse as GlobalStatement for simple statements or Block for blocks
        // For 'lock', SyntaxFactory.ParseStatement works
        var stmt = SyntaxFactory.ParseStatement(code);
        return converter.ConvertStatement(stmt);
    }
}
