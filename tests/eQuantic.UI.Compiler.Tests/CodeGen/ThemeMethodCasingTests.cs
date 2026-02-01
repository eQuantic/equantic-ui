using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

public class ThemeMethodCasingTests
{
    [Fact]
    public void ConditionalAccess_ThemeMethod_WithPropertyArgument_ShouldConvertToCamelCase()
    {
        // Use TestHelper.ConvertCodeBlock which has proper enum/class definitions
        var code = @"
            var theme = new object();
            var result = theme?.ToString();
        ";
        var result = TestHelper.ConvertCodeBlock(code);

        // Just verify the pattern works
        result.Should().Contain("theme?.toString()");
    }

    [Fact]
    public void ConditionalAccess_WithArgument_ShouldPreserveArgument()
    {
        // buttonTheme?.GetVariant(Variant) where Variant is a property
        var code = "buttonTheme?.GetMethod(Name)";
        var result = TestHelper.ConvertExpression(code);

        Console.WriteLine($"Input:  buttonTheme?.GetMethod(Name)");
        Console.WriteLine($"Output: {result}");

        // Method should be camelCase, argument should use this. prefix if it's a property
        result.Should().Be("buttonTheme?.getMethod(this.name)");
    }

    [Fact]
    public void RealWorld_ButtonComponent_ThemeMethodCall()
    {
        // Simulate the exact code from Button.cs Build() method
        var code = @"
            var buttonTheme = new object();
            var result = StyleBuilder.Create(buttonTheme?.Base).Add(buttonTheme?.GetVariant(Variant));
        ";
        var result = TestHelper.ConvertCodeBlock(code);

        Console.WriteLine($"Generated code:\n{result}");

        // Verify method name is camelCase
        result.Should().Contain("getVariant");
        result.Should().NotContain("GetVariant");
    }

    [Fact]
    public void ConditionalAccess_ChainedInsideMethodCall_ShouldConvertToCamelCase()
    {
        // StyleBuilder.Create(buttonTheme?.Base).Add(buttonTheme?.GetVariant(Variant))
        var code = "StyleBuilder.Create(buttonTheme?.Base).Add(buttonTheme?.GetVariant(Variant))";
        var result = TestHelper.ConvertExpression(code);

        // StyleBuilder should become styleBuilder, Add should become push (from mapping), GetVariant should become getVariant
        result.Should().Contain("getVariant");
    }

    [Fact]
    public void ConditionalAccess_PropertyAccess_ShouldConvertToCamelCase()
    {
        var code = "buttonTheme?.Base";
        var result = TestHelper.ConvertExpression(code);

        result.Should().Be("buttonTheme?.base");
    }

    [Fact]
    public void ConditionalAccess_MethodWithArgument_ShouldConvertMethodToCamelCase()
    {
        var code = "theme?.GetSize(Size.Medium)";
        var result = TestHelper.ConvertExpression(code);

        result.Should().Be("theme?.getSize('medium')");
    }
}
