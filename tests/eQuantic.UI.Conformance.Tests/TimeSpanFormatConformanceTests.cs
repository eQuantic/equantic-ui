using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>TimeSpan</c> written with a format, as .NET writes it (#837). The formatter handed a TimeSpan to
/// the twin's own <c>toString</c>, which takes no format, so every format wrote the constant ("c")
/// text: <c>ToString(@"hh\:mm")</c> of an hour and two minutes was <c>01:02:00</c>, and a format .NET
/// refuses wrote a value. Measured on .NET 10: <c>c</c>, <c>t</c> and <c>T</c> are the constant form in
/// every culture; <c>g</c> and <c>G</c> are the general forms, with the culture's decimal separator; a
/// custom format writes its parts token by token, without a sign; and every other format is a
/// FormatException. A format reaches the formatter through <c>ToString</c>, a hole and a composite
/// placeholder alike, and the invariant culture, the current one and a null provider are the cultures
/// they name.
/// </summary>
public class TimeSpanFormatConformanceTests
{
    private const string Spans =
        "var a = new TimeSpan(0, 1, 2, 3, 4); var b = new TimeSpan(1, 2, 3, 4, 500).Negate(); "
        + "var z = TimeSpan.Zero; var big = TimeSpan.MaxValue; var low = TimeSpan.MinValue; "
        + "var tick = new TimeSpan(-5); var day = new TimeSpan(100, 0, 0, 0); var late = new TimeSpan(0, 23, 59, 59); "
        + "var all = new[] { a, b, z, big, low, tick, day, late, TimeSpan.FromTicks(12345678), new TimeSpan(0, 0, 0, 0, 10) }; ";

    /// <summary>Every value written in each format of a list, the format read when the call runs.</summary>
    private static string Each(params string[] formats) =>
        Spans + "var s = \"\"; foreach (var f in new[] { " + string.Join(", ", formats.Select(Literal)) + " }) "
        + "{ s += \"[\" + f + \"]\"; foreach (var v in all) s += v.ToString(f) + \"/\"; } return s;";

    /// <summary>What each format of a list refuses, in .NET's words.</summary>
    private static string Refused(params string[] formats) =>
        Spans + "var s = \"\"; foreach (var f in new[] { " + string.Join(", ", formats.Select(Literal)) + " }) "
        + "{ try { s += \"[\" + f + \"]\" + a.ToString(f); } catch (FormatException e) { s += \"[\" + f + \"]!\" + e.Message; } } return s;";

    private static string Literal(string text) =>
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static readonly string[] Cases =
    [
        // The standard formats: the constant one under its three names, and the two general ones.
        Each("c", "t", "T", "g", "G"),
        // A null and an empty format are the constant one.
        Spans + "string none = null; return a.ToString(none) + \"|\" + b.ToString(\"\") + \"|\" + a.ToString((string)null);",
        // Custom formats: each part padded to its width, a day's up to eight digits, a fraction cut to
        // its width or its trailing zeros dropped, and literal text quoted, escaped or after a %.
        Each(@"hh\:mm", "%h", @"dd\.hh\:mm\:ss", "%d", "dd", "ddd", "dddddddd", "hh", "mm", "ss"),
        Each("fff", "FFF", "%F", "%f", "FF", "ffffff", "fffffff", "FFFFFFF", @"ss\.FFFFFFF", @"ff\FF"),
        Each("'lit'mm", "\"lit\"%m", "mm' min'", @"h\h", "d'.'hh", @"\-hh", @"'a\'b'mm", "\"a\\\"b\"mm", @"\'mm",
            "%h%m", @"%s\s", @"dddddddd\.hh", "'%'hh"),
        // What .NET refuses: a standard letter it does not have, a custom letter it does not have or
        // repeated past its width, a separator nobody escaped, and a quote or an escape left open.
        Refused("C", "d", "D", "h", "m", "s", "f", "F", "q", " ", "%", "%%", "%c", @"\", @"hh\", "hh:mm", "hhh", "mmm",
            "sss", "ffffffff", "FFFFFFFF", "ddddddddd", @"%\h", @"%\"),
        Refused("'abc", "%'", "%\"", @"'mm'\", "\"mm"),
        // The format reaches the formatter through a hole and a composite placeholder as well.
        Spans + "return $\"{a:g}|{b:G}|{a:hh\\\\:mm}|{b,12:c}|{a,-10:%h}|\" + string.Format(\"{0:g}|{1:dd\\\\.hh}\", b, a);",
        // A provider: the invariant culture, the current one and a null are the cultures they name.
        Spans + "return b.ToString(\"g\", System.Globalization.CultureInfo.InvariantCulture) + \"|\" "
            + "+ b.ToString(\"G\", System.Globalization.CultureInfo.CurrentCulture) + \"|\" "
            + "+ b.ToString(\"g\", null) + \"|\" + a.ToString(@\"hh\\:mm\", System.Globalization.CultureInfo.InvariantCulture) + \"|\" "
            + "+ a.ToString(null, System.Globalization.CultureInfo.InvariantCulture);",
        // A null TimeSpan writes nothing, its ToString with no format included, which read it through null.
        "TimeSpan? n = null; TimeSpan? m = new TimeSpan(1, 2, 3); "
            + "return $\"[{n:g}]|[{m:g}]|[{n?.ToString(\"G\")}]|[{n.ToString()}]|{m.ToString()}|\" + m?.ToString(\"G\");",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements =>
            NumberTextInTheCultureConformanceTests.Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ATimeSpanWithAFormat_WritesWhatDotNetWrites(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
