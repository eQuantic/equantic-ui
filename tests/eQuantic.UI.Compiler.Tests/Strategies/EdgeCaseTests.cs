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
    public void Convert_UnknownExpression_ReturnsToString()
    {
        // Fallback: unsupported syntax returns C# code as-is
        // e.g. sizeof(int) is not supported
        var code = "sizeof(int)";
        var result = TestHelper.ConvertExpression(code);
        Assert.Equal("sizeof(int)", result);
    }
}
