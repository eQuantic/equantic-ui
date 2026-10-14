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
    /// <summary>A chain is refused once, at the first member nothing carries, and the message names it.</summary>
    [Theory]
    [InlineData("var n = System.Globalization.CultureInfo.CurrentCulture.Name", "System.Globalization.CultureInfo.CurrentCulture")]
    [InlineData("var s = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator", "System.Globalization.CultureInfo.CurrentCulture")]
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
    public void AMemberTheBrowserCarries_Builds(string code)
    {
        TestHelper.DiagnosticsFor(code).Should().BeEmpty();
    }
}
