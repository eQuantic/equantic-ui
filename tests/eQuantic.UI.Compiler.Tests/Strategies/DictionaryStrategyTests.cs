using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class DictionaryStrategyTests
{
    // ============ Existing Methods (Already Implemented) ============

    [Fact]
    /// <summary>Its OWN key. `in` walks the prototype chain, so an empty dictionary answered true
    /// for "constructor" and every other Object.prototype member.</summary>
    public void ContainsKey_AsksForTheObjectsOwnKey()
    {
        var result = TestHelper.ConvertExpression("dict.ContainsKey(\"key\")");
        result.Should().Be("Object.prototype.hasOwnProperty.call(this.dict, 'key')");
    }

    [Fact]
    public void TryGetValue_WithOutVar_MapsToAssignmentCheck()
    {
        var result = TestHelper.ConvertExpression("dict.TryGetValue(\"key\", out var value)");
        result.Should().Be(
            "(Object.prototype.hasOwnProperty.call(this.dict, 'key') ? ((value = this.dict['key']), true) : false)");
    }

    [Fact]
    public void TryGetValue_WithVariable_MapsToAssignmentCheck()
    {
        var result = TestHelper.ConvertExpression("dict.TryGetValue(str, out var result)");
        result.Should().Be(
            "(Object.prototype.hasOwnProperty.call(this.dict, this.str) ? ((result = this.dict[this.str]), true) : false)");
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

    [Fact]
    public void Clear_MapsToForEachDelete()
    {
        var result = TestHelper.ConvertExpression("dict.Clear()");
        result.Should().Be("Object.keys(this.dict).forEach(k => delete this.dict[k])");
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
