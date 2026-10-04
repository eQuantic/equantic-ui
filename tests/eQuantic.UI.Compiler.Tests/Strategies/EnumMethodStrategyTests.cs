using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Enum's statics call the runtime's enum functions with the enum's shape written inline: an enum has
/// no object of its own in the browser, and these named one after it (<c>Object.values(Status)</c>,
/// <c>$eq.enums.parse(text, Status)</c>), which no module declares, so each threw (#480). The
/// behaviour runs on both sides in EnumConformanceTests; these pin the spelling.
/// </summary>
public class EnumMethodStrategyTests
{
    private const string Shape = "{ names: ['Small', 'Medium', 'Large'], keys: ['small', 'medium', 'large'], values: [0, 1, 2], flags: false, digits: 8 }";

    [Fact]
    public void EnumParse_ReadsTheEnumsShape()
    {
        TestHelper.ConvertExpression("Enum.Parse<Size>(\"Medium\")")
            .Should().Be($"$eq.enums.parse('Medium', {Shape})");
    }

    [Fact]
    public void EnumParse_WithItsCase_PassesIt()
    {
        TestHelper.ConvertExpression("Enum.Parse<Size>(\"medium\", true)")
            .Should().Be($"$eq.enums.parse('medium', {Shape}, true)");
    }

    [Fact]
    public void EnumTryParse_WritesItsOut_AndTheDefaultWhenItFails()
    {
        TestHelper.ConvertExpression("Enum.TryParse<Size>(\"Large\", out var size)")
            .Should().Be($"((size = $eq.enums.tryParse('Large', {Shape})) !== undefined || ((size = $eq.enums.zero({Shape})), false))");
    }

    [Fact]
    public void EnumTryParse_IntoADiscard_OnlyTests()
    {
        TestHelper.ConvertExpression("Enum.TryParse<Size>(\"Large\", out _)")
            .Should().Be($"($eq.enums.tryParse('Large', {Shape}) !== undefined)");
    }

    [Fact]
    public void EnumGetValues_ByTheTypeArgument_OrTheTypeof()
    {
        TestHelper.ConvertExpression("Enum.GetValues<Size>()").Should().Be($"$eq.enums.values({Shape})");
        TestHelper.ConvertExpression("Enum.GetValues(typeof(Size))").Should().Be($"$eq.enums.values({Shape})");
    }

    [Fact]
    public void EnumGetNames_ByTheTypeArgument_OrTheTypeof()
    {
        TestHelper.ConvertExpression("Enum.GetNames<Size>()").Should().Be($"$eq.enums.names({Shape})");
        TestHelper.ConvertExpression("Enum.GetNames(typeof(Size))").Should().Be($"$eq.enums.names({Shape})");
    }

    [Fact]
    public void EnumIsDefined_ReadsItsArgument_AsTheEnum_ANumber_AName_OrAnObject()
    {
        TestHelper.ConvertExpression("Enum.IsDefined(Size.Large)").Should().Be($"$eq.enums.isDefined('large', {Shape}, 'held')");
        TestHelper.ConvertExpression("Enum.IsDefined(typeof(Size), 2)").Should().Be($"$eq.enums.isDefined(2, {Shape}, 'number')");
        TestHelper.ConvertExpression("Enum.IsDefined(typeof(Size), \"Large\")").Should().Be($"$eq.enums.isDefined('Large', {Shape}, 'name')");
        TestHelper.ConvertExpression("Enum.IsDefined(typeof(Size), (object)2)").Should().Be($"$eq.enums.isDefined(2, {Shape}, 'object')");
    }

    [Fact]
    public void EnumTryParse_ByItsType_LeavesNullWhenItFails()
    {
        TestHelper.ConvertExpression("Enum.TryParse(typeof(Size), \"Large\", out var size)")
            .Should().Be($"((size = $eq.enums.tryParse('Large', {Shape})) !== undefined || ((size = null), false))");
    }
}
