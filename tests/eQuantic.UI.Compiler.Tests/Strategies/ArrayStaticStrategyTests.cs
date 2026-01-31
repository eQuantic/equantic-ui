using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class ArrayStaticStrategyTests
{
    // ============ Array.Sort ============

    [Fact]
    public void ArraySort_MapsToSort()
    {
        var result = TestHelper.ConvertExpression("Array.Sort(items)");
        result.Should().Be("this.items.sort()");
    }

    [Fact]
    public void ArraySort_WithComparison_MapsToSortWithComparison()
    {
        var result = TestHelper.ConvertExpression("Array.Sort(items, (a, b) => a - b)");
        result.Should().Be("this.items.sort((a, b) => a - b)");
    }

    // ============ Array.Reverse ============

    [Fact]
    public void ArrayReverse_MapsToReverse()
    {
        var result = TestHelper.ConvertExpression("Array.Reverse(items)");
        result.Should().Be("this.items.reverse()");
    }

    // ============ Array.Find ============

    [Fact]
    public void ArrayFind_MapsToFind()
    {
        var result = TestHelper.ConvertExpression("Array.Find(list, x => x.Active)");
        result.Should().Be("this.list.find((x) => x.active)");
    }

    // ============ Array.FindIndex ============

    [Fact]
    public void ArrayFindIndex_MapsToFindIndex()
    {
        var result = TestHelper.ConvertExpression("Array.FindIndex(list, x => x.Id > 0)");
        result.Should().Be("this.list.findIndex((x) => x.id > 0)");
    }

    // ============ Array.FindAll ============

    [Fact]
    public void ArrayFindAll_MapsToFilter()
    {
        var result = TestHelper.ConvertExpression("Array.FindAll(list, x => x.Active)");
        result.Should().Be("this.list.filter((x) => x.active)");
    }

    // ============ Array.IndexOf ============

    [Fact]
    public void ArrayIndexOf_MapsToIndexOf()
    {
        var result = TestHelper.ConvertExpression("Array.IndexOf(items, str)");
        result.Should().Be("this.items.indexOf(this.str)");
    }

    // ============ Array.LastIndexOf ============

    [Fact]
    public void ArrayLastIndexOf_MapsToLastIndexOf()
    {
        var result = TestHelper.ConvertExpression("Array.LastIndexOf(items, \"test\")");
        result.Should().Be("this.items.lastIndexOf('test')");
    }

    // ============ Array.Exists ============

    [Fact]
    public void ArrayExists_MapsToSome()
    {
        var result = TestHelper.ConvertExpression("Array.Exists(list, x => x.Active)");
        result.Should().Be("this.list.some((x) => x.active)");
    }

    // ============ Array.TrueForAll ============

    [Fact]
    public void ArrayTrueForAll_MapsToEvery()
    {
        var result = TestHelper.ConvertExpression("Array.TrueForAll(list, x => x.Id > 0)");
        result.Should().Be("this.list.every((x) => x.id > 0)");
    }

    // ============ Array.Clear ============

    [Fact]
    public void ArrayClear_MapsToSplice()
    {
        var result = TestHelper.ConvertExpression("Array.Clear(items)");
        result.Should().Be("this.items.splice(0)");
    }

    // ============ Array.Resize ============

    [Fact]
    public void ArrayResize_MapsToLengthAssignment()
    {
        var result = TestHelper.ConvertExpression("Array.Resize(ref items, 10)");
        result.Should().Be("items.length = 10");
    }
}
