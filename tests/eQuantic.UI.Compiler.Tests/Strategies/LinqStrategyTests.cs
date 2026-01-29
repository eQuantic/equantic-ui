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

    [Fact]
    public void Skip_MapsTo_Slice()
    {
        var result = TestHelper.ConvertExpression("list.Skip(5)");
        result.Should().Be("this.list.slice(5)");
    }

    [Fact]
    public void Take_MapsTo_Slice()
    {
        var result = TestHelper.ConvertExpression("list.Take(10)");
        result.Should().Be("this.list.slice(0, 10)");
    }

    [Fact]
    public void Skip_Take_Chain_ForPagination()
    {
        var result = TestHelper.ConvertExpression("list.Skip(20).Take(10)");
        result.Should().Be("this.list.slice(20).slice(0, 10)");
    }

    [Fact]
    public void Distinct_MapsTo_SetSpread()
    {
        var result = TestHelper.ConvertExpression("list.Distinct()");
        result.Should().Be("[...new Set(this.list)]");
    }

    [Fact]
    public void Contains_MapsTo_Includes()
    {
        var result = TestHelper.ConvertExpression("list.Contains(item)");
        result.Should().Be("this.list.includes(item)");
    }

    [Fact]
    public void Contains_WithLiteral_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Contains(5)");
        result.Should().Be("this.list.includes(5)");
    }

    [Fact]
    public void Last_NoPredicate_MapsToLastElement()
    {
        var result = TestHelper.ConvertExpression("list.Last()");
        result.Should().Be("(this.list[this.list.length - 1])");
    }

    [Fact]
    public void LastOrDefault_NoPredicate_MapsWithNullCoalescing()
    {
        var result = TestHelper.ConvertExpression("list.LastOrDefault()");
        result.Should().Be("(this.list[this.list.length - 1] ?? null)");
    }

    [Fact]
    public void Last_WithPredicate_MapsToFilterPop()
    {
        var result = TestHelper.ConvertExpression("list.Last(x => x.Active)");
        result.Should().Be("(this.list.filter((x) => x.active).pop())");
    }

    [Fact]
    public void Single_NoPredicate_MapsToFirstElement()
    {
        var result = TestHelper.ConvertExpression("list.Single()");
        result.Should().Be("(this.list[0])");
    }

    [Fact]
    public void Single_WithPredicate_MapsToFind()
    {
        var result = TestHelper.ConvertExpression("list.Single(x => x.Id == 1)");
        result.Should().Be("(this.list.find((x) => x.id === 1))");
    }

    [Fact]
    public void SelectMany_MapsToFlatMap()
    {
        var result = TestHelper.ConvertExpression("list.SelectMany(x => x.Items)");
        result.Should().Be("this.list.flatMap((x) => x.items)");
    }

    [Fact]
    public void Sum_NoPredicate_MapsToReduce()
    {
        var result = TestHelper.ConvertExpression("list.Sum()");
        result.Should().Be("this.list.reduce((_a, _b) => _a + _b, 0)");
    }

    [Fact]
    public void Sum_WithSelector_MapsToReduceWithSelector()
    {
        var result = TestHelper.ConvertExpression("list.Sum(x => x.Amount)");
        result.Should().Be("this.list.reduce((_sum, x) => _sum + x.amount, 0)");
    }

    [Fact]
    public void Average_NoPredicate_MapsToReduceDivide()
    {
        var result = TestHelper.ConvertExpression("list.Average()");
        result.Should().Be("(this.list.reduce((_a, _b) => _a + _b, 0) / this.list.length)");
    }

    [Fact]
    public void Min_NoPredicate_MapsToMathMin()
    {
        var result = TestHelper.ConvertExpression("list.Min()");
        result.Should().Be("Math.min(...this.list)");
    }

    [Fact]
    public void Max_NoPredicate_MapsToMathMax()
    {
        var result = TestHelper.ConvertExpression("list.Max()");
        result.Should().Be("Math.max(...this.list)");
    }

    [Fact]
    public void Min_WithSelector_MapsToMathMinWithMap()
    {
        var result = TestHelper.ConvertExpression("list.Min(x => x.Value)");
        result.Should().Be("Math.min(...this.list.map((x) => x.value))");
    }

    [Fact]
    public void Reverse_MapsToSpreadReverse()
    {
        var result = TestHelper.ConvertExpression("list.Reverse()");
        result.Should().Be("[...this.list].reverse()");
    }
}
