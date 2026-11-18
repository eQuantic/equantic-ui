using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A date written and read in the calendar of its culture, as .NET 10 counts it (#1191). Three cultures
/// .NET ships count their dates in a calendar that is not the Gregorian one, each of a different shape:
/// th-TH in the Thai Buddhist years, 543 ahead of the Gregorian ones; fa-IR in the Persian calendar, whose
/// leap years .NET computes from the equinox; and ar-SA in the Um Al Qura calendar, whose months come from
/// a table of observations and which writes nothing before 1900 or after 2077. The browser wrote the
/// Gregorian numbers under those calendars' names and read <c>15/1/2567</c> as the year 2567. en-US runs
/// beside them, the Gregorian calendar the change must leave as it was.
/// </summary>
public class CultureCalendarConformanceTests
{
    public static readonly string[] Cultures = ["th-TH", "fa-IR", "ar-SA", "en-US"];

    /// <summary>What a reader answers for each text of a list: the value in the round trip form, which
    /// no calendar changes, or <c>no</c>.</summary>
    private static string Each(string read, params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "s += (" + read + " ? v.ToString(\"o\") : \"no\") + \"|\"; return s;";

    /// <summary>What <c>DateTime.Parse</c> refuses each text of a list with, in .NET's words.</summary>
    private static string Refused(params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "{ try { DateTime.Parse(t); s += \"read|\"; } catch (FormatException e) { s += e.Message + \"|\"; } } return s;";

    /// <summary>A C# string literal of a text, every character outside printable ASCII escaped.</summary>
    private static string Literal(string text) =>
        "\"" + string.Concat(text.Select(c => c switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            < ' ' or > '~' => $"\\u{(int)c:x4}",
            _ => c.ToString(),
        })) + "\"";

    private const string Moment = "var d = new DateTime(2024, 1, 15, 10, 30, 5); ";

    private static readonly string[] Writes =
    [
        // Every standard specifier and the parts of a picture: the year, the month and the day are the
        // calendar's, and so are the names of its months; the round trip and sortable forms are not.
        Moment + "var s = \"\"; foreach (var f in new[] { \"d\", \"D\", \"f\", \"G\", \"g\", \"M\", \"Y\", \"yyyy-MM-dd\", \"yy\", \"%y\", \"yyyyy\", \"MMMM\", \"MMM\", \"%M\", \"%d\", \"dddd\", \"gg\", \"o\", \"s\", \"u\", \"r\" }) s += d.ToString(f) + \"|\"; return s;",
        // The text of each type with no format, interpolated and composed.
        Moment + "return d.ToString() + \"|\" + d + \"|\" + $\"{d:d}\" + \"|\" + string.Format(\"{0:D}\", d) + \"|\" + new DateOnly(2024, 1, 15) + \"|\" + new DateTimeOffset(d, TimeSpan.FromHours(3)).ToString(\"d\");",
        // The days either side of a year's start and of a month's end in each calendar: Nowruz, the 30th
        // of Esfand of a Persian leap year, the end of Safar 1445, a Gregorian leap day, and the first and
        // last day each calendar writes.
        "var s = \"\"; foreach (var day in new[] { new DateTime(2024, 3, 19), new DateTime(2024, 3, 20), new DateTime(2024, 3, 21), new DateTime(2025, 3, 20), new DateTime(2025, 3, 21), new DateTime(2023, 9, 15), new DateTime(2023, 9, 16), new DateTime(2024, 2, 29), new DateTime(1900, 4, 29), new DateTime(1900, 4, 30), new DateTime(2077, 11, 16), new DateTime(2077, 11, 17), new DateTime(622, 3, 21), new DateTime(622, 3, 22), new DateTime(1, 1, 1), new DateTime(9999, 12, 31) }) "
            + "{ try { s += day.ToString(\"yyyy/MM/dd\") + \"|\"; } catch (ArgumentOutOfRangeException) { s += \"out|\"; } } return s;",
        // A day the calendar does not write is refused in the calendar's own words, where it has one.
        "try { return DateTime.MinValue.ToString(\"d\"); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
        "try { return new DateTime(2100, 1, 1).ToString(\"D\"); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
        "try { return DateOnly.MinValue.ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
        "try { return DateTime.MinValue.ToString(\"MMMM\"); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
        "try { return DateTime.MinValue.ToString(\"gg\"); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
        // A time of day held in a DateTime of the first day: its text with no format is the invariant
        // sortable form where the calendar cannot write the year 1, and a part that names no date is
        // written in every calendar.
        "var t = new DateTime(1, 1, 1, 10, 30, 0); return t.ToString() + \"|\" + default(DateTime) + \"|\" + new DateTimeOffset(t, TimeSpan.FromHours(-3)) + \"|\" + t.ToString(\"t\") + \"|\" + DateTime.MinValue.ToString(\"dddd HH:mm\") + \"|\" + new TimeOnly(10, 30);",
        "try { return new DateTime(1, 1, 1, 10, 30, 0).ToString(\"G\"); } catch (ArgumentOutOfRangeException e) { return e.Message; }",
    ];

    private static readonly string[] Reads =
    [
        // A date of three numbers in each calendar's count, and the years each calendar writes first and last.
        Each("DateTime.TryParse(t, out var v)",
            "15/1/2567", "2567/1/15", "15/1/2024", "1402/10/25", "1402/12/29", "1402/12/30", "1403/12/30", "1404/1/1",
            "1445/7/4", "4/7/1445", "30/2/1445", "30/1/1445", "29/12/1403", "30/12/1403", "1/1/1318", "30/12/1500",
            "1/1/1501", "1/1/1317", "1/1/543", "1/1/544", "1/1/1", "1/1/10542", "31/12/10542", "1/1/10543"),
        // A year of two digits, which each calendar's TwoDigitYearMax puts in its century.
        Each("DateTime.TryParse(t, out var v)", "15/1/67", "15/1/45", "15/1/02", "15/1/49", "15/1/50", "15/1/72", "15/1/73"),
        // The ISO form is the Gregorian calendar's in every culture, and only with its `T`.
        Each("DateTime.TryParse(t, out var v)",
            "2024-01-15", "2024/01/15", "2024-01-15 10:30:05", "2024-01-15T10:30:00", "2024-01-15T10:30:05Z",
            "2567-01-15T10:30:00", "1445-02-30T10:00", "Monday 2024-01-15T10:00", "15 January 2567", "January 2567"),
        // A date of two numbers is of this year as the culture's calendar counts it.
        Each("DateTime.TryParse(t, out var v)", "15/1", "1/5", "1/15", "12/30"),
        // What the culture writes reads back, its long forms where .NET reads them.
        Moment + "var s = \"\"; foreach (var f in new[] { \"d\", \"G\", \"g\", \"D\", \"f\", \"M\", \"Y\" }) s += (DateTime.TryParse(d.ToString(f), out var v) ? v.ToString(\"o\") : \"no\") + \"|\"; return s;",
        // A DateOnly and a DateTimeOffset read the culture's calendar too.
        "var day = DateOnly.TryParse(\"1402/10/25\", out var o); var other = DateOnly.TryParse(\"15/1/2567\", out var p); "
            + "var at = DateTimeOffset.TryParse(\"4/7/1445 10:00 +03:00\", out var f); "
            + "return day + \" \" + o.ToString(\"o\") + \"|\" + other + \" \" + p.ToString(\"o\") + \"|\" + at + \" \" + f.ToString(\"o\");",
        // A clock that does not exist is refused in the name of the culture's calendar, the ISO form's
        // clock among them.
        Refused("15/1/2567 25:00", "1402/10/25 25:00", "4/7/1445 25:00", "2024-01-15T25:00", "1/1/2024 10:61"),
    ];

    public static TheoryData<string, string> WriteCases() => Each(Writes);

    public static TheoryData<string, string> ReadCases() => Each(Reads);

    private static TheoryData<string, string> Each(string[] statements)
    {
        var data = new TheoryData<string, string>();
        foreach (var culture in Cultures)
            foreach (var statement in statements)
                data.Add(statement, culture);
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(WriteCases))]
    public void ADate_IsWrittenInTheCalendarOfItsCulture(string statements, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }

    [SkippableTheory]
    [MemberData(nameof(ReadCases))]
    public void ADate_IsReadInTheCalendarOfItsCulture(string statements, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
