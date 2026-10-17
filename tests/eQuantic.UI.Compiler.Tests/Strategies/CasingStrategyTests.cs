using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// <c>ToUpper</c> and <c>ToLower</c>, of a string and of a char, read in the culture the call names
/// (#848, #725). What each one answers is the conformance suite's (<c>CasingConformanceTests</c>); these
/// pin what only the compiler can show: the flag the invariant culture adds, where a char's culture
/// lands, and what is refused.
/// </summary>
public class CasingStrategyTests
{
    private const string Globalization = "System.Globalization.";

    /// <summary>The current culture and a null one are the page's, which the twin reads with no flag;
    /// the invariant one is its second argument.</summary>
    [Theory]
    [InlineData("str.ToUpper(" + Globalization + "CultureInfo.CurrentCulture)", "$eq.text.toUpper(this.str)")]
    [InlineData("str.ToLower(null)", "$eq.text.toLower(this.str)")]
    [InlineData("str.ToUpper(" + Globalization + "CultureInfo.InvariantCulture)", "$eq.text.toUpper(this.str, true)")]
    [InlineData("str.ToLower(culture: " + Globalization + "CultureInfo.InvariantCulture)", "$eq.text.toLower(this.str, true)")]
    public void AString_IsCasedInTheCultureTheCallNames(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>A char is cased as a string of one. Its culture is read where C# binds it, named or
    /// not, and a null one is handed on for the twin to refuse after the char is evaluated, as .NET
    /// refuses it.</summary>
    [Theory]
    [InlineData("char.ToUpper('a')", "$eq.text.toUpper('a')")]
    [InlineData("char.ToLowerInvariant('A')", "$eq.text.toLower('A', true)")]
    [InlineData("char.ToUpper('a', " + Globalization + "CultureInfo.CurrentCulture)", "$eq.text.toUpper('a')")]
    [InlineData("char.ToLower('A', " + Globalization + "CultureInfo.InvariantCulture)", "$eq.text.toLower('A', true)")]
    [InlineData("char.ToUpper(culture: " + Globalization + "CultureInfo.InvariantCulture, c: 'a')", "$eq.text.toUpper('a', true)")]
    [InlineData("char.ToUpper('a', null)", "$eq.text.toUpper('a', null)")]
    public void AChar_IsCasedInTheCultureTheCallNames(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>Any other culture has no counterpart in the browser, which knows the page's and the
    /// invariant one: the build fails where the culture was dropped.</summary>
    [Theory]
    [InlineData("str.ToUpper(" + Globalization + "CultureInfo.GetCultureInfo(\"tr-TR\"))")]
    [InlineData("str.ToLower(new " + Globalization + "CultureInfo(\"az\"))")]
    [InlineData("char.ToUpper('i', " + Globalization + "CultureInfo.GetCultureInfo(\"tr-TR\"))")]
    public void ACasing_InAnotherCulture_IsRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ2108" && d.Message.Contains("changing case"));

    /// <summary>A char's casing with a culture, taken as a delegate, would hand the culture as a value
    /// the browser cannot hold: the build fails, as its call does with a culture it cannot name (#839).
    /// Without one it crosses.</summary>
    [Fact]
    public void AChar_WithACulture_TakenAsADelegate_IsRefused() =>
        TestHelper.DiagnosticsFor("new Func<char, " + Globalization + "CultureInfo, char>(char.ToUpper)")
            .Should().Contain(d => d.Code == "EQ2108" && d.Message.Contains("changing case"));

    [Fact]
    public void AChar_TakenAsADelegate_IsCased() =>
        TestHelper.ConvertExpression("new Func<char, char>(char.ToUpperInvariant)")
            .Should().Contain("$eq.text.toUpper(c, true)");
}
