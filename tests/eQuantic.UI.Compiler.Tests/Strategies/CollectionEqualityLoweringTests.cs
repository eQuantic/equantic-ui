using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// ONE decision for how <c>EqualityComparer&lt;T&gt;.Default</c> compares two values of a type, read by
/// every search the comparer makes in .NET: a set's elements, a dictionary's keys, a list's and an
/// array's <c>IndexOf</c>, <c>Contains</c> and <c>Remove</c> (#425, #531). What each lowering hands the
/// runtime is pinned here; what the runtime answers with it is the conformance suite's.
/// </summary>
public class CollectionEqualityLoweringTests
{
    [Fact]
    public void ASet_TakesItsElementTypesEquality()
    {
        // By identity (nothing passed), by value, and by what an element of a type that does not
        // decide turns out to be.
        TestHelper.ConvertExpression("new HashSet<int>()").Should().Be("$eq.collections.hashSet()");
        TestHelper.ConvertExpression("new HashSet<(int, string)>()").Should().Be("$eq.collections.hashSet(true)");
        TestHelper.ConvertExpression("new HashSet<object>()").Should().Be("$eq.collections.hashSet('own')");
        TestHelper.ConvertExpression("new HashSet<DistinctPoint>(10)").Should().Be("$eq.collections.hashSet(true, 10)");
        TestHelper.ConvertExpression("new HashSet<TestClass>(list)").Should().Be("$eq.collections.hashSet('item', this.list)");
    }

    [Fact]
    public void ATupleHoldingAnArray_GetsAComparisonGeneratedFromItsElements()
    {
        // ValueTuple.Equals compares an array element by reference, where $eq.equals walks it: the
        // comparison is generated from the element types, for a set's elements and a dictionary's
        // keys alike.
        TestHelper.ConvertExpression("new HashSet<(int[], int)>()")
            .Should().Be("$eq.collections.hashSet($eq.collections.tupleEquality(false, false))");
        TestHelper.ConvertExpression("new Dictionary<(int[], int), int>()")
            .Should().Be("$eq.collections.dictionary(null, $eq.collections.tupleEquality(false, false))");
    }

    [Fact]
    public void AnAnonymousTypeAndAPair_CompareEachMemberByItsOwnType()
    {
        TestHelper.ConvertExpression("new[] { new { A = 1, B = new[] { 1 } } }.ToList().IndexOf(new { A = 1, B = new[] { 1 } })")
            .Should().Contain("$eq.collections.memberEquality({ a: false, b: false })");
        TestHelper.ConvertExpression("new List<KeyValuePair<string, int[]>>().IndexOf(new KeyValuePair<string, int[]>(\"a\", null))")
            .Should().Contain("$eq.collections.pairComparer(false, false)");
        TestHelper.ConvertExpression("new List<KeyValuePair<string, int>>().IndexOf(new KeyValuePair<string, int>(\"a\", 1))")
            .Should().EndWith(", true)");
    }

    [Fact]
    public void ASetsOwnMembers_CrossWithEveryArgument()
    {
        // The fallback once handed a member its FIRST argument only: CopyTo(array, index, count)
        // copied everything to the start of the array.
        TestHelper.ConvertExpression("tags.CopyTo(new string[2], 0, 1)").Should().Be("this.tags.copyTo(new Array(2).fill(null), 0, 1)");
        TestHelper.ConvertExpression("tags.RemoveWhere(x => x == \"a\")").Should().Be("this.tags.removeWhere((x) => x === 'a')");
        TestHelper.ConvertExpression("tags.SetEquals(items)").Should().Be("this.tags.setEquals(this.items)");
        TestHelper.ConvertExpression("tags.Contains(\"a\")").Should().Be("this.tags.has('a')");
    }

    [Fact]
    public void ASetsLinqCall_IsLinqs()
    {
        // A set member is the set's own; an extension on a set receiver goes where LINQ goes.
        TestHelper.ConvertExpression("tags.ToList()").Should().NotContain("toList");
        TestHelper.ConvertExpression("numbers.ToHashSet()").Should().Be("$eq.collections.hashSet(false, this.numbers)");
    }

    [Fact]
    public void TryGetValue_IsRefused_NotCalledWithHalfItsArguments()
    {
        TestHelper.DiagnosticsFor("tags.TryGetValue(\"a\", out var actual)").Should().Contain(d => d.Code == "EQ1004");
    }

    [Fact]
    public void ToHashSet_WithAComparerThatChangesEquality_IsRefused()
    {
        TestHelper.DiagnosticsFor("items.ToHashSet(StringComparer.OrdinalIgnoreCase)").Should().Contain(d => d.Code == "EQ2007");
        TestHelper.DiagnosticsFor("items.ToHashSet(StringComparer.Ordinal)").Should().NotContain(d => d.Code == "EQ2007");
    }

    /// <summary>
    /// Refused and reported, a call is written as its own C# text, as an unhandled one is: text that
    /// fails where it runs. A value standing in for it ran as a sort that sorted nothing and a set
    /// nothing built, wherever the diagnostic was not the end of the build (found in review).
    /// </summary>
    [Theory]
    [InlineData("items.ToHashSet(StringComparer.OrdinalIgnoreCase)", "EQ2007")]
    [InlineData("items.Sort(StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, true))", "EQ2007")]
    [InlineData("new List<object>().Sort()", "EQ1004")]
    [InlineData("Array.Sort(new object[] { 1 })", "EQ1004")]
    public void ARefusedCall_IsWrittenAsItsOwnText(string code, string refusal)
    {
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == refusal);
        TestHelper.ConvertExpression(code).Should().Be(code);
    }

    [Fact]
    public void ArraysStatics_SearchSortAndFindThroughTheRuntime()
    {
        TestHelper.ConvertExpression("Array.Sort(new[] { 3, 1 })")
            .Should().Be("$eq.collections.arraySort([3, 1], $eq.collections.order('value', 'comparable'))");
        TestHelper.ConvertExpression("Array.IndexOf(new[] { 1.5 }, 1.5)").Should().Be("$eq.collections.arrayIndexOf([1.5], 1.5)");
        TestHelper.ConvertExpression("Array.Find(new[] { 1 }, x => x > 0)").Should().Be("$eq.collections.arrayFind([1], (x) => x > 0, 0)");
        TestHelper.ConvertExpression("Array.FindLastIndex(new[] { 1 }, 0, 1, x => x > 0)")
            .Should().Be("$eq.collections.arrayFindLastIndex([1], (x) => x > 0, 0, 1)");
    }

    [Fact]
    public void AComparerCreatedFromAComparison_Crosses()
    {
        TestHelper.ConvertExpression("numbers.BinarySearch(0, 1, 5, Comparer<int>.Create((a, b) => a - b))")
            .Should().Be("$eq.collections.binarySearch(this.numbers, 5, $eq.collections.comparerOrder({ compare: ((a, b) => a - b) }, "
                + "$eq.collections.order('value', 'comparable'), 'System.Collections.Generic.ComparisonComparer`1[System.Int32]'), 0, 1)");
    }

    [Fact]
    public void NamedArguments_FillTheirParameters()
    {
        TestHelper.ConvertExpression("numbers.CopyTo(arrayIndex: 1, array: new int[3])")
            .Should().Be("$eq.collections.copyTo(this.numbers, new Array(3).fill(0), 1)");
        TestHelper.ConvertExpression("numbers.FindIndex(count: 1, startIndex: 0, match: x => x > 0)")
            .Should().Be("$eq.collections.findIndex(this.numbers, (x) => x > 0, 0, 1)");
    }

    [Theory]
    // `findIndex` takes its predicate first, so `FindIndex(1, x => x > 4)` handed it the index (#488).
    // The overloads' shapes say where each argument goes even with no model to bind them.
    [InlineData("items.FindIndex(1, x => x > 4)", "$eq.collections.findIndex(items, (x) => x > 4, 1)")]
    [InlineData("items.FindLastIndex(3, 2, x => x > 4)", "$eq.collections.findLastIndex(items, (x) => x > 4, 3, 2)")]
    [InlineData("items.FindIndex(x => x > 4)", "items.findIndex((x) => x > 4)")]
    public void WithNoModel_ARangedFindIndex_TakesItsRangeWhereDotNetDoes(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);
}
