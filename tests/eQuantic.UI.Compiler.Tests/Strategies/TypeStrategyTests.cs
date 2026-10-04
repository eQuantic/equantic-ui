using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class TypeStrategyTests
{
    [Fact]
    public void Tuple_Expression_MapsTo_Array()
    {
        var result = TestHelper.ConvertExpression("(1, \"a\")");
        result.Should().Be("[1, 'a']");
    }

    [Fact]
    public void Guid_NewGuid_MapsTo_RandomUUID()
    {
        var result = TestHelper.ConvertExpression("Guid.NewGuid()");
        result.Should().Be("crypto.randomUUID()");
    }

    [Fact]
    public void Guid_Empty_MapsTo_ZeroUUID()
    {
        var result = TestHelper.ConvertExpression("Guid.Empty");
        result.Should().Be("'00000000-0000-0000-0000-000000000000'");
    }
    
    [Fact]
    public void Guid_Parse_ReadsTheCanonicalText()
    {
        // The text .NET writes for the Guid, whatever spelling it was read from (#459).
        var result = TestHelper.ConvertExpression("Guid.Parse(\"abc\")");
        result.Should().Be("$eq.guid.parse('abc')");
    }
}
