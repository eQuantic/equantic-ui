using System.Globalization;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What the active format culture's CALENDAR counts for a day: its year, month and day, its month's first
/// day, and a move by months or years. <see cref="DateOnly"/> always counts the Gregorian calendar, and a
/// culture may count another one: January 17th 2024 is the 27th of Dey 1402 in fa-IR, the 5th of Rajab 1445
/// in ar-SA, and the 17th of January 2567 in th-TH. A month grid counts in this calendar, so that the
/// month it shows is the one <see cref="CalendarNames.MonthNames"/> names (#1617).
/// <para>
/// Resolved at CALL time against <see cref="CultureInfo.CurrentCulture"/>'s
/// <see cref="DateTimeFormatInfo.Calendar"/>, the rule <see cref="CalendarNames"/> follows. The client
/// twin (<c>shared/culture-calendar.ts</c>) counts with the calendar the culture's data carries to the
/// browser, the one the server described, and with the Gregorian one where no data travelled.
/// </para>
/// </summary>
public static class CultureCalendar
{
    /// <summary>The year a day falls in: 1402 for January 17th 2024 in fa-IR, 2567 in th-TH.</summary>
    public static int YearOf(DateOnly day) => Current.GetYear(At(day));

    /// <summary>The month a day falls in, 1 for the calendar's first: 10, Dey, for January 17th 2024 in
    /// fa-IR.</summary>
    public static int MonthOf(DateOnly day) => Current.GetMonth(At(day));

    /// <summary>The day of its month: 27 for January 17th 2024 in fa-IR.</summary>
    public static int DayOf(DateOnly day) => Current.GetDayOfMonth(At(day));

    /// <summary>The first day of the month a day falls in: December 22nd 2023 for January 17th 2024 in
    /// fa-IR.</summary>
    public static DateOnly FirstOfMonth(DateOnly day) => day.AddDays(1 - DayOf(day));

    /// <summary>How many days the month a day falls in has, as <see cref="Calendar.GetDaysInMonth(int, int, int)"/>
    /// answers it in the day's era: 30 for Dey 1402 in fa-IR. A grid counts its month's days with it, never by asking the
    /// month of a day past the month, which a calendar of a bounded range refuses beyond its first and its
    /// last month (Um Al Qura's run from April 30th 1900 to November 16th 2077).</summary>
    public static int DaysInMonth(DateOnly day)
    {
        var at = At(day);
        var calendar = Current;
        return calendar.GetDaysInMonth(calendar.GetYear(at), calendar.GetMonth(at), calendar.GetEra(at));
    }

    /// <summary>The day a number of the calendar's months away, as <see cref="Calendar.AddMonths"/> moves
    /// it: the day of the month is kept, and cut to the last day the target month has.</summary>
    public static DateOnly AddMonths(DateOnly day, int months) =>
        DateOnly.FromDateTime(Current.AddMonths(At(day), months));

    /// <summary>The day a number of the calendar's years away, as <see cref="Calendar.AddYears"/> moves
    /// it: the 30th of Esfand 1403 plus a year is the 29th of Esfand 1404, the month's last day.</summary>
    public static DateOnly AddYears(DateOnly day, int years) =>
        DateOnly.FromDateTime(Current.AddYears(At(day), years));

    private static Calendar Current => CultureInfo.CurrentCulture.DateTimeFormat.Calendar;

    private static DateTime At(DateOnly day) => day.ToDateTime(TimeOnly.MinValue);
}
