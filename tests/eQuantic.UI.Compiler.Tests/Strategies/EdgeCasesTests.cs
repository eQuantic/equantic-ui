using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class EdgeCasesTests : StrategyTestBase
{
    [Fact]
    public void BaseExpression_ConvertsToSuper()
    {
        var code = "base.Method()";
        var js = Convert(code);
        Assert.StartsWith("super.method()", js);
    }

    [Fact]
    public void CheckedStatement_UnwrapsBlock()
    {
        var code = "checked { var x = 1; }";
        var js = Convert(code);
        Assert.DoesNotContain("checked", js);
        Assert.Contains("let x = 1;", js);
    }

    [Fact]
    public void TypeofExpression_ConvertsToStringName()
    {
        var code = "typeof(string)";
        var js = Convert(code);
        Assert.StartsWith("'string'", js);
    }

    [Fact]
    public void ElementAccess_MultiDimensional_ConvertsToChainedIndexers()
    {
        var code = "arr[1, 2]";
        var js = Convert(code);
        Assert.StartsWith("arr[1][2]", js);
    }
}
