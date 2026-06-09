using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Unit coverage for the .NET-coverage strategies added via the conformance program: numeric type
/// constants, bool.Parse, and Convert.ToXxx. (Conformance tests validate runtime behavior; these
/// run without a JS engine.)
/// </summary>
public class NumericCoverageTests
{
    [Theory]
    [InlineData("int.MaxValue", "2147483647")]
    [InlineData("int.MinValue", "-2147483648")]
    [InlineData("byte.MaxValue", "255")]
    [InlineData("short.MinValue", "-32768")]
    public void NumericConstants_MapToLiteralValue(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    [Fact]
    public void BoolParse_MapsToCaseInsensitiveComparison()
    {
        TestHelper.ConvertExpression("bool.Parse(\"true\")")
            .Should().Be("(String('true').trim().toLowerCase() === 'true')");
    }

    [Fact]
    public void ConvertToString_MapsToString()
    {
        // Must win over the generic x.ToString() strategy.
        TestHelper.ConvertExpression("Convert.ToString(42)").Should().Be("String(42)");
    }

    [Fact]
    public void ConvertToInt32_FromStringLiteral_UsesParseInt()
    {
        TestHelper.ConvertExpression("Convert.ToInt32(\"42\")").Should().Be("parseInt('42', 10)");
    }

    [Fact]
    public void ConvertToBoolean_FromNumber_UsesNonZero()
    {
        TestHelper.ConvertExpression("Convert.ToBoolean(1)").Should().Be("((1) !== 0)");
    }
}
