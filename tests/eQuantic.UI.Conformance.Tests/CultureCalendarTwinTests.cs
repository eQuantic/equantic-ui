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
    /// <summary>Each day's year, month and day, and its month's first day, written as numbers both sides
    /// write alike.</summary>
    private const string Counted =
        "string Count(DateOnly d) => CultureCalendar.YearOf(d) + \"-\" + CultureCalendar.MonthOf(d) + \"-\" "
        + "+ CultureCalendar.DayOf(d) + \" first \" + CultureCalendar.FirstOfMonth(d).DayNumber; "
        + "var s = \"\"; foreach (var d in new[] { new DateOnly(2024, 1, 17), new DateOnly(2023, 12, 22), "
        + "new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21), new DateOnly(2024, 3, 19), new DateOnly(2024, 3, 20), "
        + "new DateOnly(2024, 2, 29), new DateOnly(2025, 3, 20), new DateOnly(2024, 12, 31) }) s += Count(d) + \"|\"; return s;";

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
}
