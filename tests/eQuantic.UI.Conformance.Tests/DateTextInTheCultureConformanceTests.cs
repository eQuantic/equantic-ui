using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A date, a time and a time span read from text in the culture the call names, as .NET 10 reads them
/// (#806, #717), and <c>Convert.ToDateTime</c>, which reads a text as <c>Parse</c> does (#844).
/// <c>Parse</c> and <c>TryParse</c> with no provider read the current culture, and the twins read the
/// invariant shapes whatever culture the page was in: <c>01/02/2024</c> was the 2nd of January on a
/// pt-BR page where .NET reads the 1st of February, a value written with <c>ToString()</c> did not read
/// back, and a provider crossed as a name no browser defines. Measured on .NET 10 in nine cultures that
/// each read a date their own way: month first, day first with a slash and with a point, year first
/// with a hyphen, with a slash and with a point and a space, an AM or PM designator of the culture's
/// own before or after the clock, a point between the hours and the minutes, and marks of direction
/// between the parts.
/// </summary>
public class DateTextInTheCultureConformanceTests
{
    /// <summary>The cultures every case runs in: the invariant one, then month first (en-US), day
    /// first with a slash (pt-BR), with a point (de-DE) and with marks of direction and words for AM
    /// and PM (ar), year first with a hyphen (sv-SE), with a slash (ja-JP) and with a point and a space
    /// and the designator before the clock (ko-KR), and a point between the parts of a clock (fi-FI).</summary>
    public static readonly string?[] Cultures = [null, "en-US", "pt-BR", "de-DE", "sv-SE", "ar", "fi-FI", "ja-JP", "ko-KR"];

    /// <summary>What a reader answers for each text of a list: the value written in a form no culture
    /// changes, or <c>no</c>.</summary>
    private static string Each(string read, string written, params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "s += (" + read + " ? " + written + " : \"no\") + \"|\"; return s;";

    private static string DateTimes(params string[] texts) =>
        Each("DateTime.TryParse(t, out var v)", "v.ToString(\"o\")", texts);

    private static string Offsets(params string[] texts) =>
        Each("DateTimeOffset.TryParse(t, out var v)", "v.ToString(\"o\")", texts);

    /// <summary>What a reader's Parse refuses each text of a list with, in .NET's words.</summary>
    private static string Refused(string parse, params string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(Literal)) + " }) "
        + "{ try { " + parse + "(t); s += \"read|\"; } catch (FormatException e) { s += e.Message + \"|\"; } } return s;";

    /// <summary>A C# string literal of a text, every character outside printable ASCII escaped, so a
    /// mark of direction is seen where it is.</summary>
    private static string Literal(string text) =>
        "\"" + string.Concat(text.Select(c => c switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            < ' ' or > '~' => $"\\u{(int)c:x4}",
            _ => c.ToString(),
        })) + "\"";

    private const string Invariant = "System.Globalization.CultureInfo.InvariantCulture";

    private static readonly string[] Cases =
    [
        // What the culture writes reads back: a date and time, without its seconds, a date alone, and
        // one whose day and month could be either, with the morning's designator.
        "var s = \"\"; foreach (var x in new[] { new DateTime(2024, 1, 15, 21, 30, 5), new DateTime(2024, 11, 5, 9, 5, 0) }) "
            + "foreach (var f in new[] { \"G\", \"g\", \"d\" }) s += (DateTime.TryParse(x.ToString(f), out var b) ? b.ToString(\"o\") : \"no\") + \"|\"; "
            + "return s + DateTime.Parse(new DateTime(2024, 11, 5, 9, 5, 7).ToString()).ToString(\"o\");",
        // And so does every other type's text.
        "var d = new DateOnly(2024, 11, 5); var t = new TimeOnly(21, 30, 5); var m = new TimeOnly(9, 5); "
            + "var o = new DateTimeOffset(2024, 11, 5, 21, 30, 5, TimeSpan.FromHours(-3)); var p = new DateTimeOffset(2024, 1, 15, 9, 5, 0, new TimeSpan(5, 30, 0)); "
            + "var span = new TimeSpan(0, 1, 2, 3, 500); "
            + "return DateOnly.Parse(d.ToString()).ToString(\"o\") + \"|\" + TimeOnly.Parse(t.ToString(\"T\")).ToString(\"o\") + \"|\" "
            + "+ TimeOnly.Parse(m.ToString()).ToString(\"o\") + \"|\" + DateTimeOffset.Parse(o.ToString()).ToString(\"o\") + \"|\" "
            + "+ DateTimeOffset.Parse(p.ToString()).ToString(\"o\") + \"|\" + TimeSpan.Parse(span.ToString(\"g\")).ToString(\"c\") + \"|\" "
            + "+ TimeSpan.Parse(span.Negate().ToString(\"g\")).ToString(\"c\") + \"|\" + TimeSpan.Parse(span.ToString()).ToString(\"c\");",
        // Which number is the day, the month and the year: the culture's order, a year of three digits
        // or more wherever it is, a year of two read against 2049, and a date that does not exist.
        DateTimes("01/02/2024", "15/01/2024", "1/15/2024", "15.01.2024", "15-01-2024", "2024-01-15", "2024/01/15",
            "2024.01.15", "24/01/15", "01/02/24", "15/1/24", "1/2/3", "99/1/1", "100/1/1", "1/1/100", "1/1/0", "1/1/00",
            "31/02/2024", "2/31/2024", "15/01/224", "015/01/2024"),
        // What may stand between the parts of a date: any of the separators, white space beside them,
        // and a mark of right-to-left direction after a number, which ar writes; not one before the
        // date, nor a mark of left-to-right direction.
        DateTimes("15//01/2024", "2024/01-15", "15/01-2024", "2024 / 01 / 15", "15‏/1‏/2024",
            "15‏‏/1‏‏/2024", "15/1/2024‏", "‏15/1/2024", "1‎/15‎/2024", "  15/01/2024  "),
        // A clock after the date: one or two digits a part, a fraction after the seconds alone, written
        // with a point or a comma, and rounded to the tick, the hours alone only with a designator.
        DateTimes("2024/01/15 21:30", "2024/01/15 21:30:05.5", "2024/01/15 21:30:05,5", "2024/01/15 21:30:05.123456789",
            "2024/01/15 21:30,5", "2024/01/15 21", "2024/01/15 21:3", "2024/01/15 21:30:5", "2024/01/15 021:30",
            "2024/01/15 21:030", "2024/01/15 24:00", "2024/01/15 23:60", "2024/01/15 21 :30", "2024/01/15 21: 30",
            "2024/01/15 21:30", "2024/01/15 21:30", "2024/01/15\t21:30", "2024/01/15  21:30", "2024-01-15T21:30:05"),
        // The designators: AM and PM in any culture, in any case, before or after the clock, and the
        // culture's own, which no other culture reads.
        DateTimes("2024/01/15 9:30 PM", "2024/01/15 9:30PM", "2024/01/15 9:30 pm", "2024/01/15 PM 9:30", "2024/01/15 9 PM",
            "2024/01/15 9:30:05.5 PM", "2024/01/15 12:30 AM", "2024/01/15 13:30 PM", "2024/01/15 13:30 AM", "2024/01/15 0:30 AM",
            "2024/01/15 9:30 PM", "2024/01/15 9:30 a.m.", "2024/01/15 9:30 P M"),
        DateTimes("2024/01/15 9:30 fm", "2024/01/15 9:30 em", "2024/01/15 9:30 午後", "2024/01/15 午後 9:30",
            "2024/01/15 오후 9:30", "2024/01/15 9:30 ص", "2024/01/15 9:30 م", "2024/01/15 9:30 ap.", "2024/01/15 9:30 ip."),
        // A point between the parts of a clock, which only a culture that writes one reads.
        DateTimes("2024/01/15 21.30", "2024/01/15 21.30.05", "2024/01/15 21:30.05", "2024/01/15 21.30,5",
            "2024/01/15 21.30.05,5", "2024/01/15 21.30.05.5", "15.1.2024 21.30.05"),
        // A zone after it: a DateTime is moved to the local time, and a DateTimeOffset keeps it, or takes
        // the local one where the text has none.
        DateTimes("2024/01/15 21:30:05 -03:00", "2024/01/15 21:30:05 +0530", "2024/01/15 21:30:05Z", "2024/01/15 21:30 -3",
            "2024/01/15 21:30:05 +14:00", "2024/01/15 21:30:05 +14:01", "2024-01-15T21:30:05-03:00", "2024/01/15 9:30:05 PM -03:00",
            "2024/01/15 21:30:05 +123", "2024/01/15 21:30:05 -1401", "2024/01/15 21:30:05 +12345"),
        Offsets("2024/01/15 21:30:05 -03:00", "2024/01/15 21:30:05 +0530", "2024/01/15 21:30:05Z", "2024/01/15 21:30 -3",
            "2024/01/15", "2024/01/15 21:30", "2024/01/15 -03:00", "2024-01-15T21:30:05-03:00", "2024/01/15 9:30:05 PM -03:00",
            "2024/01/15 21:30 +14:01", "15/01/2024 21:30:05 -03:00", "2024/01/15 21:30 +123", "2024/01/15 21:30 -930"),
        // A time of day alone, and a date alone.
        Each("TimeOnly.TryParse(t, out var v)", "v.ToString(\"o\")", "21:30", "9:30 PM", "PM 9:30", "9 PM", "21", "21:30:05,5",
            "21:30:05.5", "21.30", "21.30.05", "21.30.05,5", "9:30 fm", "9:30 em", "9:30 午後", "午後 9:30",
            "오후 9:30", "9:30 ص", "9:30 م", "9:30 ap.", "9:30 ip.", "9:30 PM", " 21:30 ", "24:00",
            "2024/01/15 21:30", "21:30 -03:00", "21:30Z"),
        Each("DateOnly.TryParse(t, out var v)", "v.ToString(\"o\")", "2024/01/15", "15/01/2024", "1/15/2024", "15.1.2024",
            "2024/01/15 21:30", "2024/01/15 00:00", "15‏‏/1‏‏/2024", "2024/01/15 -03:00"),
        // A time span's fraction: a point in any culture, and the culture's own separator where the
        // span has no days.
        Each("TimeSpan.TryParse(t, out var v)", "v.ToString(\"c\")", "1:02:03.5", "1:02:03,5", "1:02:03٫5",
            "1.02:03:04.5", "1.02:03:04,5", "-1:02:03,5", "01:02:03,5000000", " 1:02:03,5 "),
        // What each Parse refuses, in .NET's words.
        Refused("DateTime.Parse", "15//01/2024", "2024/01/15 24:00", "2024/01/15 23:60", "2024/01/15 13:30 AM", "abc",
            "2024/01/15 21:30 junk", "2024/01/15 21:30:05 +14:01", "", "  ", "2024/01/15 021:30", "10000/01/01", "2024/13/01",
            "2024/01/15 21", "2024/02/31"),
        Refused("DateTimeOffset.Parse", "abc", "2024/02/31", "2024/01/15 24:00", "2024/01/15 21:30 +15:00", "2024/01/15 21:30 junk"),
        Refused("TimeOnly.Parse", "24:00", "2024/01/15 21:30", "21:30 -03:00", "13:30 AM", "abc", "21", "21:30Z", "9:30 PM junk"),
        Refused("DateOnly.Parse", "2024/01/15 21:30", "2024/02/31", "abc", "2024/01/15 -03:00", "2024/01/15Z"),
        Refused("TimeSpan.Parse", "1.02:03:04,5", "abc"),
        // The invariant culture named reads its own shapes on any page, and the current one and a null
        // read the page's. (Named where it is used: a culture read from a variable is EQ2108, which the
        // harness, running what it transpiles, would not report. A constant's styles are read.)
        "const System.Globalization.DateTimeStyles none = System.Globalization.DateTimeStyles.None; "
            + "var ok = DateTime.TryParse(\"15/01/2024\", " + Invariant + ", none, out var no); "
            + "return DateTime.Parse(\"01/02/2024\", " + Invariant + ").ToString(\"o\") + \"|\" + ok + \"|\" "
            + "+ DateTime.Parse(\"01/02/2024\", System.Globalization.CultureInfo.CurrentCulture).ToString(\"o\") + \"|\" "
            + "+ DateTime.Parse(\"01/02/2024\", null).ToString(\"o\") + \"|\" "
            + "+ DateTime.Parse(\" 01/02/2024 9:30 PM \", " + Invariant + ", System.Globalization.DateTimeStyles.AllowWhiteSpaces).ToString(\"o\") + \"|\" "
            + "+ DateTime.Parse(new DateTime(2024, 11, 5, 9, 5, 7).ToString(" + Invariant + "), " + Invariant + ").ToString(\"o\");",
        "const System.Globalization.DateTimeStyles none = System.Globalization.DateTimeStyles.None; "
            + "var a = DateOnly.TryParse(\"01/02/2024\", " + Invariant + ", none, out var d); var b = DateOnly.TryParse(\"01/02/2024\", " + Invariant + ", out var e); "
            + "var c = TimeOnly.TryParse(\"9:30 em\", " + Invariant + ", none, out var t); var f = TimeSpan.TryParse(\"1:02:03,5\", " + Invariant + ", out var span); "
            + "var g = DateTimeOffset.TryParse(\"01/02/2024 9:30 PM -03:00\", " + Invariant + ", none, out var o); "
            + "return a + d.ToString(\"o\") + \"|\" + b + e.ToString(\"o\") + \"|\" + c + t.ToString(\"o\") + \"|\" + f + span.ToString(\"c\") + \"|\" "
            + "+ g + o.ToString(\"o\") + \"|\" + DateOnly.Parse(\"01/02/2024\", " + Invariant + ").ToString(\"o\") + \"|\" "
            + "+ TimeOnly.Parse(\"9:30 PM\", " + Invariant + ", none).ToString(\"o\") + \"|\" + TimeSpan.Parse(\"1:02:03.5\", " + Invariant + ").ToString(\"c\") + \"|\" "
            + "+ DateTimeOffset.Parse(\"01/02/2024 21:30\", null, none).ToString(\"o\") + \"|\" "
            + "+ DateOnly.Parse(\"01/02/2024\", System.Globalization.CultureInfo.CurrentCulture, none).ToString(\"o\");",
        // Convert.ToDateTime reads a text as Parse does, a null one as MinValue, an object by what it
        // holds, and a value of a type with no conversion to a date not at all (#844).
        "string? none = null; object text = \"01/02/2024\"; object date = new DateTime(2024, 1, 2, 3, 4, 5); object nothing = null; "
            + "var s = Convert.ToDateTime(\"01/02/2024\").ToString(\"o\") + \"|\" + Convert.ToDateTime(\"01/02/2024\", " + Invariant + ").ToString(\"o\") + \"|\" "
            + "+ Convert.ToDateTime(\"01/02/2024\", null).ToString(\"o\") + \"|\" + Convert.ToDateTime(none).ToString(\"o\") + \"|\" "
            + "+ Convert.ToDateTime(none, " + Invariant + ").ToString(\"o\") + \"|\" + Convert.ToDateTime(text).ToString(\"o\") + \"|\" "
            + "+ Convert.ToDateTime(text, " + Invariant + ").ToString(\"o\") + \"|\" + Convert.ToDateTime(date).ToString(\"o\") + \"|\" "
            + "+ Convert.ToDateTime(nothing).ToString(\"o\") + \"|\" + Convert.ToDateTime(new DateTime(2024, 1, 2, 3, 4, 5)).ToString(\"o\") + \"|\"; "
            + "try { Convert.ToDateTime(1); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime(true); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime('a'); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime(1.5m); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime(2L); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime((object)true); } catch (InvalidCastException e) { s += e.Message + \"|\"; } "
            + "try { Convert.ToDateTime(\"abc\"); } catch (FormatException e) { s += e.Message; } "
            + "return s;",
        // An argument bound by its name is the one it names, and they run in the order they are written.
        "var log = \"\"; string S() { log += \"s\"; return \"01/02/2024\"; } "
            + "var ok = DateTime.TryParse(result: out var d, styles: System.Globalization.DateTimeStyles.None, "
            + "provider: System.Globalization.CultureInfo.InvariantCulture, s: S()); "
            + "return log + ok + d.ToString(\"o\") + \"|\" + DateTime.Parse(provider: null, s: \"01/02/2024\").ToString(\"o\");",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ADateOrATimeReadFromText_ReadsAsDotNetReadsIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
