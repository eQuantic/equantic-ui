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

    [Theory]
    [InlineData("5L", "5n")]     // long -> BigInt literal (exact 64-bit)
    [InlineData("9223372036854775807L", "9223372036854775807n")] // long.MaxValue literal, exact
    [InlineData("1.5f", "1.5")]  // float suffix stripped
    [InlineData("2.0d", "2.0")]  // double suffix stripped
    [InlineData("100u", "100")]  // unsigned suffix stripped
    public void NumericLiteralSuffixes_AreHandled(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    [Fact]
    public void DecimalLiteral_BecomesDecHelper()
    {
        TestHelper.ConvertExpression("1.1m").Should().Be("dec(\"1.1\")");
    }

    [Fact]
    public void DecimalArithmetic_RoutesThroughDecimalClass()
    {
        // Both operands wrapped in dec() (pass-through for Decimals, coercion for plain numbers).
        TestHelper.ConvertExpression("1.1m + 2.2m")
            .Should().Be("dec(dec(\"1.1\")).add(dec(dec(\"2.2\")))");
    }
}
