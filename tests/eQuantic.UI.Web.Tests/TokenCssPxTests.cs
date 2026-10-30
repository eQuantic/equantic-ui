using System.Globalization;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The server's half of a cross-pin: <c>lowering.spec.ts</c> asserts the same rows of the twin's <c>px</c>.
/// The browser hydrates what the server wrote and the atomic class is named from this text, so the two
/// must write it alike (#729).
/// </summary>
public class TokenCssPxTests
{
    [Theory]
    [InlineData(1.005f, "1.01px")]
    [InlineData(2.675f, "2.68px")]
    [InlineData(0.125f, "0.13px")]
    [InlineData(540.125f, "540.13px")]
    [InlineData(0.005f, "0.01px")]
    [InlineData(0.004f, "0px")]
    [InlineData(-0.001f, "-0px")]
    [InlineData(-0.005f, "-0.01px")]
    [InlineData(-1.005f, "-1.01px")]
    [InlineData(0.995f, "1px")]
    [InlineData(99.999f, "100px")]
    [InlineData(123456.79f, "123456.8px")]
    [InlineData(16777216f, "16777220px")]
    [InlineData(1e10f, "10000000000px")]
    [InlineData(3.3333333f, "3.33px")]
    [InlineData(1.5e-5f, "0px")]
    [InlineData(1234566.5f, "1234566px")]
    [InlineData(1234567.5f, "1234568px")]
    [InlineData(123456.25f, "123456.2px")]
    [InlineData(1000000.5f, "1000000px")]
    [InlineData(float.NaN, "NaNpx")]
    [InlineData(float.PositiveInfinity, "Infinitypx")]
    [InlineData(float.NegativeInfinity, "-Infinitypx")]
    public void ALength_IsWrittenAsTheTwinWritesIt(float dp, string text) =>
        TokenCss.Px(dp).Should().Be(text, dp.ToString(CultureInfo.InvariantCulture));

    /// <summary>The server's half of the same cross-pin for a bare number, the twin's <c>num</c>.</summary>
    [Theory]
    [InlineData(1.00005f, "1.0001")]
    [InlineData(0.33335f, "0.3334")]
    [InlineData(5e-5f, "0.0001")]
    [InlineData(-1e-5f, "-0")]
    [InlineData(-0f, "-0")]
    [InlineData(1234.5625f, "1234.562")]
    [InlineData(1.5e-7f, "0")]
    [InlineData(0.1f, "0.1")]
    public void ABareNumber_IsWrittenAsTheTwinWritesIt(float value, string text) =>
        TokenCss.Number(value).Should().Be(text, value.ToString(CultureInfo.InvariantCulture));
}
