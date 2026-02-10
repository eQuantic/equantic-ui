using Xunit;
using eQuantic.UI.Compiler.Tests;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class EdgeCaseTests
{
    [Fact]
    public void Convert_Identifier_WithUnderscore_AddsThis()
    {
        // Heuristic: _name -> this._name
        var result = TestHelper.ConvertExpression("_count");
        Assert.Equal("this._count", result);
    }
    
    [Fact]
    public void Convert_Identifier_Uppercased_AddsThis()
    {
        // Heuristic: Property -> this.Property
        var result = TestHelper.ConvertExpression("Count");
        Assert.Equal("this.count", result);
    }
    
    [Fact]
    public void Convert_Identifier_Lowercased_ReturnsAsIs()
    {
        // Heuristic: local var -> local var
        var result = TestHelper.ConvertExpression("count");
        Assert.Equal("count", result);
    }
    
    [Fact]
    public void Convert_SizeOf_EvaluatesToConstant()
    {
        // sizeof(int) is now evaluated at compile time to its constant value
        var code = "sizeof(int)";
        var result = TestHelper.ConvertExpression(code);
        Assert.Equal("4", result);
    }
}
