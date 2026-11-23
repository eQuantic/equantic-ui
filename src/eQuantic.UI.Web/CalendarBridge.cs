using System.Collections.Concurrent;
using System.Globalization;

namespace eQuantic.UI.Web;

/// <summary>
/// What the browser reads of a culture's calendar, the <c>calendar</c> of its date format data (#1191). .NET
/// writes and reads a date in the calendar of its culture: th-TH counts the Thai Buddhist years, fa-IR the
/// Persian calendar's and ar-SA the Um Al Qura one's, so the 15th of January 2024 is <c>15/1/2567</c>,
/// <c>1402/10/25</c> and the 3rd of Rajab 1445 there. The browser wrote the Gregorian numbers under those
/// calendars' names and read <c>15/1/2567</c> as the year 2567.
/// <para>
/// A calendar is described by MEASURING .NET's own <see cref="Calendar"/>, never by transcribing one: the
/// length of every month of every year it counts is asked of it, so the Persian calendar's leap years,
/// which .NET computes from the equinox, and Um Al Qura's months, which come from a table of observations,
/// arrive as .NET has them. A calendar whose months are the Gregorian ones under another count of years is
/// that count's distance (<c>yearOffset</c>: 543 for the Thai Buddhist calendar, 0 for the Gregorian one).
/// Any other calendar of twelve months a year is each month's shorter length and, for every year, one bit
/// for each month whose length varies (<c>months</c>): one bit a year for the Persian calendar, twelve for
/// Um Al Qura's. A calendar of more than one era (the Japanese one) or of a thirteenth month (the Hebrew
/// one and the lunisolar ones) is none a culture defaults to, and is refused rather than written in the
/// Gregorian numbers.
/// </para>
/// </summary>
internal static class CalendarBridge
{
    /// <summary>The day <see cref="DateTime.MaxValue"/> falls on, as days since 0001-01-01.</summary>
    private static readonly int LastDayOfDateTime = (int)(DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay);

    /// <summary>The shape of each calendar measured, by its type and the one setting of a calendar of the
    /// framework that moves its days, <see cref="HijriCalendar.HijriAdjustment"/>. Measuring the Persian
    /// calendar asks .NET for the equinox of nine thousand years, once a process.</summary>
    private static readonly ConcurrentDictionary<(Type Type, int Adjustment), Shape> Shapes = new();

    /// <summary>What each culture says of its calendar's range, by the culture's name and the calendar's
    /// type: the refusal is read by provoking it, which is not paid on every page.</summary>
    private static readonly ConcurrentDictionary<(string Culture, Type Calendar), (string? OutOfRange, bool TimeOfDayInvariant)> Sayings = new();

    /// <summary>The calendar <paramref name="culture"/> writes its dates in, as the runtime's
    /// <c>CalendarData</c> reads it.</summary>
    public static Dictionary<string, object> Describe(CultureInfo culture)
    {
        var calendar = culture.DateTimeFormat.Calendar;
        var shape = Shapes.GetOrAdd(
            (calendar.GetType(), calendar is HijriCalendar hijri ? hijri.HijriAdjustment : 0),
            _ => Measure(calendar));
        var described = new Dictionary<string, object>
        {
            ["name"] = calendar.GetType().FullName!,
            ["twoDigitYearMax"] = calendar.TwoDigitYearMax,
        };
        if (shape.YearOffset is { } offset) described["yearOffset"] = offset;
        if (shape.Months is { } months)
        {
            described["months"] = new Dictionary<string, object>
            {
                ["firstYear"] = months.FirstYear,
                ["start"] = months.Start,
                ["years"] = months.Years,
                ["lengths"] = months.Lengths,
                ["varying"] = months.Varying,
                ["longer"] = months.Longer,
            };
        }
        var bounded = shape.FirstDay != 0 || shape.LastDay != LastDayOfDateTime;
        var (outOfRange, timeOfDayInvariant) = Sayings.GetOrAdd(
            (culture.Name, calendar.GetType()),
            _ => (bounded ? OutOfRange(calendar, culture, shape) : null, WritesATimeOfDayInvariant(culture)));
        if (bounded)
        {
            described["firstDay"] = shape.FirstDay;
            described["lastDay"] = shape.LastDay;
            described["outOfRange"] = outOfRange!;
        }
        if (timeOfDayInvariant) described["timeOfDayInvariant"] = true;
        return described;
    }

    /// <summary>The months of a calendar of its own: its first year, the day that year starts on (days
    /// since 0001-01-01), how many years it counts, each month's shorter length, the months whose length
    /// varies (1 to 12), and a bit a year for each of them, set where it is a day longer, in base64.</summary>
    private sealed record Months(int FirstYear, int Start, int Years, int[] Lengths, int[] Varying, string Longer);

    /// <summary>A calendar measured: the Gregorian months under a count of years <c>YearOffset</c> ahead, or
    /// months of its own; and the first and the last day it writes, as days since 0001-01-01.</summary>
    private sealed record Shape(int? YearOffset, Months? Months, int FirstDay, int LastDay);

    private static Shape Measure(Calendar calendar)
    {
        if (calendar.Eras.Length != 1)
            throw Refused(calendar, "counts its years in more than one era");
        var min = calendar.MinSupportedDateTime;
        var max = calendar.MaxSupportedDateTime;
        var firstDay = (int)(min.Ticks / TimeSpan.TicksPerDay);
        var lastDay = (int)(max.Ticks / TimeSpan.TicksPerDay);
        var firstYear = calendar.GetYear(min);
        var lastYear = calendar.GetYear(max);
        var lastMonth = calendar.GetMonth(max);

        // Every month of every year, as .NET counts it. The month of the last day the calendar writes and
        // every one after it are left at 0 and none of them is measured, as no day after that one is read:
        // the Persian calendar counts its last year in the months it writes of it, ten, and the tenth in
        // the days it writes, thirteen. The last month is as long as that day's number, at least.
        var lengths = new int[lastYear - firstYear + 1][];
        for (var year = firstYear; year <= lastYear; year++)
        {
            var monthsInYear = calendar.GetMonthsInYear(year);
            if (monthsInYear != 12 && !(year == lastYear && monthsInYear >= lastMonth))
                throw Refused(calendar, "counts a year of other than twelve months");
            var months = lengths[year - firstYear] = new int[12];
            for (var month = 1; month <= (year == lastYear ? lastMonth - 1 : 12); month++)
                months[month - 1] = calendar.GetDaysInMonth(year, month);
        }

        // The Gregorian months under another count of years: the same day starts both, and every year's
        // months are the Gregorian year's that many years behind it.
        var offset = firstYear - min.Year;
        if (calendar.GetMonth(min) == min.Month && calendar.GetDayOfMonth(min) == min.Day
            && lengths.Select((months, at) => (months, year: firstYear + at - offset)).All(pair =>
                pair.year is >= 1 and <= 9999
                && pair.months.Select((length, month) => length == 0 || length == DateTime.DaysInMonth(pair.year, month + 1)).All(same => same)))
        {
            return new Shape(offset, null, firstDay, lastDay);
        }

        var shortest = new int[12];
        var varying = new List<int>();
        for (var month = 0; month < 12; month++)
        {
            var counted = lengths.Select(months => months[month]).Where(length => length > 0).ToArray();
            shortest[month] = counted.Min();
            var longest = counted.Max();
            if (longest - shortest[month] > 1)
                throw Refused(calendar, $"counts its month {month + 1} in lengths more than a day apart");
            if (longest > shortest[month]) varying.Add(month + 1);
        }

        var lastDayOfMonth = calendar.GetDayOfMonth(max);
        if (lastDayOfMonth > shortest[lastMonth - 1] && !varying.Contains(lastMonth))
            throw Refused(calendar, $"writes a day of its last month {lastMonth} past that month's length in every other year");
        lengths[^1][lastMonth - 1] = lastDayOfMonth;
        var bits = new byte[(lengths.Length * varying.Count + 7) / 8];
        var at = 0;
        foreach (var months in lengths)
        {
            foreach (var month in varying)
            {
                if (months[month - 1] > shortest[month - 1]) bits[at / 8] |= (byte)(0x80 >> (at % 8));
                at++;
            }
        }

        var start = firstDay - (calendar.GetDayOfYear(min) - 1);
        return new Shape(
            null,
            new Months(firstYear, start, lengths.Length, shortest, [.. varying], Convert.ToBase64String(bits)),
            firstDay,
            lastDay);
    }

    /// <summary>
    /// .NET's refusal of a day its calendar does not write, the sentence before the parameter's name, read
    /// from the calendar itself while the culture is the current one: the Persian calendar writes the days
    /// it names in the culture in force, and Um Al Qura's in the invariant one.
    /// </summary>
    private static string OutOfRange(Calendar calendar, CultureInfo culture, Shape shape)
    {
        var outside = shape.FirstDay > 0 ? DateTime.MinValue : DateTime.MaxValue;
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            calendar.GetYear(outside);
        }
        catch (ArgumentOutOfRangeException refusal)
        {
            var end = refusal.Message.IndexOf($" (Parameter '{refusal.ParamName}')", StringComparison.Ordinal);
            return end < 0 ? refusal.Message : refusal.Message[..end];
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
        throw new InvalidOperationException(
            $"The calendar '{calendar.GetType().FullName}' wrote a day outside the range it declares.");
    }

    /// <summary>
    /// Whether the culture writes a time of day held in a <see cref="DateTime"/> of the first day, its text
    /// with no format, as the invariant sortable form: .NET does where its calendar cannot write the year 1
    /// (the Persian and Um Al Qura ones), so <c>default(DateTime).ToString()</c> is
    /// <c>0001-01-01T00:00:00</c> there and not a refusal. Asked of .NET rather than listed.
    /// </summary>
    private static bool WritesATimeOfDayInvariant(CultureInfo culture)
    {
        var timeOfDay = new DateTime(TimeSpan.TicksPerHour);
        try
        {
            return timeOfDay.ToString(culture) == timeOfDay.ToString("s", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static NotSupportedException Refused(Calendar calendar, string why) =>
        new($"The browser cannot write or read a date in the calendar '{calendar.GetType().FullName}', which {why}: "
            + "a culture whose dates are in it cannot be the format culture of a page.");
}
