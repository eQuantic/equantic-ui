using System.Globalization;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What a CALENDAR has to say in the reader's language: which day the week starts on, and the
/// names of the days and months. Resolved at CALL time against the active format culture — the
/// same rule <c>ToString("d")</c> follows, and the reason a culture switch redraws a month grid
/// without anything re-fetching anything.
/// <para>
/// The client twin (<c>shared/calendar-names.ts</c>) answers from <c>Intl</c> where this answers
/// from <see cref="CultureInfo"/>. That they agree is not an assumption: it was PROBED across ten
/// cultures (en-US, pt-BR, es-ES, fr-FR, de-DE, ja-JP, ar-EG, en-GB, ru-RU, zh-CN) before this
/// surface was fixed, and it is pinned by a generated fixture the TypeScript specs assert against.
/// </para>
/// <para>
/// NARROW day names are deliberately ABSENT, and their absence is the finding that shaped this
/// class. .NET's <c>ShortestDayNames</c> and CLDR's <c>weekday: "narrow"</c> are different data:
/// they disagree for seven of the ten cultures probed (en-US "Su" vs "S", pt-BR "dom." vs "D",
/// de-DE "So." vs "S"). No shared derivation rescues it either — taking the first character of the
/// short name gives Chinese seven identical headers, because 周日/周一/週二 all begin with the same
/// glyph. A calendar that cannot say the day names honestly in every script says them in the
/// SHORT form, which both sides agree on exactly.
/// </para>
/// </summary>
public static class CalendarNames
{
    /// <summary>The day the week starts on for the active culture, as
    /// <see cref="System.DayOfWeek"/>'s numbering (0 = Sunday): 0 in the United States and Brazil,
    /// 1 across most of Europe, 6 in Egypt.</summary>
    public static int FirstDayOfWeek => (int)Format.FirstDayOfWeek;

    /// <summary>The seven day names in their abbreviated form, ALWAYS Sunday-first — the calendar
    /// rotates them by <see cref="FirstDayOfWeek"/>, so the array's order never depends on the
    /// culture and an index is always <see cref="System.DayOfWeek"/>'s.</summary>
    public static IReadOnlyList<string> DayNamesShort => Format.AbbreviatedDayNames;

    /// <summary>The seven day names in full, Sunday-first — what a cell announces to a screen
    /// reader ("Friday, July 17"), where the abbreviation would be read letter by letter.</summary>
    public static IReadOnlyList<string> DayNamesLong => Format.DayNames;

    /// <summary>The twelve month names in full, January-first.</summary>
    public static IReadOnlyList<string> MonthNames => Trimmed(Format.MonthNames);

    /// <summary>The twelve month names abbreviated, January-first.</summary>
    public static IReadOnlyList<string> MonthNamesShort => Trimmed(Format.AbbreviatedMonthNames);

    private static DateTimeFormatInfo Format => CultureInfo.CurrentCulture.DateTimeFormat;

    /// <summary>.NET's month arrays carry THIRTEEN entries — the thirteenth is the leap month of
    /// lunisolar calendars, empty for every Gregorian culture. Handing it out would put a nameless
    /// month in a picker's year grid.</summary>
    private static IReadOnlyList<string> Trimmed(string[] months) => months.Length > 12
        ? months[..12]
        : months;
}
