using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class OperatorsTests : StrategyTestBase
{
    [Fact]
    public void CastExpression_Numeric_TruncatesValue()
    {
        var code = "(int)1.5";
        var js = Convert(code);
        // C# int cast truncates AND wraps to 32 bits — `| 0` is the JS twin of both.
        Assert.StartsWith("(Math.trunc(1.5) | 0)", js);
    }

    [Fact]
    public void CastExpression_Byte_WrapsToLowBits()
    {
        // (byte)(v >> 8) keeps only the low 8 bits in C# — the mask is the semantics, not polish:
        // without it every Color.FromRgb((byte)(rgb >> 16), …) channel corrupts on the client.
        var js = Convert("(byte)(v >> 8)");
        Assert.Contains("& 0xFF", js);
    }

    [Fact]
    public void CastExpression_Short_SignExtends()
    {
        var js = Convert("(short)70000");
        Assert.Contains("<< 16) >> 16", js);
    }

    [Fact]
    public void CastExpression_String_CallsStringFunction()
    {
        var code = "(string)123";
        var js = Convert(code);
        Assert.StartsWith("String(123)", js);
    }

    /// <summary>
    /// `x as T` is null-on-mismatch, and it shares patterns' one type test (PatternConverter) so
    /// `x as T` and `x is T` can never disagree. It was a passthrough that emitted plain `x` —
    /// which made `if (x as Foo != null)` take the branch for ANY non-null x.
    /// </summary>
    [Fact]
    public void AsExpression_EmitsTheSameTypeTestPatternsUse()
    {
        var js = Convert("x as string");
        Assert.Equal("($as => typeof $as === 'string' ? $as : null)(x);", js);
    }

    [Fact]
    public void AsExpression_NullableValueType_TestsTheUnderlyingType()
    {
        var js = Convert("x as int?");
        Assert.Equal("($as => typeof $as === 'number' ? $as : null)(x);", js);
    }

    [Fact]
    public void ParenthesizedExpression_PreservesParens()
    {
        var code = "(1 + 2) * 3";
        var js = Convert(code);
        Assert.Equal("(1 + 2) * 3;", js);
    }

    [Fact]
    public void SizeOfExpression_ReturnsConstant()
    {
        var code = "sizeof(int)";
        var js = Convert(code);
        Assert.Equal("4;", js);
    }

    [Fact]
    public void AnonymousMethod_ConvertsToArrow()
    {
        var code = "delegate(int x) { return x; }";
        var js = Convert(code);
        Assert.Contains("=>", js);
        Assert.Contains("{returnx;}", System.Text.RegularExpressions.Regex.Replace(js, @"\s+", ""));
    }

    [Fact]
    public void DeconstructionAssignment_Works()
    {
        var code = "(a, b) = (1, 2)";
        var js = Convert(code);
        Assert.StartsWith("[a, b] = [1, 2]", js);
    }
}
