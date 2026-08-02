using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A receiver typed only as a COLLECTION may be a <c>HashSet</c> at run time, and a HashSet lowers to
/// a JS Set: no <c>includes</c>, no <c>length</c>, and <c>Add</c> that answers the set rather than
/// whether the value was new. Each of those returns <c>undefined</c> or a truthy object instead of
/// failing, so the page keeps rendering and the control simply stops responding — which is exactly
/// how the DataTable's checkboxes went quiet. These pin the shape-agnostic lowering.
/// </summary>
public class OpenCollectionShapeTests
{
    [Fact]
    public void Contains_OnACollectionInterface_AsksTheValueWhatItIs()
    {
        var result = TestHelper.ConvertExpression("selection.Contains(\"a\")");
        result.Should().Be("$eq.collections.contains(this.selection, 'a')");
    }

    [Fact]
    public void Contains_UnderANullGuard_REPLACESTheGuardRatherThanHangingOffIt()
    {
        // `this.selection?.contains(...)` is the shape that shipped: a member no Set and no array
        // has, reached through a guard. The helper answers false for null, so it needs none.
        var result = TestHelper.ConvertExpression("selection?.Contains(\"a\") == true");
        result.Should().Be("$eq.collections.contains(this.selection, 'a') === true");
    }

    [Fact]
    public void Contains_OnAList_StaysThePlainArrayMember()
    {
        TestHelper.ConvertExpression("items.Contains(\"a\")").Should().Be("this.items.includes('a')");
    }

    [Fact]
    public void Contains_OnAString_StaysThePlainStringMember()
    {
        TestHelper.ConvertExpression("str.Contains(\"a\")").Should().Be("this.str.includes('a')");
    }

    [Fact]
    public void Count_OnACollectionInterface_AsksTheValueWhatItIs()
    {
        TestHelper.ConvertExpression("selection.Count > 0")
            .Should().Be("$eq.collections.count(this.selection) > 0");
    }

    [Fact]
    public void Count_OnAList_StaysLength()
    {
        TestHelper.ConvertExpression("items.Count > 0").Should().Be("this.items.length > 0");
    }

    [Fact]
    public void Count_OnAHashSet_IsTheSetsOwnSize()
    {
        TestHelper.ConvertExpression("tags.Count > 0").Should().Be("this.tags.size > 0");
    }

    [Fact]
    public void Add_ToASet_ANSWERS_WhetherTheValueWasNew()
    {
        // A JS Set.add returns the SET — always truthy — so `if (!set.Add(x)) set.Remove(x)` became
        // "add, and never remove": a checkbox that ticks once and then ignores you.
        TestHelper.ConvertExpression("tags.Add(\"a\")")
            .Should().Be("$eq.collections.setAdd(this.tags, 'a')");
    }

    [Fact]
    public void Remove_And_Contains_OnAHashSet_KeepTheSetsOwnMembers()
    {
        TestHelper.ConvertExpression("tags.Remove(\"a\")").Should().Be("this.tags.delete('a')");
        // A HashSet the compiler can SEE is a Set: its own member, no question to ask at run time.
        TestHelper.ConvertExpression("tags.Contains(\"a\")").Should().Be("this.tags.has('a')");
    }

    [Fact]
    public void ASequence_IsAsOpenAsACollection()
    {
        TestHelper.ConvertExpression("sequence.Contains(\"a\")")
            .Should().Be("$eq.collections.contains(this.sequence, 'a')");
    }

    [Fact]
    public void ACollectionExpression_TakesItsShapeFromTheTARGET_NotFromTheBrackets()
    {
        // `HashSet<string> _selected = ["#3841"]` lowered to a plain ARRAY, so the very first
        // `_selected.Add(...)` threw — which is what a checkbox that never responds looks like.
        TestHelper.ConvertStatement("HashSet<string> picked = [\"a\", \"b\"];")
            .Should().Be("let picked = new Set(['a', 'b']);");
    }

    [Fact]
    public void ASetInterfaceTarget_IsStillASet()
    {
        TestHelper.ConvertStatement("ISet<string> picked = [\"a\"];")
            .Should().Be("let picked = new Set(['a']);");
    }

    [Fact]
    public void AListTarget_IsStillAPlainArray()
    {
        TestHelper.ConvertStatement("List<string> names = [\"a\"];")
            .Should().Be("let names = ['a'];");
    }
}
