using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// A property or a field of a .NET type that no strategy translates is EQ2004, as a method that
/// reaches the invocation fallback is. The read went out as its camelCase name on whatever the
/// receiver became, so <c>CultureInfo.CurrentCulture.Name</c> was <c>CultureInfo.currentCulture.name</c>,
/// a name the runtime does not export, and the page failed where .NET answers while the build said
/// nothing (#847). The members the browser does carry by name keep building.
/// </summary>
public class PlatformMemberReadTests
{
    /// <summary>A refused static call is told once, at the call, whichever spelling names the type: the
    /// keyword also reported EQ1001 for itself, as an expression with no strategy (#953).</summary>
    [Theory]
    [InlineData("var s = string.IsInterned(\"a\")", "string.IsInterned")]
    [InlineData("var s = String.IsInterned(\"a\")", "string.IsInterned")]
    [InlineData("var s = string.Copy(\"a\")", "string.Copy")]
    [InlineData("var s = String.Copy(\"a\")", "string.Copy")]
    public void ARefusedStaticCall_IsToldOnce_WhicheverSpellingNamesTheType(string code, string member)
    {
        var refused = TestHelper.DiagnosticsFor(code).Should().ContainSingle().Which;
        refused.Code.Should().Be("EQ2004");
        refused.Message.Should().StartWith($"'{member}' has no JavaScript translation");
    }

    /// <summary>A chain is refused once, at the first member nothing carries, and the message names it.</summary>
    [Theory]
    [InlineData("var n = System.Globalization.CultureInfo.CurrentCulture.Name", "System.Globalization.CultureInfo.CurrentCulture")]
    [InlineData("var s = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator", "System.Globalization.CultureInfo.CurrentCulture")]
    [InlineData("var s = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator", "System.Globalization.CultureInfo.CurrentUICulture")]
    // A culture's number format crosses, but not the arrays .NET hands out as copies (#1765).
    [InlineData("var s = System.Globalization.NumberFormatInfo.CurrentInfo.NumberGroupSizes", "System.Globalization.NumberFormatInfo.NumberGroupSizes")]
    [InlineData("var s = System.Globalization.NumberFormatInfo.CurrentInfo.NaNSymbol", "System.Globalization.NumberFormatInfo.NaNSymbol")]
    [InlineData("var p = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.ShortDatePattern", "System.Globalization.CultureInfo.InvariantCulture")]
    [InlineData("var n = System.Globalization.CultureInfo.CurrentCulture?.Name", "System.Globalization.CultureInfo.CurrentCulture")]
    [InlineData("var line = Environment.NewLine", "System.Environment.NewLine")]
    [InlineData("var zone = TimeZoneInfo.Local", "System.TimeZoneInfo.Local")]
    [InlineData("var name = typeof(string).Name", "System.Reflection.MemberInfo.Name")]
    [InlineData("var rank = new int[2].Rank", "System.Array.Rank")]
    [InlineData("var capacity = new List<int>().Capacity", "System.Collections.Generic.List<int>.Capacity")]
    // After a `?.`, and with a second one behind it: `a?.B?.C` is `a?.(.B?.C)`.
    [InlineData("var lines = new Exception(\"m\")?.StackTrace?.Split('\\n')", "System.Exception.StackTrace")]
    [InlineData("var name = new Exception(\"m\")?.TargetSite?.Name", "System.Exception.TargetSite")]
    // On a receiver a strategy translated, which no fence refused: an indexer and a nullable's Value.
    [InlineData("var trace = new List<Exception>()[0].StackTrace", "System.Exception.StackTrace")]
    [InlineData("var version = ((Guid?)Guid.Empty).Value.Version", "System.Guid.Version")]
    public void APlatformMemberNothingCarries_FailsTheBuild_NamingIt(string code, string member)
    {
        var refused = TestHelper.DiagnosticsFor(code).Should().ContainSingle().Which;
        refused.Code.Should().Be("EQ2004");
        refused.Message.Should().StartWith($"'{member}' has no JavaScript translation");
    }

    /// <summary>
    /// What the browser holds carries these by their own name: a string's and an array's
    /// <c>length</c>, what an exception's twin was handed, a decimal's scale, a dictionary's pair, and
    /// a member an interface declares, which a type of the app's own may implement. A culture handed
    /// to a call that reads it from its spelling is never read as a value.
    /// </summary>
    [Theory]
    [InlineData("var n = \"ab\".Length")]
    [InlineData("var n = new int[2].Length")]
    [InlineData("var m = new InvalidOperationException(\"m\").Message")]
    [InlineData("var p = new ArgumentNullException(\"p\").ParamName")]
    [InlineData("var e = new Exception(\"m\", new Exception(\"inner\")).InnerException")]
    [InlineData("var k = dict.First().Key")]
    [InlineData("var v = dict.First().Value")]
    [InlineData("var k = items.GroupBy(x => x.Length).First().Key")]
    [InlineData("var s = 1.50m.Scale")]
    [InlineData("var c = char.ToUpper('i', System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("var c = char.ToLower(culture: System.Globalization.CultureInfo.CurrentCulture, c: 'I')")]
    [InlineData("var n = item.Name")]
    // A culture's number format, the page's and the invariant one's, by both doors (#1765).
    [InlineData("var p = System.Globalization.NumberFormatInfo.CurrentInfo.CurrencyPositivePattern")]
    [InlineData("var s = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator")]
    [InlineData("var s = System.Globalization.NumberFormatInfo.InvariantInfo.CurrencySymbol")]
    [InlineData("var s = System.Globalization.CultureInfo.InvariantCulture.NumberFormat.NegativeSign")]
    [InlineData("var f = System.Globalization.NumberFormatInfo.CurrentInfo; var s = f.PercentSymbol + f.PerMilleSymbol")]
    public void AMemberTheBrowserCarries_Builds(string code)
    {
        TestHelper.DiagnosticsFor(code).Should().BeEmpty();
    }

    /// <summary>A culture's number format is the one every formatter of the page reads: it is read in the
    /// browser and never written, by an assignment, a compound one or an increment (#1765).</summary>
    [Theory]
    [InlineData("System.Globalization.NumberFormatInfo.CurrentInfo.CurrencySymbol = \"x\"", "CurrencySymbol")]
    [InlineData("System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign += \"x\"", "NegativeSign")]
    [InlineData("System.Globalization.NumberFormatInfo.CurrentInfo.CurrencyDecimalDigits++", "CurrencyDecimalDigits")]
    public void ANumberFormatsMember_IsNeverWritten(string code, string member)
    {
        var refused = TestHelper.DiagnosticsFor(code).Should().ContainSingle().Which;
        refused.Code.Should().Be("EQ2004");
        refused.Message.Should().Be($"'System.Globalization.NumberFormatInfo.{member}' is read in the browser and never written: "
            + "the culture's number format is the object every formatter of the page reads.");
    }

    /// <summary>The read writes the object the culture travels as, the page's or the invariant one's.</summary>
    [Theory]
    [InlineData("var p = System.Globalization.NumberFormatInfo.CurrentInfo.CurrencyPositivePattern", "$eq.text.numberFormat(false).currencyPositivePattern")]
    [InlineData("var p = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyPositivePattern", "$eq.text.numberFormat(false).currencyPositivePattern")]
    [InlineData("var p = System.Globalization.CultureInfo.InvariantCulture.NumberFormat.CurrencySymbol", "$eq.text.numberFormat(true).currencySymbol")]
    public void ANumberFormat_IsTheCulturesObject(string code, string emitted)
    {
        TestHelper.ConvertStatement(code + ";").Should().Contain(emitted);
    }
}
