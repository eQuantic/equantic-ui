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

    [Fact]
    public void DoWhileStatement_MapsTo_DoWhile()
    {
        var code = @"
            do {
                x = x - 1;
            } while (x > 0);";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().StartWith("do {");
        result.Should().Contain("x = x - 1;");
        result.Should().Contain("} while (x > 0);");
    }

    [Fact]
    public void ForStatement_MapsTo_For()
    {
        var code = @"
            for (int i = 0; i < 10; i++) {
                sum = sum + i;
            }";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().StartWith("for (let i = 0; i < 10; i++)");
        result.Should().Contain("sum = sum + i;");
    }

    [Fact]
    public void ForStatement_WithMultipleVariables_MapsCorrectly()
    {
        var code = @"
            for (int i = 0, j = 10; i < j; i++) {
                process(i);
            }";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().Contain("let i = 0, j = 10");
        result.Should().Contain("i < j");
    }

    [Fact]
    public void BreakStatement_MapsTo_Break()
    {
        var code = @"
            while (true) {
                if (x > 10) break;
                x = x + 1;
            }";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().Contain("break;");
    }

    [Fact]
    public void ContinueStatement_MapsTo_Continue()
    {
        var code = @"
            for (int i = 0; i < 10; i++) {
                if (i == 5) continue;
                process(i);
            }";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().Contain("continue;");
    }

    [Fact]
    public void ThrowStatement_MapsTo_Throw()
    {
        var code = @"
            if (x < 0) {
                throw new Exception(""Invalid value"");
            }";

        var result = TestHelper.ConvertExpression(code).Replace("\r\n", "\n");
        result.Should().Contain("throw new Error('Invalid value');");
    }
}
