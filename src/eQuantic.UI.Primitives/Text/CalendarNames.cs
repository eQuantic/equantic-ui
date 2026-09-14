using System.Globalization;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What a CALENDAR has to say in the reader's language: which day the week starts on, and the
/// names of the days and months. Resolved at CALL time against the active format culture — the
/// same rule <c>ToString("d")</c> follows, and the reason a culture switch redraws a month grid
/// without anything re-fetching anything.
/// <para>
/// The client twin (<c>shared/calendar-names.ts</c>) does NOT re-derive these: on a page with a
/// server behind it the twin reads the catalog the server sent, verbatim. That is the only way
/// the two sides are equal by construction rather than by coincidence — this side reads
/// <see cref="CultureInfo"/> and <c>Intl</c> reads the browser's own tables, and they are
/// different data by design. <c>ar-EG</c> abbreviates Sunday with the definite article in one ICU
/// build and without it in another; two JS engines disagreed with each other in the same probe.
/// A Linux server therefore sends its spelling and every one of its clients shows that spelling,
/// with no flicker between the SSR markup and the hydrated tree.
/// <para>
/// <c>Intl</c> is the twin's FALLBACK, for a render with no server behind it — a client-only
/// mount, or a culture switched in the browser before any request carried the new catalog. There
/// is nothing to disagree with there.
/// </para>
/// <para>
/// The committed fixture is a SAMPLE for the TypeScript specs, not a promise that three ICUs
/// agree: it proves the twin reads a catalog back verbatim and that the fallback has the right
/// shape. What the .NET side is asked for is the MAPPING — that these members read the culture's
/// own tables, <c>AbbreviatedDayNames</c> and not <c>ShortestDayNames</c> — which is the same on
/// every host.
/// </para>
/// <para>
/// NARROW day names are deliberately ABSENT, and their absence is the finding that shaped this
/// class. .NET's <c>ShortestDayNames</c> and CLDR's <c>weekday: "narrow"</c> are different data:
/// they disagree for seven of the ten cultures probed (en-US "Su" vs "S", pt-BR "dom." vs "D",
/// de-DE "So." vs "S"). No shared derivation rescues it either — taking the first character of the
/// short name gives Chinese seven identical headers, because 周日/周一/周二 all begin with the same
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

    /// <summary>
    /// The culture's short date pattern, as .NET writes it: <c>M/d/yyyy</c> in the United States,
    /// <c>dd/MM/yyyy</c> in Brazil, <c>yyyy-MM-dd</c> in Sweden.
    /// <para>
    /// Handed out RAW because two readers need it and they need different things: the parser
    /// reads which slot holds what, and the field's hint reads the order to show. Deriving either
    /// one from a translated string is how a hint ends up telling a reader to type the date in an
    /// order the parser then refuses.
    /// </para>
    /// </summary>
    public static string ShortDatePattern => Format.ShortDatePattern;

    private static DateTimeFormatInfo Format => CultureInfo.CurrentCulture.DateTimeFormat;

    /// <summary>.NET's month arrays carry THIRTEEN entries — the thirteenth is the leap month of
    /// lunisolar calendars, empty for every Gregorian culture. Handing it out would put a nameless
    /// month in a picker's year grid.</summary>
    private static IReadOnlyList<string> Trimmed(string[] months) => months.Length > 12
        ? months[..12]
        : months;
}
