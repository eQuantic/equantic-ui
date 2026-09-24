using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class DictionaryStrategyTests
{
    // ============ Existing Methods (Already Implemented) ============

    /// <summary>Its OWN key. `in` walks the prototype chain, so an empty dictionary answered true
    /// for "constructor" and every other Object.prototype member.</summary>
    [Fact]
    public void ContainsKey_AsksForTheObjectsOwnKey()
    {
        var result = TestHelper.ConvertExpression("dict.ContainsKey(\"key\")");
        result.Should().Be("Object.prototype.hasOwnProperty.call(this.dict, 'key')");
    }

    /// <summary>The lowering names the receiver twice — in the own-key question and in the read —
    /// and `this.dict` is a PROPERTY, whose getter could count its calls, so the writer binds it
    /// once. A miss writes default(TValue) to the out: null for a string.</summary>
    [Fact]
    public void TryGetValue_WithOutVar_BindsTheReceiverOnce_AndAMissWritesTheDefault()
    {
        var result = TestHelper.ConvertExpression("dict.TryGetValue(\"key\", out var value)");
        result.Should().Be(
            "(($0) => (Object.prototype.hasOwnProperty.call($0, 'key') ? ((value = $0['key']), true) : ((value = null), false)))(this.dict)");
    }

    /// <summary>A key read through a property is bound once too, after the receiver.</summary>
    [Fact]
    public void TryGetValue_WithVariable_BindsTheReceiverAndTheKeyOnce()
    {
        var result = TestHelper.ConvertExpression("dict.TryGetValue(str, out var result)");
        result.Should().Be(
            "(($0, $1) => (Object.prototype.hasOwnProperty.call($0, $1) ? ((result = $0[$1]), true) : ((result = null), false)))(this.dict, this.str)");
    }

    // ============ New Methods ============

    [Fact]
    public void Add_MapsToIndexerAssignment()
    {
        var result = TestHelper.ConvertExpression("dict.Add(\"key\", \"value\")");
        result.Should().Be("this.dict['key'] = 'value'");
    }

    [Fact]
    public void Add_WithVariables_MapsToIndexerAssignment()
    {
        var result = TestHelper.ConvertExpression("dict.Add(str, item)");
        result.Should().Be("this.dict[this.str] = this.item");
    }

    [Fact]
    public void Remove_MapsToDeleteOperator()
    {
        var result = TestHelper.ConvertExpression("dict.Remove(\"key\")");
        result.Should().Be("delete this.dict['key']");
    }

    [Fact]
    public void Remove_WithVariable_MapsToDeleteOperator()
    {
        var result = TestHelper.ConvertExpression("dict.Remove(str)");
        result.Should().Be("delete this.dict[this.str]");
    }

    /// <summary>The receiver once, not once more per key deleted; and `$k`, which no C# name can
    /// be, so a dictionary called `k` is not shadowed.</summary>
    [Fact]
    public void Clear_DeletesEveryOwnKey_ThroughOneReceiver()
    {
        var result = TestHelper.ConvertExpression("dict.Clear()");
        result.Should().Be("(($0) => Object.keys($0).forEach(($k) => delete $0[$k]))(this.dict)");
    }

    // ============ Properties ============

    [Fact]
    public void Keys_MapsToObjectKeys()
    {
        var result = TestHelper.ConvertExpression("dict.Keys");
        result.Should().Be("Object.keys(this.dict)");
    }

    [Fact]
    public void Values_MapsToObjectValues()
    {
        var result = TestHelper.ConvertExpression("dict.Values");
        result.Should().Be("Object.values(this.dict)");
    }

    // ============ Real-World Scenarios ============

    [Fact]
    public void DictionaryKeys_InForEach_MapsCorrectly()
    {
        var code = @"
            foreach (var key in dict.Keys)
            {
                Console.WriteLine(key);
            }";
        var result = TestHelper.ConvertStatement(code);
        result.Should().Contain("Object.keys(this.dict)");
    }

    [Fact]
    public void DictionaryValues_InLINQ_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("dict.Values.Where(x => x != null)");
        result.Should().Contain("Object.values(this.dict)");
        result.Should().Contain("filter");
    }
}
