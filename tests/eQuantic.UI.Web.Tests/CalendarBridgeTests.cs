using System.Globalization;
using System.Text.Json;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The calendar a culture's format data carries to the browser (#1191) counts every month as .NET does.
/// The description is measured from .NET's own <see cref="Calendar"/>, and these read it back the way the
/// runtime's <c>utils/calendar.ts</c> does, then walk every month of every year the calendar counts and
/// compare the day it starts on with <see cref="Calendar.ToDateTime(int,int,int,int,int,int,int)"/>: the
/// Persian calendar's leap years, which .NET computes from the equinox, and Um Al Qura's observed months
/// included. The conformance suite runs the runtime's reading of it against .NET on dates; this is the
/// whole range, which no list of dates covers.
/// </summary>
public class CalendarBridgeTests
{
    private static JsonElement CalendarOf(CultureInfo culture) =>
        JsonDocument.Parse(CultureFormatBridge.SerializeJson(culture)).RootElement
            .GetProperty("dateTimeFormat").GetProperty("calendar");

    /// <summary>A culture of <paramref name="name"/> counting its dates in <paramref name="calendar"/>,
    /// one of the culture's optional calendars.</summary>
    private static CultureInfo In(string name, Calendar calendar)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        culture.DateTimeFormat.Calendar = calendar;
        return culture;
    }

    /// <summary>A culture that may count in <paramref name="calendar"/>, counting in it: whichever culture
    /// its host's data offers that calendar to.</summary>
    private static CultureInfo In(Calendar calendar)
    {
        var name = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .First(culture => culture.OptionalCalendars.Any(optional => optional.GetType() == calendar.GetType())).Name;
        return In(name, calendar);
    }

    public static TheoryData<string> DefaultCultures() => new() { "th-TH", "fa-IR", "ar-SA", "en-US", "" };

    [Theory]
    [MemberData(nameof(DefaultCultures))]
    public void EveryMonthACultureCounts_StartsOnTheDayDotNetCountsIt(string name) =>
        AssertEveryMonth(CultureInfo.GetCultureInfo(name));

    [Fact]
    public void ACalendarACultureMayBeGiven_IsCountedAsDotNetCountsIt()
    {
        // ar-SA may count in the arithmetic Hijri calendar instead of Um Al Qura; zh-TW in Taiwan's years.
        AssertEveryMonth(In("ar-SA", new HijriCalendar()));
        AssertEveryMonth(In("zh-TW", new TaiwanCalendar()));
    }

    [Fact]
    public void TheShapes_AreTheOnesTheRuntimeReads()
    {
        CalendarOf(CultureInfo.GetCultureInfo("th-TH")).GetProperty("yearOffset").GetInt32().Should().Be(543);
        CalendarOf(CultureInfo.InvariantCulture).GetRawText().Should().Be(
            "{\"name\":\"System.Globalization.GregorianCalendar\",\"twoDigitYearMax\":2049,\"yearOffset\":0}",
            "the runtime's INVARIANT_FORMAT carries this calendar, and the Gregorian one is recognised by it");
        // Which calendar a culture defaults to is its host's ICU's to say: Linux's gives ar-SA the Gregorian
        // one, macOS's Um Al Qura. So each calendar is asked of a culture that may count in it.
        var persian = CalendarOf(In(new PersianCalendar()));
        persian.GetProperty("months").GetProperty("varying").EnumerateArray().Select(m => m.GetInt32())
            .Should().Equal([12], "only Esfand's length varies, one bit a year");
        persian.GetProperty("timeOfDayInvariant").GetBoolean().Should().BeTrue();
        CalendarOf(In(new UmAlQuraCalendar())).GetProperty("outOfRange").GetString().Should().Be(
            "Specified time is not supported in this calendar. It should be between 04/30/1900 00:00:00 (Gregorian date) and 11/16/2077 23:59:59 (Gregorian date), inclusive.");
    }

    [Fact]
    public void ACalendarOfErasOrOfAThirteenthMonth_IsRefusedRatherThanWrittenInGregorianNumbers()
    {
        var japanese = () => CultureFormatBridge.SerializeJson(In("ja-JP", new JapaneseCalendar()));
        japanese.Should().Throw<NotSupportedException>().WithMessage("*JapaneseCalendar*more than one era*");
        var hebrew = () => CultureFormatBridge.SerializeJson(In("he-IL", new HebrewCalendar()));
        hebrew.Should().Throw<NotSupportedException>().WithMessage("*HebrewCalendar*twelve months*");
    }

    /// <summary>Reads the description as the runtime does and compares the start of every month with
    /// .NET's, and the first and last day the calendar writes.</summary>
    private static void AssertEveryMonth(CultureInfo culture)
    {
        var calendar = culture.DateTimeFormat.Calendar;
        var described = CalendarOf(culture);
        var firstDay = (int)(calendar.MinSupportedDateTime.Ticks / TimeSpan.TicksPerDay);
        var lastDay = (int)(calendar.MaxSupportedDateTime.Ticks / TimeSpan.TicksPerDay);
        (described.TryGetProperty("firstDay", out var first) ? first.GetInt32() : 0).Should().Be(firstDay);
        (described.TryGetProperty("lastDay", out var last) ? last.GetInt32() : 3_652_058).Should().Be(lastDay);
        described.GetProperty("twoDigitYearMax").GetInt32().Should().Be(calendar.TwoDigitYearMax);

        var starts = MonthStarts(described);
        var checkedMonths = 0;
        for (var year = calendar.GetYear(calendar.MinSupportedDateTime); year <= calendar.GetYear(calendar.MaxSupportedDateTime); year++)
        {
            for (var month = 1; month <= calendar.GetMonthsInYear(year); month++)
            {
                if (!Counts(calendar, year, month, out var dotnet)) continue;
                starts(year, month).Should().Be(dotnet, $"{year}/{month} of {calendar.GetType().Name} starts on .NET's day");
                checkedMonths++;
            }
        }
        checkedMonths.Should().BeGreaterThan(12 * 150, "every year the calendar writes was walked");

        // The month of the last day: no month after it says how long it is, and its last day is read.
        var max = calendar.MaxSupportedDateTime;
        var (lastYear, lastMonth) = (calendar.GetYear(max), calendar.GetMonth(max));
        var next = lastMonth == 12 ? starts(lastYear + 1, 1) : starts(lastYear, lastMonth + 1);
        (next - starts(lastYear, lastMonth)).Should().BeGreaterThanOrEqualTo(calendar.GetDayOfMonth(max),
            "the last day the calendar writes is in its last month");
    }

    /// <summary>The day month <paramref name="month"/> of <paramref name="year"/> starts on, as .NET
    /// counts it, where .NET writes that day.</summary>
    private static bool Counts(Calendar calendar, int year, int month, out int day)
    {
        try
        {
            day = (int)(calendar.ToDateTime(year, month, 1, 0, 0, 0, 0).Ticks / TimeSpan.TicksPerDay);
            return day >= calendar.MinSupportedDateTime.Ticks / TimeSpan.TicksPerDay;
        }
        catch (ArgumentOutOfRangeException)
        {
            day = 0;
            return false;
        }
    }

    /// <summary>The day each month starts on, read off the description as <c>utils/calendar.ts</c> reads it:
    /// the Gregorian months under a count of years, or the years of months of the calendar's own added up
    /// from the first.</summary>
    private static Func<int, int, int> MonthStarts(JsonElement described)
    {
        if (described.TryGetProperty("yearOffset", out var offset))
        {
            var years = offset.GetInt32();
            return (year, month) => year - years > 9999
                ? (int)(DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay) + 1
                : (int)(new DateTime(year - years, month, 1).Ticks / TimeSpan.TicksPerDay);
        }
        var months = described.GetProperty("months");
        var firstYear = months.GetProperty("firstYear").GetInt32();
        var lengths = months.GetProperty("lengths").EnumerateArray().Select(m => m.GetInt32()).ToArray();
        var varying = months.GetProperty("varying").EnumerateArray().Select(m => m.GetInt32()).ToList();
        var bits = Convert.FromBase64String(months.GetProperty("longer").GetString()!);
        var count = months.GetProperty("years").GetInt32();
        // One more than the months counted: the day after the last one ends.
        var starts = new int[count * 12 + 1];
        starts[0] = months.GetProperty("start").GetInt32();
        for (var at = 0; at < count * 12; at++)
        {
            var slot = varying.IndexOf(at % 12 + 1);
            var bit = at / 12 * varying.Count + slot;
            starts[at + 1] = starts[at] + lengths[at % 12] + (slot < 0 ? 0 : (bits[bit >> 3] >> (7 - (bit & 7))) & 1);
        }
        return (year, month) => starts[(year - firstYear) * 12 + month - 1];
    }
}
