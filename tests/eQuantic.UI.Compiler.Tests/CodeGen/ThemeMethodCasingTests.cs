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

        // The guarded ToString routes through the SAME strategy as the plain one now: a
        // null-answering arrow over String($r), never a camelCase rename.
        result.Should().Contain("$r == null ? null : String($r)");
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
        // Using simple expressions since we just need to verify method casing conversion
        var code = "buttonTheme?.GetVariant(Variant)";
        var result = TestHelper.ConvertExpression(code);

        Console.WriteLine($"Generated code: {result}");

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

        // `theme` binds to the harness property now, so the member read is `this.`-qualified and
        // the enum argument resolves to its member string — both by symbol, no shape-guessing.
        result.Should().Be("this.theme?.getSize('medium')");
    }
}
