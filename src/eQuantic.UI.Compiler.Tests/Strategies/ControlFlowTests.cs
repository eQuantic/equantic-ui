using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class ControlFlowTests
{
    [Fact]
    public void SwitchStatement_MapsTo_Switch()
    {
        var code = @"
            switch (x) {
                case 1:
                    return ""One"";
                case 2:
                    return ""Two"";
                default:
                    return ""Other"";
            }";
            
        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        // Note: Formatting might vary (newlines, etc), so we check logical structure or normalize
        // For simplicity, we expect standard switch syntax
        
        result.Should().Contain("switch (x)");
        result.Should().Contain("case 1:");
        result.Should().Contain("return 'One';");
        result.Should().Contain("default:");
    }

    [Fact]
    public void WhileStatement_MapsTo_While()
    {
        var code = @"
            while (x > 0) {
                x = x - 1;
            }";
            
        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().StartWith("while (x > 0)");
        result.Should().Contain("x = x - 1;");
    }
}
