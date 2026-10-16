using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// What a LINQ operator that holds a key or an element hands the runtime: the key type's equality
/// (<c>ElementEquality</c>), and a JavaScript <c>Map</c>, <c>Set</c> or <c>===</c> only for a type those
/// compare as .NET does (#922). The conformance rows prove the answers; these pin the shapes, so a change
/// that moves a key back onto <c>===</c>, or an <c>int</c> key off it, is seen where it is made.
/// </summary>
public class LinqKeyEqualityLoweringTests
{
    [Fact]
    public void AJoinByAnAnonymousKey_HoldsItsKeysInTheRuntimesDictionary()
    {
        var result = TestHelper.ConvertExpression("Orders.Join(Orders, o => new { o.Id }, i => new { i.Id }, (o, i) => o.Id)");
        result.Should().Contain("$eq.collections.dictionary(null, true)").And.NotContain("new Map()");
    }

    [Fact]
    public void AJoinByAnIntKey_KeepsTheMap()
    {
        var result = TestHelper.ConvertExpression("Orders.Join(Orders, o => o.Id, i => i.Id, (o, i) => o.Id)");
        result.Should().Contain("new Map()").And.NotContain("$eq.collections.dictionary");
    }

    [Fact]
    public void AGroupByAnIntOrAStringKey_KeepsIdentity()
    {
        TestHelper.ConvertExpression("Items.GroupBy(x => x.Id)").Should().Contain("$x.key === $key");
        TestHelper.ConvertExpression("Items.GroupBy(x => x.Name)").Should().Contain("$x.key === $key");
    }

    [Fact]
    public void AGroupByADoubleKey_FindsNaNEqualToItself()
    {
        TestHelper.ConvertExpression("Items.GroupBy(x => x.Amount)")
            .Should().Contain("$eq.equals($x.key, $key)").And.NotContain("$x.key === $key");
    }

    [Fact]
    public void AGroupByAClassKey_AsksItsOwnEquals()
    {
        TestHelper.ConvertExpression("Items.GroupBy(x => x.item)")
            .Should().Contain("$eq.collections.sameItem($x.key, $key)").And.NotContain("$x.key === $key");
    }

    [Fact]
    public void ADistinctByAnAnonymousKey_HoldsItsKeysInTheRuntimesSet()
    {
        TestHelper.ConvertExpression("Items.DistinctBy(x => new { x.Id, x.Name })")
            .Should().Contain("$eq.collections.hashSet(true)").And.NotContain("new Set()");
    }
}
