using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class SetOperationsLinqTests
{
    // ============ Concat ============

    [Fact]
    public void Concat_MapsToSpreadOperator()
    {
        var result = TestHelper.ConvertExpression("list.Concat(otherList)");
        result.Should().Be("[...this.list, ...this.otherList]");
    }

    [Fact]
    public void Concat_WithInlineArray_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Concat(new[] { 1, 2, 3 })");
        // new[] { 1, 2, 3 } is converted to "new[] { 1, 2, 3 }" (not fully converted yet)
        result.Should().Contain("[...this.list, ...");
        result.Should().Contain("1, 2, 3");
    }

    [Fact]
    public void Concat_Chained_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Concat(otherList).Where(x => x.Active)");
        result.Should().Contain("[...this.list, ...this.otherList]");
        result.Should().Contain("filter");
    }

    // ============ Union ============

    [Fact]
    public void Union_MapsToSetSpread()
    {
        var result = TestHelper.ConvertExpression("list.Union(otherList)");
        result.Should().Be("[...new Set([...this.list, ...this.otherList])]");
    }

    [Fact]
    public void Union_RemovesDuplicates()
    {
        // Union should create a Set to remove duplicates
        var result = TestHelper.ConvertExpression("items.Union(items)");
        result.Should().Contain("new Set");
    }

    [Fact]
    public void Union_WithWhere_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Union(otherList).Where(x => x.Id > 0)");
        result.Should().Contain("[...new Set([...this.list, ...this.otherList])]");
        result.Should().Contain("filter");
    }

    // ============ Intersect ============

    [Fact]
    public void Intersect_MapsToFilterWithIncludes()
    {
        var result = TestHelper.ConvertExpression("list.Intersect(otherList)");
        result.Should().Be("[...new Set(this.list)].filter(x => this.otherList.includes(x))");
    }

    [Fact]
    public void Intersect_FindsCommonElements()
    {
        // Intersect should filter source by items that exist in other
        var result = TestHelper.ConvertExpression("items.Intersect(list)");
        result.Should().Contain("filter(x => this.list.includes(x))");
    }

    [Fact]
    public void Intersect_WithSelect_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Intersect(otherList).Select(x => x.Name)");
        result.Should().Contain("filter(x => this.otherList.includes(x))");
        result.Should().Contain("map");
    }

    // ============ Except ============

    [Fact]
    public void Except_MapsToFilterWithNegatedIncludes()
    {
        var result = TestHelper.ConvertExpression("list.Except(otherList)");
        result.Should().Be("[...new Set(this.list)].filter(x => !this.otherList.includes(x))");
    }

    [Fact]
    public void Except_ExcludesElements()
    {
        // Except should filter out items that exist in other
        var result = TestHelper.ConvertExpression("items.Except(list)");
        result.Should().Contain("filter(x => !this.list.includes(x))");
    }

    [Fact]
    public void Except_WithCount_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Except(otherList).Count()");
        result.Should().Contain("filter(x => !this.otherList.includes(x))");
        result.Should().Contain(".length");
    }

    // ============ Real-World Scenarios ============

    [Fact]
    public void CombinedSetOperations_MapsCorrectly()
    {
        // Union then Except
        var result = TestHelper.ConvertExpression("list.Union(otherList).Except(items)");
        result.Should().Contain("new Set");
        result.Should().Contain("filter(x => !this.items.includes(x))");
    }

    [Fact]
    public void SetOperationsWithLinq_MapsCorrectly()
    {
        // Concat, filter, then distinct-like behavior with Union
        var result = TestHelper.ConvertExpression("list.Concat(otherList).Where(x => x.Active).Distinct()");
        result.Should().Contain("[...this.list, ...this.otherList]");
        result.Should().Contain("filter");
        result.Should().Contain("new Set");
    }
}
