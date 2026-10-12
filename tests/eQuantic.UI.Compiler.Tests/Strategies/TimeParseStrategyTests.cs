using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using eQuantic.UI.Compiler.CodeGen;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// <c>Parse</c> and <c>TryParse</c> of the five time types, read in the culture the call names (#718,
/// #806, #717). What each one reads is the conformance suite's (<c>DateTextInTheCultureConformanceTests</c>,
/// <c>DateTimeTryParseConformanceTests</c>); these pin what only the compiler can show: where each
/// argument lands, the flag the invariant culture adds, the spelling read where no model can be asked,
/// and what is refused.
/// </summary>
public class TimeParseStrategyTests
{
    private const string Globalization = "System.Globalization.";

    /// <summary>A TryParse answers inline over the twin's reader, the type's zero in its out argument
    /// where the text is no value of it.</summary>
    [Theory]
    [InlineData("DateTime.TryParse(str, out var d)",
        "((d = $eq.time.dateTime.tryParse(this.str)) !== undefined || ((d = $eq.time.dateTime.minValue()), false))")]
    [InlineData("DateTimeOffset.TryParse(str, out var d)",
        "((d = $eq.time.dateTimeOffset.tryParse(this.str)) !== undefined || ((d = $eq.time.dateTimeOffset.minValue()), false))")]
    [InlineData("DateOnly.TryParse(str, out var d)",
        "((d = $eq.time.dateOnly.tryParse(this.str)) !== undefined || ((d = $eq.time.dateOnly.minValue()), false))")]
    [InlineData("TimeOnly.TryParse(str, out var d)",
        "((d = $eq.time.timeOnly.tryParse(this.str)) !== undefined || ((d = $eq.time.timeOnly.minValue()), false))")]
    [InlineData("TimeSpan.TryParse(str, out var d)",
        "((d = $eq.time.timeSpan.tryParse(this.str)) !== undefined || ((d = $eq.time.timeSpan.zero), false))")]
    [InlineData("TimeSpan.TryParse(str, out _)", "($eq.time.timeSpan.tryParse(this.str) !== undefined)")]
    public void ATryParse_AnswersOverTheTwinsReader(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>With no provider, the current culture's and a null one, the twin reads the page's
    /// culture, which is the current one; styles that are white space are what Parse does anyway.</summary>
    [Theory]
    [InlineData("DateTime.Parse(str)", "$eq.time.dateTime.parse(this.str)")]
    [InlineData("DateTime.Parse(str, null)", "$eq.time.dateTime.parse(this.str)")]
    [InlineData("DateTimeOffset.Parse(str, " + Globalization + "CultureInfo.CurrentCulture)",
        "$eq.time.dateTimeOffset.parse(this.str)")]
    [InlineData("DateOnly.Parse(str, " + Globalization + "CultureInfo.CurrentCulture, " + Globalization
        + "DateTimeStyles.AllowWhiteSpaces)", "$eq.time.dateOnly.parse(this.str)")]
    [InlineData("TimeOnly.Parse(str, null, " + Globalization + "DateTimeStyles.AllowLeadingWhite | "
        + Globalization + "DateTimeStyles.AllowTrailingWhite)", "$eq.time.timeOnly.parse(this.str)")]
    [InlineData("TimeSpan.Parse(str, null)", "$eq.time.timeSpan.parse(this.str)")]
    [InlineData("DateTime.TryParse(str, null, " + Globalization + "DateTimeStyles.None, out var d)",
        "((d = $eq.time.dateTime.tryParse(this.str)) !== undefined || ((d = $eq.time.dateTime.minValue()), false))")]
    public void AParse_InTheCurrentCulture_ReadsThePagesCulture(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>The invariant culture is the twin's second argument, wherever C# places the provider.</summary>
    [Theory]
    [InlineData("DateTime.Parse(str, " + Globalization + "CultureInfo.InvariantCulture)",
        "$eq.time.dateTime.parse(this.str, true)")]
    [InlineData("TimeSpan.Parse(str, " + Globalization + "CultureInfo.InvariantCulture)",
        "$eq.time.timeSpan.parse(this.str, true)")]
    [InlineData("DateOnly.TryParse(str, " + Globalization + "CultureInfo.InvariantCulture, " + Globalization
        + "DateTimeStyles.None, out var d)",
        "((d = $eq.time.dateOnly.tryParse(this.str, true)) !== undefined || ((d = $eq.time.dateOnly.minValue()), false))")]
    [InlineData("DateTimeOffset.TryParse(result: out var d, styles: " + Globalization + "DateTimeStyles.None, "
        + "formatProvider: " + Globalization + "CultureInfo.InvariantCulture, input: str)",
        "((d = $eq.time.dateTimeOffset.tryParse(this.str, true)) !== undefined || ((d = $eq.time.dateTimeOffset.minValue()), false))")]
    public void AParse_InTheInvariantCulture_SaysSo(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>Any other culture has no counterpart in the browser, which reads the page's and the
    /// invariant one: the build fails where the page used to fail on a name no browser defines.</summary>
    [Theory]
    [InlineData("DateTime.Parse(str, " + Globalization + "CultureInfo.GetCultureInfo(\"pt-BR\"))")]
    [InlineData("TimeOnly.TryParse(str, new " + Globalization + "CultureInfo(\"de-DE\"), out var d)")]
    public void AParse_InAnotherCulture_IsRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ2108" && d.Message.Contains("reading a date or a time"));

    /// <summary>Styles that change what is read, or that no constant settles, and a span, which no twin
    /// reads, fail the build.</summary>
    [Theory]
    [InlineData("DateTime.Parse(str, null, " + Globalization + "DateTimeStyles.AdjustToUniversal)", "with styles that are not white space")]
    [InlineData("DateOnly.Parse(str, null, (" + Globalization + "DateTimeStyles)Value)", "with styles that are not white space")]
    [InlineData("TimeSpan.TryParse(str.AsSpan(), out var d)", "TimeSpan.TryParse with a span")]
    [InlineData("DateTime.Parse(str.AsSpan(), null)", "DateTime.Parse with a span")]
    public void AParse_ThatNoTwinReads_IsRefused(string code, string reason) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains(reason));

    /// <summary>An exact format is #717's and not read here: each one is left to the fence, which fails
    /// the build where the page used to call a member the twin did not have.</summary>
    [Theory]
    [InlineData("DateTime.ParseExact(str, \"d\", null)")]
    [InlineData("DateTime.TryParseExact(str, \"d\", null, " + Globalization + "DateTimeStyles.None, out var d)")]
    [InlineData("DateTimeOffset.ParseExact(str, \"d\", null)")]
    [InlineData("TimeSpan.ParseExact(str, \"c\", null)")]
    [InlineData("DateTime.Now.GetDateTimeFormats()")]
    public void AnExactFormat_IsFenced(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ2004");

    /// <summary>With no model, the type's spelling settles it for <c>Parse(text)</c> and
    /// <c>TryParse(text, out value)</c>.</summary>
    [Theory]
    [InlineData("DateOnly.TryParse(s, out var d)",
        "((d = $eq.time.dateOnly.tryParse(s)) !== undefined || ((d = $eq.time.dateOnly.minValue()), false))")]
    [InlineData("System.TimeSpan.TryParse(result: out var d, s: s)",
        "((d = $eq.time.timeSpan.tryParse(s)) !== undefined || ((d = $eq.time.timeSpan.zero), false))")]
    [InlineData("DateTime.Parse(s)", "$eq.time.dateTime.parse(s)")]
    public void AParse_WithNoModel_ReadsTheTypesSpelling(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>With no model, a provider or styles cannot be told from a span, nor a culture from another:
    /// a build error rather than a guess.</summary>
    [Theory]
    [InlineData("DateTime.TryParse(s, provider, out var d)")]
    [InlineData("DateTime.Parse(s, provider)")]
    public void AParse_WithNoModel_OfAnotherOverload_IsRefused(string code)
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression(code));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("which no model binds"));
    }
}
