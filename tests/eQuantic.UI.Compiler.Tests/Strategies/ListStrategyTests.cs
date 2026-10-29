using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>
    /// <c>AddRange</c> and <c>InsertRange</c> go through the runtime, one element at a time: they were
    /// <c>push(...items)</c> and <c>splice(index, 0, ...items)</c>, which hand every element to the call as
    /// an argument, past the cap an engine sets on them (#1060). A named argument fills the parameter it
    /// names, where the collection took the index's place.
    /// </summary>
    [Theory]
    [InlineData("list.AddRange(otherList)", "$eq.collections.addRange(this.list, this.otherList)")]
    [InlineData("list.InsertRange(1, otherList)", "$eq.collections.insertRange(this.list, 1, this.otherList)")]
    [InlineData("list.InsertRange(collection: otherList, index: 1)", "$eq.collections.insertRange(this.list, 1, this.otherList)")]
    public void ARange_IsTheRuntimes(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>With no model to bind the call, a range goes through the same runtime functions.</summary>
    [Theory]
    [InlineData("list.AddRange(items)", "$eq.collections.addRange(list, items)")]
    [InlineData("list.InsertRange(0, items)", "$eq.collections.insertRange(list, 0, items)")]
    public void ARange_WithNoModel_IsTheRuntimes(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>With no model, a named argument could fill either of a range's parameters: a build error
    /// rather than a guess, which put a named collection in the index's place.</summary>
    [Theory]
    [InlineData("list.InsertRange(collection: items, index: 0)")]
    [InlineData("list.AddRange(collection: items)")]
    public void ARange_WithNoModel_RefusesANamedArgument(string code)
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression(code));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004");
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
    public void IndexOf_OfAnIdentityElement_MapsToIndexOf()
    {
        var result = TestHelper.ConvertExpression("numbers.IndexOf(5)");
        result.Should().Be("this.numbers.indexOf(5)");
    }

    [Fact]
    public void IndexOf_ComparesAsTheDefaultComparer()
    {
        // indexOf's === never finds a record equal to one in the list, nor a NaN (#425).
        TestHelper.ConvertExpression("points.IndexOf(new DistinctPoint(1))")
            .Should().Be("$eq.collections.indexOf(this.points, new DistinctPoint(1), true)");
        TestHelper.ConvertExpression("list.IndexOf(item)")
            .Should().Be("$eq.collections.indexOf(this.list, this.item, 'item')");
    }

    [Fact]
    public void LastIndexOf_ComparesAsTheDefaultComparer()
    {
        var result = TestHelper.ConvertExpression("list.LastIndexOf(item)");
        result.Should().Be("$eq.collections.lastIndexOf(this.list, this.item, 'item')");
    }

    [Fact]
    public void Find_AnswersTheElementTypesDefault()
    {
        // No match is default(T), which for an int is 0, not undefined (#488).
        TestHelper.ConvertExpression("list.Find(x => x.Active)")
            .Should().Be("$eq.collections.find(this.list, (x) => x.active, null)");
        TestHelper.ConvertExpression("numbers.Find(x => x > 1)")
            .Should().Be("$eq.collections.find(this.numbers, (x) => x > 1, 0)");
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
    public void Sort_NoArgs_SortsByTheTypesDefaultComparer()
    {
        // By value, not by text, with the helper .NET picks for an IComparable<T> (#488).
        TestHelper.ConvertExpression("numbers.Sort()")
            .Should().Be("$eq.collections.listSort(this.numbers, $eq.collections.order('value', 'comparable'))");
        // A class that is not comparable sorts as .NET's: it throws once two elements are compared.
        TestHelper.ConvertExpression("list.Sort()")
            .Should().Be("$eq.collections.listSort(this.list, $eq.collections.order(null))");
    }

    [Fact]
    public void Sort_WithComparison_SortsAsDotNet()
    {
        // The comparison's type names it in .NET's message about an inconsistent one.
        var result = TestHelper.ConvertExpression("list.Sort((a, b) => a.Id - b.Id)");
        result.Should().Be("$eq.collections.listSortBy(this.list, (a, b) => a.id - b.id, 'System.Comparison`1[TestClass]')");
    }

    [Fact]
    public void Sort_WithAStringComparer_SortsByItsComparison()
    {
        TestHelper.ConvertExpression("items.Sort(StringComparer.OrdinalIgnoreCase)")
            .Should().Be("$eq.collections.listSort(this.items, $eq.collections.stringOrder('ordinalIgnoreCase'))");
    }

    [Fact]
    public void Sort_WithAComparerNoTwinCarries_IsRefused()
    {
        var diagnostics = TestHelper.DiagnosticsFor(
            "items.Sort(StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, true))");
        diagnostics.Should().Contain(d => d.Code == "EQ2007");
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
