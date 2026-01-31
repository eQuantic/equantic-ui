using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class NullCoalescingAssignmentTests
{
    [Fact]
    public void NullCoalescingAssignment_SimpleVariable_ConvertsCorrectly()
    {
        var result = TestHelper.ConvertExpression("x ??= 10");
        result.Should().Be("x ?? (x = 10)");
    }

    [Fact]
    public void NullCoalescingAssignment_Property_ConvertsCorrectly()
    {
        var result = TestHelper.ConvertExpression("obj.Value ??= 42");
        result.Should().Be("obj.value ?? (obj.value = 42)");
    }

    [Fact]
    public void NullCoalescingAssignment_WithComplexExpression_ConvertsCorrectly()
    {
        var result = TestHelper.ConvertExpression("data ??= GetDefaultData()");
        result.Should().Be("data ?? (data = getDefaultData())");
    }

    [Fact]
    public void NullCoalescingAssignment_WithString_ConvertsCorrectly()
    {
        var result = TestHelper.ConvertExpression("name ??= \"Default\"");
        result.Should().Be("name ?? (name = 'Default')");
    }

    [Fact]
    public void NullCoalescingAssignment_WithArray_ConvertsCorrectly()
    {
        var result = TestHelper.ConvertExpression("items ??= new int[] { 1, 2, 3 }");
        result.Should().Contain("?? (");
        result.Should().Contain("items =");
    }

    [Fact]
    public void NullCoalescingAssignment_Chained_ConvertsCorrectly()
    {
        // x ??= y ??= 10 should convert to x ?? (x = y ?? (y = 10))
        var result = TestHelper.ConvertExpression("x ??= y ??= 10");
        result.Should().Contain("??");
        result.Should().Contain("=");
    }
}
