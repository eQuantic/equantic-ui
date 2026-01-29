using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class ExpressionStrategyTests
{
    [Fact]
    public void ConsoleWriteLine_MapsTo_ConsoleLog()
    {
        var result = TestHelper.ConvertExpression("Console.WriteLine(\"Hello\")");
        result.Should().Be("console.log('Hello')");
    }

    [Fact]
    public void ListAdd_MapsTo_Push()
    {
        var result = TestHelper.ConvertExpression("list.Add(item)");
        result.Should().Be("this.list.push(this.item)");
    }

    [Fact]
    public void StringJoin_MapsTo_Join()
    {
        var result = TestHelper.ConvertExpression("string.Join(\", \", list)");
        result.Should().Be("this.list.join(', ')");
    }

    [Fact]
    public void ObjectCreation_List_MapsTo_Array()
    {
        var result = TestHelper.ConvertExpression("new List<string>()");
        result.Should().Be("[]");
    }

    [Fact]
    public void ObjectCreation_Dictionary_MapsTo_Object()
    {
        var result = TestHelper.ConvertExpression("new Dictionary<string, int>()");
        result.Should().Be("{}");
    }

    [Fact]
    public void BinaryExpression_Equality_MapsTo_StrictEquality()
    {
        var result = TestHelper.ConvertExpression("a == b");
        // 'a' and 'b' are properties in TestHelper, so they get 'this.' prefix
        result.Should().Be("this.a === this.b");
    }

    [Fact]
    public void MemberAccess_Length_MapsTo_Length()
    {
        var result = TestHelper.ConvertExpression("str.Length");
        // str is a property in TestHelper
        result.Should().Be("this.str.length");
    }

    [Fact]
    public void NullCoalescing_MapsTo_QuestionQuestion()
    {
        var result = TestHelper.ConvertExpression("a ?? b");
        // 'a' and 'b' are properties in TestHelper
        result.Should().Be("this.a ?? this.b");
    }

    [Fact]
    public void NullCoalescing_WithExpression_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("name ?? \"default\"");
        result.Should().Be("name ?? 'default'");
    }

    [Fact]
    public void ConditionalAccess_MemberAccess_MapsTo_OptionalChaining()
    {
        var result = TestHelper.ConvertExpression("user?.Name");
        result.Should().Be("user?.name");
    }

    [Fact]
    public void ConditionalAccess_MethodCall_MapsTo_OptionalChaining()
    {
        var result = TestHelper.ConvertExpression("user?.GetName()");
        result.Should().Be("user?.getName()");
    }

    [Fact]
    public void ConditionalAccess_ElementAccess_MapsTo_OptionalChaining()
    {
        var result = TestHelper.ConvertExpression("list?[0]");
        result.Should().Be("this.list?.[0]");
    }

    [Fact]
    public void ConditionalAccess_Chained_MapsTo_OptionalChaining()
    {
        var result = TestHelper.ConvertExpression("user?.Address?.City");
        result.Should().Be("user?.address?.city");
    }

    [Fact]
    public void ConditionalAccess_WithNullCoalescing_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("user?.Name ?? \"Unknown\"");
        result.Should().Be("user?.name ?? 'Unknown'");
    }

    [Fact]
    public void IndexFromEnd_SingleElement_MapsToLengthMinus()
    {
        var result = TestHelper.ConvertExpression("list[^1]");
        result.Should().Be("this.list[this.list.length - 1]");
    }

    [Fact]
    public void IndexFromEnd_SecondLast_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list[^2]");
        result.Should().Be("this.list[this.list.length - 2]");
    }
    [Fact]
    public void SwitchExpression_MapsTo_NestedTernary()
    {
        var result = TestHelper.ConvertExpression("status switch { 0 => \"Pending\", 1 => \"Active\", _ => \"Unknown\" }");
        result.Should().Be("(() => { const _s = status; if (_s === 0) return 'Pending'; if (_s === 1) return 'Active'; return 'Unknown'; })()");
    }
    [Fact]
    public void SwitchExpression_NoDiscard_MapsToNestedTernaryWithNull()
    {
        var result = TestHelper.ConvertExpression("val switch { 0 => \"A\" }");
        result.Should().Be("(() => { const _s = val; if (_s === 0) return 'A'; return null; })()");
    }
    [Fact]
    public void Any_NoArgs_MapsToLengthGreaterThanZeroWithParens()
    {
        var result = TestHelper.ConvertExpression("list.Any()");
        // list is a property in TestHelper
        result.Should().Be("(this.list.length > 0)");
    }

    [Fact]
    public void ImplicitObjectCreation_List_MapsToArray()
    {
        // We simulate the type hint from the emitter
        var result = TestHelper.ConvertExpression("new()", "List<string>");
        result.Should().Be("[]");
    }
}
