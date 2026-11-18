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
        result.Should().Contain("$eq.query.where(");
    }

    // ============ OfType<T> ============

    /// <summary>OfType filters by <c>typeof</c> when its query is walked (#685).</summary>
    [Fact]
    public void OfType_WithPrimitiveString_DefersATypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<string>()");
        result.Should().Be("$eq.query.defer(($src) => $src.filter(($x) => typeof $x === 'string'), this.items)");
    }

    /// <summary>OfType filters by <c>typeof</c> when its query is walked (#685).</summary>
    [Fact]
    public void OfType_WithPrimitiveNumber_DefersATypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<int>()");
        result.Should().Be("$eq.query.defer(($src) => $src.filter(($x) => typeof $x === 'number'), this.items)");
    }

    /// <summary>OfType filters by <c>typeof</c> when its query is walked (#685).</summary>
    [Fact]
    public void OfType_WithPrimitiveBool_DefersATypeofFilter()
    {
        var result = TestHelper.ConvertExpression("items.OfType<bool>()");
        result.Should().Be("$eq.query.defer(($src) => $src.filter(($x) => typeof $x === 'boolean'), this.items)");
    }

    /// <summary>OfType filters by <c>instanceof</c> when its query is walked (#685).</summary>
    [Fact]
    public void OfType_WithReferenceType_DefersAnInstanceofFilter()
    {
        var result = TestHelper.ConvertExpression("list.OfType<Order>()");
        result.Should().Be("$eq.query.defer(($src) => $src.filter(($x) => $x instanceof Order), this.list)");
    }

    [Fact]
    public void OfType_WithChaining_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.OfType<Order>().Select(x => x.Id)");
        result.Should().Contain("filter(($x) => $x instanceof Order)");
        result.Should().Contain("$eq.query.select(");
    }

    // ============ Real-World Scenarios ============

    [Fact]
    public void Cast_ThenOfType_MapsCorrectly()
    {
        // Cast followed by OfType
        var result = TestHelper.ConvertExpression("items.Cast<object>().OfType<string>()");
        result.Should().Contain("this.items");
        result.Should().Contain("filter(($x) => typeof $x === 'string')");
    }

    [Fact]
    public void OfType_WithCount_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("items.OfType<string>().Count()");
        result.Should().Contain("filter(($x) => typeof $x === 'string')");
        result.Should().Contain(".length");
    }
}
