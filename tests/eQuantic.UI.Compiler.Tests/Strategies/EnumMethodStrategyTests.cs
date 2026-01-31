using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class EnumMethodStrategyTests
{
    // ============ Enum.Parse<T> ============

    [Fact]
    public void EnumParse_WithGeneric_MapsToParseEnumHelper()
    {
        var result = TestHelper.ConvertExpression("Enum.Parse<Status>(str)");
        result.Should().Be("parseEnum(this.str, Status)");
    }

    [Fact]
    public void EnumParse_WithLiteral_MapsToParseEnumHelper()
    {
        var result = TestHelper.ConvertExpression("Enum.Parse<OrderStatus>(\"active\")");
        result.Should().Be("parseEnum('active', OrderStatus)");
    }

    // ============ Enum.TryParse<T> ============

    [Fact]
    public void EnumTryParse_WithOutVar_MapsToParseEnumWithCheck()
    {
        var result = TestHelper.ConvertExpression("Enum.TryParse<Status>(str, out var result)");
        result.Should().Be("(result = parseEnum(this.str, Status), result !== undefined)");
    }

    [Fact]
    public void EnumTryParse_WithOutExisting_MapsToParseEnumWithCheck()
    {
        var result = TestHelper.ConvertExpression("Enum.TryParse<Status>(\"pending\", out var status)");
        result.Should().Be("(status = parseEnum('pending', Status), status !== undefined)");
    }

    // ============ Enum.GetValues<T> ============

    [Fact]
    public void EnumGetValues_Generic_MapsToObjectValues()
    {
        var result = TestHelper.ConvertExpression("Enum.GetValues<Status>()");
        result.Should().Be("Object.values(Status)");
    }

    [Fact]
    public void EnumGetValues_WithTypeof_MapsToObjectValues()
    {
        var result = TestHelper.ConvertExpression("Enum.GetValues(typeof(OrderStatus))");
        result.Should().Be("Object.values(OrderStatus)");
    }

    // ============ Enum.GetNames<T> ============

    [Fact]
    public void EnumGetNames_Generic_MapsToObjectKeys()
    {
        var result = TestHelper.ConvertExpression("Enum.GetNames<Status>()");
        result.Should().Be("Object.keys(Status)");
    }

    [Fact]
    public void EnumGetNames_WithTypeof_MapsToObjectKeys()
    {
        var result = TestHelper.ConvertExpression("Enum.GetNames(typeof(OrderStatus))");
        result.Should().Be("Object.keys(OrderStatus)");
    }

    // ============ Enum.IsDefined ============

    [Fact]
    public void EnumIsDefined_WithString_MapsToUndefinedCheck()
    {
        var result = TestHelper.ConvertExpression("Enum.IsDefined(typeof(Status), \"active\")");
        result.Should().Be("(Status['active'] !== undefined)");
    }

    [Fact]
    public void EnumIsDefined_WithVariable_MapsToUndefinedCheck()
    {
        var result = TestHelper.ConvertExpression("Enum.IsDefined(typeof(Status), str)");
        result.Should().Be("(Status[this.str] !== undefined)");
    }

    [Fact]
    public void EnumIsDefined_WithNumber_MapsToUndefinedCheck()
    {
        var result = TestHelper.ConvertExpression("Enum.IsDefined(typeof(Status), 1)");
        result.Should().Be("(Status[1] !== undefined)");
    }
}
