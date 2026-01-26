using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class LinqStrategyTests
{
    [Fact]
    public void Select_MapsTo_Map()
    {
        var result = TestHelper.ConvertExpression("list.Select(x => x.Id)");
        result.Should().Be("this.list.map((x) => x.id)");
    }

    [Fact]
    public void Where_MapsTo_Filter()
    {
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active)");
        result.Should().Be("this.list.filter((x) => x.active)");
    }

    [Fact]
    public void First_NoPredicate_MapsTo_Index0()
    {
        var result = TestHelper.ConvertExpression("list.First()");
        result.Should().Be("this.list[0]");
    }

    [Fact]
    public void First_WithPredicate_MapsTo_Find()
    {
        var result = TestHelper.ConvertExpression("list.First(x => x.Id == 1)");
        result.Should().Be("this.list.find((x) => x.id === 1)");
    }

    [Fact]
    public void Any_NoPredicate_MapsTo_LengthCheck()
    {
        var result = TestHelper.ConvertExpression("list.Any()");
        result.Should().Be("(this.list.length > 0)");
    }

    [Fact]
    public void Any_WithPredicate_MapsTo_Some()
    {
        var result = TestHelper.ConvertExpression("list.Any(x => x.Active)");
        result.Should().Be("this.list.some((x) => x.active)");
    }
    
    [Fact]
    public void All_MapsTo_Every()
    {
        var result = TestHelper.ConvertExpression("list.All(x => x.Active)");
        result.Should().Be("this.list.every((x) => x.active)");
    }

    [Fact]
    public void OrderBy_MapsTo_Sort()
    {
        // Simple case: OrderBy generic
        // We expect .sort((a, b) => ...) transformation
        var result = TestHelper.ConvertExpression("list.OrderBy(x => x.Id)");
        // The implementation should produce a sort function
        result.Should().Contain(".sort(");
        result.Should().StartWith("this.list");
    }
    [Fact]
    public void Chained_Calls_Respect_Order()
    {
        // list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)");
        
        // Check structural correctness of the chain
        result.Should().StartWith("this.list.filter((x) => x.active)");
        result.Should().Contain(".sort(");
        result.Should().EndWith(".map((x) => x.id)");
    }

    [Fact]
    public void Nested_Lambdas_Recurse_Correctly()
    {
        // list.Select(u => u.Orders.Where(o => o.Total > 100))
        var result = TestHelper.ConvertExpression("list.Select(u => u.Orders.Where(o => o.Total > 100))");
        
        // This validates that the inner .Where() is correctly converted inside the .Select() callback
        // Note: u.Orders is likely property of order, but since u is lambda param, it should NOT have this.
        // Wait, u is a TestClass? Yes. So Orders is a property.
        // It should be u.orders.
        result.Should().Be("this.list.map((u) => u.orders.filter((o) => o.total > 100))");
    }

    [Fact]
    public void Complex_Predicate_With_Nested_Scope()
    {
        // list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))");
        
        // This validates that variable 'x' from outer scope is accessible in inner 'Any'
        result.Should().Be("this.list.filter((x) => x.active && this.otherList.some((y) => y.id === x.id))");
    }
}
