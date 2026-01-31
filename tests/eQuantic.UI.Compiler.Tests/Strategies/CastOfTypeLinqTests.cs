using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class CastOfTypeLinqTests
{
    // ============ Cast<T> ============

    [Fact]
    public void Cast_PassthroughArray()
    {
        var result = TestHelper.ConvertExpression("items.Cast<string>()");
        // Cast is a type assertion, so it just passes through in JS
        result.Should().Be("this.items");
    }

    [Fact]
    public void Cast_WithChaining_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("items.Cast<int>().Where(x => x > 0)");
        result.Should().Contain("this.items");
        result.Should().Contain("filter");
    }

    // ============ OfType<T> ============

    [Fact]
    public void OfType_WithPrimitiveString_MapsToTypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<string>()");
        result.Should().Be("this.items.filter(x => typeof x === 'string')");
    }

    [Fact]
    public void OfType_WithPrimitiveNumber_MapsToTypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<int>()");
        result.Should().Be("this.items.filter(x => typeof x === 'number')");
    }

    [Fact]
    public void OfType_WithPrimitiveBool_MapsToTypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<bool>()");
        result.Should().Be("this.items.filter(x => typeof x === 'boolean')");
    }

    [Fact]
    public void OfType_WithReferenceType_MapsToInstanceofFilter()
    {
        var result = TestHelper.ConvertExpression("list.OfType<Order>()");
        result.Should().Be("this.list.filter(x => x instanceof Order)");
    }

    [Fact]
    public void OfType_WithChaining_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.OfType<Order>().Select(x => x.Id)");
        result.Should().Contain("filter(x => x instanceof Order)");
        result.Should().Contain("map");
    }

    // ============ Real-World Scenarios ============

    [Fact]
    public void Cast_ThenOfType_MapsCorrectly()
    {
        // Cast followed by OfType
        var result = TestHelper.ConvertExpression("items.Cast<object>().OfType<string>()");
        result.Should().Contain("this.items");
        result.Should().Contain("filter(x => typeof x === 'string')");
    }

    [Fact]
    public void OfType_WithCount_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("items.OfType<string>().Count()");
        result.Should().Contain("filter(x => typeof x === 'string')");
        result.Should().Contain(".length");
    }
}
