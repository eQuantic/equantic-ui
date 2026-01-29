using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class ListStrategyTests
{
    [Fact]
    public void Add_MapsToPush()
    {
        var result = TestHelper.ConvertExpression("list.Add(item)");
        result.Should().Be("this.list.push(this.item)");
    }

    [Fact]
    public void AddRange_MapsToPushSpread()
    {
        var result = TestHelper.ConvertExpression("list.AddRange(items)");
        result.Should().Be("this.list.push(...this.items)");
    }

    [Fact]
    public void Insert_MapsToSplice()
    {
        var result = TestHelper.ConvertExpression("list.Insert(0, item)");
        result.Should().Be("this.list.splice(0, 0, this.item)");
    }

    [Fact]
    public void RemoveAt_MapsToSplice()
    {
        var result = TestHelper.ConvertExpression("list.RemoveAt(5)");
        result.Should().Be("this.list.splice(5, 1)");
    }

    [Fact]
    public void RemoveRange_MapsToSplice()
    {
        var result = TestHelper.ConvertExpression("list.RemoveRange(2, 3)");
        result.Should().Be("this.list.splice(2, 3)");
    }

    [Fact]
    public void Clear_MapsToSpliceZero()
    {
        var result = TestHelper.ConvertExpression("list.Clear()");
        result.Should().Be("this.list.splice(0)");
    }

    [Fact]
    public void IndexOf_MapsToIndexOf()
    {
        var result = TestHelper.ConvertExpression("list.IndexOf(item)");
        result.Should().Be("this.list.indexOf(this.item)");
    }

    [Fact]
    public void LastIndexOf_MapsToLastIndexOf()
    {
        var result = TestHelper.ConvertExpression("list.LastIndexOf(item)");
        result.Should().Be("this.list.lastIndexOf(this.item)");
    }

    [Fact]
    public void Find_MapsToFind()
    {
        var result = TestHelper.ConvertExpression("list.Find(x => x.Active)");
        result.Should().Be("this.list.find((x) => x.active)");
    }

    [Fact]
    public void FindIndex_MapsToFindIndex()
    {
        var result = TestHelper.ConvertExpression("list.FindIndex(x => x.Active)");
        result.Should().Be("this.list.findIndex((x) => x.active)");
    }

    [Fact]
    public void FindAll_MapsToFilter()
    {
        var result = TestHelper.ConvertExpression("list.FindAll(x => x.Active)");
        result.Should().Be("this.list.filter((x) => x.active)");
    }

    [Fact]
    public void Exists_MapsToSome()
    {
        var result = TestHelper.ConvertExpression("list.Exists(x => x.Active)");
        result.Should().Be("this.list.some((x) => x.active)");
    }

    [Fact]
    public void TrueForAll_MapsToEvery()
    {
        var result = TestHelper.ConvertExpression("list.TrueForAll(x => x.Active)");
        result.Should().Be("this.list.every((x) => x.active)");
    }

    [Fact]
    public void Sort_NoArgs_MapsToSort()
    {
        var result = TestHelper.ConvertExpression("list.Sort()");
        result.Should().Be("this.list.sort()");
    }

    [Fact]
    public void Sort_WithComparison_MapsToSort()
    {
        var result = TestHelper.ConvertExpression("list.Sort((a, b) => a.Id - b.Id)");
        result.Should().Be("this.list.sort((a, b) => a.id - b.id)");
    }

    [Fact]
    public void ForEach_MapsToForEach()
    {
        var result = TestHelper.ConvertExpression("list.ForEach(x => Console.WriteLine(x))");
        result.Should().Be("this.list.forEach((x) => console.log(x))");
    }

    [Fact]
    public void GetRange_MapsToSlice()
    {
        var result = TestHelper.ConvertExpression("list.GetRange(2, 5)");
        result.Should().Be("this.list.slice(2, 2 + 5)");
    }
}
