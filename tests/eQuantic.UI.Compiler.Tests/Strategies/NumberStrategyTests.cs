using FluentAssertions;
using eQuantic.UI.Compiler.CodeGen;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class NumberStrategyTests
{
    /// <summary>
    /// Every numeric type reads text with the runtime's reader for its kind (#376), naming itself by
    /// a tag, since `parseInt` and `parseFloat` read the longest prefix that looks like a number:
    /// "12abc" was 12, an int kept digits past its range, and a long became a number.
    /// </summary>
    [Theory]
    [InlineData("int.Parse(str)", "$eq.num.intParse(this.str, 'int')")]
    [InlineData("uint.Parse(str)", "$eq.num.intParse(this.str, 'uint')")]
    [InlineData("long.Parse(str)", "$eq.num.intParse(this.str, 'long')")]
    [InlineData("ulong.Parse(str)", "$eq.num.intParse(this.str, 'ulong')")]
    [InlineData("short.Parse(str)", "$eq.num.intParse(this.str, 'short')")]
    [InlineData("ushort.Parse(str)", "$eq.num.intParse(this.str, 'ushort')")]
    [InlineData("byte.Parse(str)", "$eq.num.intParse(this.str, 'byte')")]
    [InlineData("sbyte.Parse(str)", "$eq.num.intParse(this.str, 'sbyte')")]
    [InlineData("System.Int32.Parse(str)", "$eq.num.intParse(this.str, 'int')")]
    [InlineData("double.Parse(str)", "$eq.num.realParse(this.str, 'double')")]
    // A float read from text is a single, rounded once from the digits by the runtime.
    [InlineData("float.Parse(str)", "$eq.num.realParse(this.str, 'single')")]
    [InlineData("int.Parse(str, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)",
        "$eq.num.intParse(this.str, 'int', 515)")]
    // A named style written before the text is evaluated first, as C# evaluates every argument.
    [InlineData("double.Parse(style: System.Globalization.NumberStyles.Float, s: str)",
        "(($0, $1) => $eq.num.realParse($1, 'double', $0))(167, this.str)")]
    public void ANumberReadFromText_IsTheRuntimesReader(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
    }

    [Fact]
    public void DecimalParse_IsTheRuntimesReader()
    {
        // A decimal read from text is a Decimal, rounded as .NET's parser rounds it (#358): the
        // double parseFloat made lost 0.1 on the way in and had no `add` for the next operation.
        var result = TestHelper.ConvertExpression("decimal.Parse(str)");
        result.Should().Be("$eq.num.decParse(this.str)");
    }

    [Fact]
    public void DecimalTryParse_LeavesZeroInTheOutWhenItFails()
    {
        var result = TestHelper.ConvertExpression("decimal.TryParse(str, out var value)");
        result.Should().Be("((value = $eq.num.decTryParse(this.str)) !== undefined || ((value = $eq.num.dec(0)), false))");
    }

    /// <summary>
    /// The browser reads a number in the invariant culture and no other. A provider that names it
    /// is left out without a word; no provider is EQ2110, because C# then reads in the request's
    /// culture ("1,5" is 1.5 in pt) and the browser reads 15; any other provider is EQ2108.
    /// </summary>
    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decParse(this.str)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decConvert(this.str)")]
    [InlineData("Convert.ToDecimal((object)str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.decConvert(this.str)")]
    [InlineData("Convert.ToDecimal(provider: System.Globalization.CultureInfo.InvariantCulture, value: str)", "$eq.num.decConvert(this.str)")]
    public void ADecimalReadInTheInvariantCulture_LeavesTheProviderOut(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110" || d.Code == "EQ1004");
    }

    [Theory]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.CurrentCulture)")]
    [InlineData("decimal.Parse(str, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"))")]
    [InlineData("decimal.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"), out var v)")]
    [InlineData("Convert.ToDecimal(str, System.Globalization.CultureInfo.CurrentCulture)")]
    // A value that may be text when the call runs is read in the culture the call names.
    [InlineData("Convert.ToDecimal((object)str, System.Globalization.CultureInfo.CurrentCulture)")]
    [InlineData("Convert.ToDecimal((IConvertible)str, System.Globalization.CultureInfo.CurrentCulture)")]
    public void ADecimalReadInAnotherCulture_IsABuildError(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2108");
    }

    [Theory]
    [InlineData("decimal.Parse(str)")]
    [InlineData("decimal.Parse(str, null)")]
    [InlineData("decimal.TryParse(str, out var v)")]
    [InlineData("Convert.ToDecimal(str)")]
    [InlineData("Convert.ToDecimal((object)str)")]
    public void ADecimalReadWithNoCulture_IsWarnedAbout(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2110" && d.Severity == ConversionSeverity.Warning);
    }

    /// <summary>A number converts into a decimal with no culture involved, so it is not asked for one.</summary>
    [Theory]
    [InlineData("Convert.ToDecimal(Value)")]
    [InlineData("Convert.ToDecimal(Amount)")]
    [InlineData("Convert.ToDecimal(Active)")]
    public void ANumberConvertedToDecimal_NeedsNoCulture(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110");
    }

    [Fact]
    public void IntTryParse_IntoADiscard_AssignsNothing()
    {
        // Converted as a name, the discard read as a member: `this._ = parseInt(…)` gave the
        // component a property nobody declared.
        var result = TestHelper.ConvertExpression("int.TryParse(str, out _)");
        result.Should().Be("($eq.num.intTryParse(this.str, 'int') !== undefined)");
    }

    /// <summary>A failed TryParse leaves the type's zero in its out, as .NET does, where the NaN
    /// `parseInt` answered stayed there; a long's zero is a BigInt, as every long is.</summary>
    [Theory]
    [InlineData("int.TryParse(str, out var result)",
        "((result = $eq.num.intTryParse(this.str, 'int')) !== undefined || ((result = 0), false))")]
    [InlineData("long.TryParse(str, out var result)",
        "((result = $eq.num.intTryParse(this.str, 'long')) !== undefined || ((result = 0n), false))")]
    [InlineData("double.TryParse(str, out var result)",
        "((result = $eq.num.realTryParse(this.str, 'double')) !== undefined || ((result = 0), false))")]
    [InlineData("float.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)",
        "((result = $eq.num.realTryParse(this.str, 'single', 167)) !== undefined || ((result = 0), false))")]
    public void ANumberTryParsedFromText_LeavesZeroInTheOutWhenItFails(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
    }

    /// <summary>
    /// Every numeric type reads text in a culture, so the policy decimal set holds for all of them
    /// (#376): no provider is EQ2110, the invariant culture is left out without a word, and any
    /// other is EQ2108. An integer's default style reads only the signs in a culture, and that is
    /// enough to differ: a culture whose minus sign is U+2212 reads "−5" where the browser cannot.
    /// </summary>
    [Theory]
    [InlineData("int.Parse(str)")]
    [InlineData("long.TryParse(str, out var v)")]
    [InlineData("double.Parse(str)")]
    [InlineData("float.TryParse(str, out var v)")]
    [InlineData("byte.Parse(str, System.Globalization.NumberStyles.HexNumber)")]
    [InlineData("Convert.ToInt32(str)")]
    [InlineData("Convert.ToDouble(str)")]
    public void ANumberReadWithNoCulture_IsWarnedAbout(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2110" && d.Severity == ConversionSeverity.Warning);
    }

    [Theory]
    [InlineData("int.Parse(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.intParse(this.str, 'int')")]
    [InlineData("ulong.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out _)", "($eq.num.intTryParse(this.str, 'ulong') !== undefined)")]
    [InlineData("double.Parse(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.realParse(this.str, 'double')")]
    [InlineData("Convert.ToInt64(str, System.Globalization.CultureInfo.InvariantCulture)", "$eq.num.intConvert(this.str, 'long')")]
    [InlineData("Convert.ToSingle(provider: System.Globalization.CultureInfo.InvariantCulture, value: str)", "$eq.num.realConvert(this.str, 'single')")]
    public void ANumberReadInTheInvariantCulture_LeavesTheProviderOut(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110" || d.Code == "EQ1004");
    }

    [Theory]
    [InlineData("int.Parse(str, System.Globalization.CultureInfo.CurrentCulture)")]
    [InlineData("double.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"), out var v)")]
    [InlineData("Convert.ToDouble(str, System.Globalization.CultureInfo.CurrentCulture)")]
    public void ANumberReadInAnotherCulture_IsABuildError(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ2108");
    }

    /// <summary>A number converted to another numeric type involves no culture, so it is not asked for one.</summary>
    [Theory]
    [InlineData("Convert.ToInt32(Amount)")]
    [InlineData("Convert.ToDouble(Value)")]
    public void ANumberConvertedToAnotherNumber_NeedsNoCulture(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().NotContain(d => d.Code == "EQ2108" || d.Code == "EQ2110");
    }

    /// <summary>A span of UTF-8 bytes has no twin to read, and a call that names one says so.</summary>
    [Fact]
    public void ANumberReadFromUtf8Bytes_IsRefused()
    {
        TestHelper.DiagnosticsFor("int.Parse(System.Text.Encoding.UTF8.GetBytes(str).AsSpan())")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("numeric Parse/TryParse over UTF-8 bytes"));
    }
}
