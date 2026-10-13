using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A time span read from text with no format, as .NET 10's <c>TimeSpanParse</c> reads one (#857).
/// The twin read one shape, <c>[-][d.]hh:mm:ss[.f]</c>, and checked no part against its bound, so
/// <c>10</c> and <c>1:2</c> were refused, <c>24:00:00</c> was a day where .NET reads twenty-four, and
/// <c>00:60:00</c> was an hour where .NET answers none. .NET reads the invariant pattern and the
/// culture's own, whose days end with a colon and whose fraction starts with the culture's decimal
/// separator, so the cases run in the invariant culture and in three whose separator is a point
/// (en-US), a comma (pt-BR) and the Arabic one (ar).
/// </summary>
public class TimeSpanTextConformanceTests
{
    public static readonly string?[] Cultures = [null, "en-US", "pt-BR", "ar"];

    private const string Invariant = "System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>What <c>TryParse</c> answers for each text of a list: the span in its constant form, or
    /// <c>no</c>.</summary>
    private static string Read(params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "s += (TimeSpan.TryParse(t, out var v) ? v.ToString(\"c\") : \"no\") + \"|\"; return s;";

    /// <summary>What <c>Parse</c> answers for each text of a list: the span, or the exception it throws
    /// in .NET's words, a part past its bound being an overflow and not a format.</summary>
    private static string Parsed(params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "{ try { s += TimeSpan.Parse(t).ToString(\"c\") + \"|\"; } "
        + "catch (FormatException e) { s += \"F \" + e.Message + \"|\"; } "
        + "catch (OverflowException e) { s += \"O \" + e.Message + \"|\"; } } return s;";

    /// <summary>A C# string literal of a text, every character outside printable ASCII escaped.</summary>
    private static string Literal(string text) =>
        "\"" + string.Concat(text.Select(c => c switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            < ' ' or > '~' => $"\\u{(int)c:x4}",
            _ => c.ToString(),
        })) + "\"";

    private static readonly string[] Cases =
    [
        // How many numbers there are decides what they are: days; hours and minutes; then the seconds,
        // the days or a fraction; then two of them; then all five.
        Read("10", "-10", "1:2", "1:2:3", "1.2:3", "1:2:.5", "10:20:.5", "1:2:3:4", "1.2:3:4", "1.2:3:.5",
            "1:2:3.5", "1.2:3:4.5", "-1.2:3:4.5", "1:2:3:4:5", "1:2:3:4:5:6", "1.2.3", "1.2:3:4:5"),
        // The fraction's separator: a point in the invariant pattern, the culture's in its own, whose
        // days end with a colon.
        Read("1:2:3,5", "1:2:3٫5", "1:2:3:4.5", "1:2:3:4,5", "1:2:3:4٫5", "-1:2:3:4,5", "1.2:3:4,5",
            "1:2:3:4:5.6"),
        // White space around the text is trimmed as .NET trims it, and none is read inside it; a sign
        // is a minus, alone, before the first number.
        Read(" 1:2 ", " 1:2 ", "1:2​", "1 :2", "-1:2", "- 1:2", "--1:2", "+1:2", "-0", "-0:0:0",
            "", "   ", "abc", "1:", ":1", ".5", "1:2:3."),
        // Each part against its bound, and a text that only names a span past them is an overflow.
        Parsed("24:00:00", "00:60:00", "0:59", "0:60", "24:0", "23:59:59", "1:2:60", "10675199", "10675200",
            "99999999.00:00:00", "268435455", "268435456", "0000000000001:2", "10675199.23:59:59", "abc"),
        // The range of a TimeSpan, to the tick, at both ends.
        Parsed("10675199.02:48:05.4775807", "10675199.02:48:05.4775808", "-10675199.02:48:05.4775808",
            "-10675199.02:48:05.4775809", "-10675199"),
        // A fraction of more than seven digits is rounded to seven, away from zero, unless its first
        // seven are past what a tick holds.
        Parsed("0:0:0.9999999", "0:0:0.99999995", "0:0:0.09999999", "0:0:0.0123456789", "0:0:0.00000005",
            "0:0:0.000000001", "0:0:0.00000000", "0:0:0.123456789", "1:02:03.12345678", "1:2:3:4.12345678"),
        // Where .NET fails inside its own reader: a fraction whose leading zeros and digits come to
        // fifteen or more, which TryParse throws too.
        "var s = \"\"; foreach (var t in new[] { \"0:0:0.000000123456789\", \"0:0:0.0000000123456789\" }) "
            + "{ try { TimeSpan.Parse(t); s += \"read|\"; } catch (IndexOutOfRangeException e) { s += e.Message + \"|\"; } "
            + "try { TimeSpan.TryParse(t, out var v); s += \"read|\"; } catch (IndexOutOfRangeException) { s += \"thrown|\"; } } return s;",
        // The invariant culture named reads its own pattern on any page, and the page's is the current one.
        "return TimeSpan.Parse(\"1:2:3:4.5\", " + Invariant + ").ToString(\"c\") + \"|\" "
            + "+ TimeSpan.TryParse(\"1:2:3:4,5\", " + Invariant + ", out var a) + \"|\" "
            + "+ TimeSpan.TryParse(\"1:2:3:4,5\", System.Globalization.CultureInfo.CurrentCulture, out var b) + \"|\" + b.ToString(\"c\");",
        // A value the culture writes reads back.
        "var s = \"\"; foreach (var x in new[] { new TimeSpan(1, 2, 3, 4, 500), new TimeSpan(-1, -2, -3, -4, -5), TimeSpan.MaxValue, TimeSpan.MinValue, TimeSpan.Zero }) "
            + "foreach (var f in new[] { \"c\", \"g\", \"G\" }) s += TimeSpan.Parse(x.ToString(f)).ToString(\"c\") + \"|\"; return s;",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ATimeSpanReadFromText_ReadsAsDotNetReadsIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
