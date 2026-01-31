using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Scenarios;

/// <summary>
/// Tests to validate ConvertCodeBlock functionality
/// </summary>
public class CodeBlockConversionTests
{
    [Fact]
    public void ConvertCodeBlock_SimpleAssignment_Works()
    {
        var code = "var x = 10;";
        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("let x = 10");
    }

    [Fact]
    public void ConvertCodeBlock_MultipleStatements_Works()
    {
        var code = @"
            var x = 10;
            var y = 20;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("let x = 10");
        result.Should().Contain("let y = 20");
    }

    [Fact]
    public void ConvertCodeBlock_WithNullCoalescing_Works()
    {
        var code = "name ??= \"default\";";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("??");
        result.Should().Contain("name");
    }

    [Fact]
    public void ConvertCodeBlock_WithIf_Works()
    {
        var code = @"
            if (x > 10)
                y = 20;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("if");
        result.Should().Contain("x > 10");
    }
}
