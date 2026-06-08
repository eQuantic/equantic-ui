using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Regression tests for translation bugs found during the bug hunt:
/// integer division, Math method mapping, ToString format specifiers and
/// switch-expression variable-pattern binding.
/// </summary>
public class BugHuntFixesTests
{
    [Fact]
    public void IntegerDivision_TruncatesTowardZero()
    {
        // C# int / int truncates; JS / is float division.
        var result = TestHelper.ConvertExpression("Id / 2");
        result.Should().Be("Math.trunc(this.id / 2)");
    }

    [Fact]
    public void DecimalDivision_DoesNotTruncate()
    {
        // Total is decimal -> result is fractional, must NOT be wrapped in Math.trunc.
        var result = TestHelper.ConvertExpression("Total / 2");
        result.Should().Be("this.total / 2");
    }

    [Fact]
    public void MathTruncate_MapsToTrunc()
    {
        var result = TestHelper.ConvertExpression("Math.Truncate(Total)");
        result.Should().Be("Math.trunc(this.total)");
    }

    [Fact]
    public void MathCeiling_MapsToCeil()
    {
        var result = TestHelper.ConvertExpression("Math.Ceiling(Total)");
        result.Should().Be("Math.ceil(this.total)");
    }

    [Fact]
    public void MathRound_WithPrecision_EmulatesDigits()
    {
        var result = TestHelper.ConvertExpression("Math.Round(Total, 2)");
        result.Should().Contain("Math.round");
        result.Should().Contain("10 ** (2)");
    }

    [Fact]
    public void ToString_WithFormat_UsesFormatHelper()
    {
        var result = TestHelper.ConvertExpression("Total.ToString(\"F2\")");
        result.Should().Be("format(this.total, 'F2')");
    }

    [Fact]
    public void ToString_WithoutArguments_UsesString()
    {
        var result = TestHelper.ConvertExpression("Total.ToString()");
        result.Should().Be("String(this.total)");
    }

    [Fact]
    public void SwitchExpression_VarPattern_BindsCapturedVariable()
    {
        var result = TestHelper.ConvertCodeBlock("var r = x switch { var v => v + 1 };");
        result.Should().Contain("const v = _s");
        result.Should().Contain("return v + 1");
    }

    [Fact]
    public void OrderBy_IsStable_AndCopiesSource()
    {
        var result = TestHelper.ConvertExpression("list.OrderBy(x => x.Id)");
        result.Should().StartWith("[...this.list].sort("); // copies, does not mutate source
        result.Should().Contain(": 0");                     // returns 0 on equal keys (stable)
        result.Should().NotContain("? 1 : -1");             // old non-stable comparator gone
    }
}
