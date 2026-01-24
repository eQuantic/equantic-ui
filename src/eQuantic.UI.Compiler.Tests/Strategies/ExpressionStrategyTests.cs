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
        result.Should().Be("list.push(item)");
    }

    [Fact]
    public void StringJoin_MapsTo_Join()
    {
        var result = TestHelper.ConvertExpression("string.Join(\", \", list)");
        result.Should().Be("list.join(', ')");
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
        result.Should().Be("a === b");
    }
    
    [Fact]
    public void MemberAccess_Length_MapsTo_Length()
    {
        var result = TestHelper.ConvertExpression("str.Length");
        result.Should().Be("str.length");
    }

    [Fact]
    public void NullCoalescing_MapsTo_QuestionQuestion()
    {
        var result = TestHelper.ConvertExpression("a ?? b");
        // Modern JS supports ??
        result.Should().Be("a ?? b");
    }
    [Fact]
    public void SwitchExpression_MapsTo_NestedTernary()
    {
        var result = TestHelper.ConvertExpression("status switch { 0 => \"Pending\", 1 => \"Active\", _ => \"Unknown\" }");
        result.Should().Be("(status === 0 ? 'Pending' : (status === 1 ? 'Active' : 'Unknown'))");
    }
    [Fact]
    public void SwitchExpression_NoDiscard_MapsToNestedTernaryWithNull()
    {
        var result = TestHelper.ConvertExpression("val switch { 0 => \"A\" }");
        result.Should().Be("(val === 0 ? 'A' : null)");
    }
    [Fact]
    public void Any_NoArgs_MapsToLengthGreaterThanZeroWithParens()
    {
        var result = TestHelper.ConvertExpression("list.Any()");
        result.Should().Be("(list.length > 0)");
    }

    [Fact]
    public void ImplicitObjectCreation_List_MapsToArray()
    {
        // We simulate the type hint from the emitter
        var result = TestHelper.ConvertExpression("new()", "List<string>");
        result.Should().Be("[]");
    }
}
