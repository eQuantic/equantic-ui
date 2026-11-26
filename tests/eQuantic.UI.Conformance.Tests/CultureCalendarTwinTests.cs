using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>CultureCalendar</c> counts a day in the culture's calendar on both sides (#1617): the vocabulary on
/// .NET reads <c>DateTimeFormatInfo.Calendar</c>, and its twin reads the calendar the culture's data
/// carries to the browser. A month grid counts its month through it, so a twin that counted the Gregorian
/// calendar, or kept a day the target month lacks, would show another month than the server's.
/// </summary>
public class CultureCalendarTwinTests
{
    /// <summary>Each day's year, month and day, its month's first day and its month's length, written as
    /// numbers both sides write alike, Um Al Qura's first and last days among them.</summary>
    private const string Counted =
        "string Count(DateOnly d) => CultureCalendar.YearOf(d) + \"-\" + CultureCalendar.MonthOf(d) + \"-\" "
        + "+ CultureCalendar.DayOf(d) + \" first \" + CultureCalendar.FirstOfMonth(d).DayNumber + \" days \" + CultureCalendar.DaysInMonth(d); "
        + "var s = \"\"; foreach (var d in new[] { new DateOnly(2024, 1, 17), new DateOnly(2023, 12, 22), "
        + "new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21), new DateOnly(2024, 3, 19), new DateOnly(2024, 3, 20), "
        + "new DateOnly(2024, 2, 29), new DateOnly(2025, 3, 20), new DateOnly(2024, 12, 31), new DateOnly(1900, 4, 30), "
        + "new DateOnly(2077, 11, 16) }) s += Count(d) + \"|\"; return s;";

    /// <summary>Moves by months and years from days a shorter target month cuts, both ways: the 30th of
    /// Esfand 1403, the 31st of January, the 29th of February, and the 30th of Ramadan 1445 (April 9th
    /// 2024), which Um Al Qura's months of 29 days cut.</summary>
    private const string Moved =
        "string Day(DateOnly d) => d.DayNumber.ToString(); var s = \"\"; "
        + "foreach (var d in new[] { new DateOnly(2024, 1, 17), new DateOnly(2025, 3, 20), new DateOnly(2024, 3, 19), "
        + "new DateOnly(2024, 1, 31), new DateOnly(2024, 2, 29), new DateOnly(2024, 8, 21), new DateOnly(2024, 4, 9) }) "
        + "foreach (var n in new[] { 1, -1, 2, 11, 12, -13 }) "
        + "s += Day(CultureCalendar.AddMonths(d, n)) + \",\" + Day(CultureCalendar.AddYears(d, n)) + \"|\"; return s;";

    [SkippableTheory]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("en-US")]
    public void ADay_IsCountedInTheCulturesCalendar(string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(Counted, culture);
    }

    [SkippableTheory]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("en-US")]
    public void AMove_KeepsTheDayAndCutsItToTheMonthsLast(string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(Moved, culture);
    }

    /// <summary>A move past the months the calendar counts is refused in its words (#1849): Um Al Qura's
    /// years, the Persian calendar's years and its last year's tenth month, and a Gregorian or Thai date a
    /// DateTime cannot hold. Its first and last days are its MinSupportedDateTime's and MaxSupportedDateTime's.</summary>
    [SkippableTheory]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("en-US")]
    public void AMovePastTheCalendarsRange_IsRefusedInItsWords(string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNetExceptTheHostsNewline(
            "string Try(Func<DateOnly> f) { try { return f().DayNumber.ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; } } "
            + "var first = CultureCalendar.FirstDay; var last = CultureCalendar.LastDay; "
            + "return first.DayNumber + \"|\" + last.DayNumber + \"|\" + Try(() => CultureCalendar.AddMonths(last, 1)) + \"|\" "
            + "+ Try(() => CultureCalendar.AddMonths(first, -1)) + \"|\" + Try(() => CultureCalendar.AddYears(last, 1)) + \"|\" "
            + "+ Try(() => CultureCalendar.AddYears(first, -1)) + \"|\" + Try(() => CultureCalendar.AddMonths(last, -1));",
            "an ArgumentOutOfRangeException writes its actual value after the host's newline", culture);
    }

    /// <summary>A day the calendar does not write is refused in its words: ar-SA's Um Al Qura calendar
    /// writes from 1900 to 2077 (and Linux's ar-SA, whose ICU counts it Gregorian, answers the year on both
    /// sides).</summary>
    [SkippableFact]
    public void ADayTheCalendarDoesNotWrite_IsRefusedInItsWords()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNetExceptTheHostsNewline(
            "var s = \"\"; foreach (var d in new[] { new DateOnly(2080, 1, 1), new DateOnly(1899, 1, 1) }) "
            + "{ try { s += CultureCalendar.YearOf(d) + \"|\"; } catch (ArgumentOutOfRangeException e) { s += e.Message + \"|\"; } } return s;",
            "an ArgumentOutOfRangeException writes its actual value after the host's newline", "ar-SA");
    }
}
