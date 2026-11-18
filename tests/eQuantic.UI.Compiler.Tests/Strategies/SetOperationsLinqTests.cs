using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class SetOperationsLinqTests
{
    // ============ Concat ============

    /// <summary>Concat is the runtime's lazy query, which walks the first sequence and then the second (#685).</summary>
    [Fact]
    public void Concat_WalksBothLazily()
    {
        var result = TestHelper.ConvertExpression("list.Concat(otherList)");
        result.Should().Be("$eq.query.concat(this.list, this.otherList)");
    }

    [Fact]
    public void Concat_WithInlineArray_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("numbers.Concat(new[] { 1, 2, 3 })");
        result.Should().Contain("$eq.query.concat(this.numbers, ");
        result.Should().Contain("1, 2, 3");
    }

    [Fact]
    public void Concat_Chained_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Concat(otherList).Where(x => x.Active)");
        result.Should().Contain("$eq.query.concat(this.list, this.otherList)");
        result.Should().Contain("$eq.query.where(");
    }

    // ============ Union ============

    /// <summary>Union spreads the runtime's set of both sequences when its query is walked (#685).</summary>
    [Fact]
    public void Union_OfAClass_DefersTheRuntimesSetSpread()
    {
        // A class's element is found by its own Equals or by reference (ElementEquality's 'item'), so a
        // subclass that overrides Equals is asked it, as .NET's default comparer asks it (#922).
        var result = TestHelper.ConvertExpression("list.Union(otherList)");
        result.Should().Be("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', [...$src, ...$1])], $0, 'first'))(this.list, this.otherList)");
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
        result.Should().Contain("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', [...$src, ...$1])], $0, 'first'))(this.list, this.otherList)");
        result.Should().Contain("$eq.query.where(");
    }

    // ============ Intersect ============

    /// <summary>Intersect filters the source by its elements' own equality when its query is walked (#685).</summary>
    [Fact]
    public void Intersect_OfAClass_DefersAFilterByItsEquality()
    {
        var result = TestHelper.ConvertExpression("list.Intersect(otherList)");
        result.Should().Be("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', $src)].filter(($x) => $eq.collections.contains($1, $x, 'item')), $0, 'first'))(this.list, this.otherList)");
    }

    [Fact]
    public void Intersect_FindsCommonElements()
    {
        // Intersect should filter source by items that exist in other
        var result = TestHelper.ConvertExpression("items.Intersect(list1)");
        result.Should().Contain("(($0, $1) => $eq.query.defer(($src) => [...new Set($src)].filter(($x) => $1.includes($x)), $0, 'first'))(this.items, this.list1)");
    }

    [Fact]
    public void Intersect_WithSelect_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Intersect(otherList).Select(x => x.Name)");
        result.Should().Contain("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', $src)].filter(($x) => $eq.collections.contains($1, $x, 'item')), $0, 'first'))(this.list, this.otherList)");
        result.Should().Contain("$eq.query.select(");
    }

    // ============ Except ============

    /// <summary>Except filters out the second sequence's elements by their own equality when its query is
    /// walked (#685).</summary>
    [Fact]
    public void Except_OfAClass_DefersANegatedFilterByItsEquality()
    {
        var result = TestHelper.ConvertExpression("list.Except(otherList)");
        result.Should().Be("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', $src)].filter(($x) => !$eq.collections.contains($1, $x, 'item')), $0, 'first'))(this.list, this.otherList)");
    }

    [Fact]
    public void Except_ExcludesElements()
    {
        // Except should filter out items that exist in other
        var result = TestHelper.ConvertExpression("items.Except(excludeList)");
        result.Should().Contain("filter(($x) => !$1.includes($x)), $0, 'first'))(") .And.Contain(", this.excludeList)");
    }

    [Fact]
    public void Except_WithCount_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Except(otherList).Count()");
        result.Should().Contain("(($0, $1) => $eq.query.defer(($src) => [...$eq.collections.hashSet('item', $src)].filter(($x) => !$eq.collections.contains($1, $x, 'item')), $0, 'first'))(this.list, this.otherList)");
        result.Should().Contain(".length");
    }

    // ============ Real-World Scenarios ============

    [Fact]
    public void CombinedSetOperations_MapsCorrectly()
    {
        // Union then Except
        var result = TestHelper.ConvertExpression("items.Union(list1).Except(excludeList)");
        result.Should().Contain("new Set");
        result.Should().Contain("filter(($x) => !$1.includes($x)), $0, 'first'))(") .And.Contain(", this.excludeList)");
    }

    [Fact]
    public void SetOperationsWithLinq_MapsCorrectly()
    {
        // Concat, filter, then distinct-like behavior with Union
        var result = TestHelper.ConvertExpression("list.Concat(otherList).Where(x => x.Active).Distinct()");
        result.Should().Contain("$eq.query.concat(this.list, this.otherList)");
        result.Should().Contain("$eq.query.where(");
        result.Should().Contain("$eq.collections.hashSet('item', ");
    }
}
