using System.Globalization;
using System.Text.Json;

namespace eQuantic.UI.Web;

/// <summary>
/// What the browser's formatter reads of a culture, serialized from .NET's own data for it: the
/// symbols of its <see cref="NumberFormatInfo"/> and the patterns and separators of its
/// <see cref="DateTimeFormatInfo"/> that the runtime writes by hand, and the ISO code of its currency,
/// which <c>Intl</c> demands and no browser API derives from a locale. It is the culture bridge's
/// FORMAT half (<c>__EQ_CULTURE__.format</c>), written for every page whether or not the app has a
/// string to translate (#471), and what a culture switch fetches for the culture it switches to — the
/// way Flutter's <c>intl</c> reads a locale's <c>NumberSymbols</c> and <c>DateSymbols</c> and never the
/// platform's. Shipped rather than derived because the server's ICU and the browser's do not always
/// agree: <c>ar</c>'s minus sign carries a left-to-right mark <c>Intl</c> keeps outside the sign, and
/// <c>ar-SA</c>'s per mille is <c>؉</c> where <c>Intl</c> has none. The wire shape is
/// <c>CultureFormat</c> in the runtime's <c>utils/culture.ts</c>, whose invariant constant is this
/// method's answer for <see cref="CultureInfo.InvariantCulture"/>, compared by
/// <c>format-subset.spec.ts</c>.
/// </summary>
public static class CultureFormatBridge
{
    /// <summary>The culture's format data as a single-line JSON object.</summary>
    public static string SerializeJson(CultureInfo culture)
    {
        var number = culture.NumberFormat;
        var date = culture.DateTimeFormat;
        return JsonSerializer.Serialize(new
        {
            isoCurrencySymbol = IsoCurrency(culture),
            numberFormat = new
            {
                numberDecimalSeparator = number.NumberDecimalSeparator,
                numberGroupSeparator = number.NumberGroupSeparator,
                numberGroupSizes = number.NumberGroupSizes,
                negativeSign = number.NegativeSign,
                positiveSign = number.PositiveSign,
                percentSymbol = number.PercentSymbol,
                perMilleSymbol = number.PerMilleSymbol,
                nanSymbol = number.NaNSymbol,
                positiveInfinitySymbol = number.PositiveInfinitySymbol,
                negativeInfinitySymbol = number.NegativeInfinitySymbol,
            },
            dateTimeFormat = new
            {
                shortDatePattern = date.ShortDatePattern,
                longDatePattern = date.LongDatePattern,
                shortTimePattern = date.ShortTimePattern,
                longTimePattern = date.LongTimePattern,
                monthDayPattern = date.MonthDayPattern,
                yearMonthPattern = date.YearMonthPattern,
                dateSeparator = date.DateSeparator,
                timeSeparator = date.TimeSeparator,
                // The era `g` writes, of the calendar's current era.
                eraName = date.GetEraName(date.Calendar.Eras[0]),
                // The names `dddd`, `ddd`, `MMMM`, `MMM` and `tt` write, Sunday and January first;
                // a month beside its day takes its genitive name where the culture has one (ru
                // writes `24 сентября`). The browser's ICU abbreviates otherwise: de-DE's September
                // is `Sept.` here and `Sep` in a JS runtime.
                dayNames = date.DayNames,
                abbreviatedDayNames = date.AbbreviatedDayNames,
                monthNames = Months(date.MonthNames),
                abbreviatedMonthNames = Months(date.AbbreviatedMonthNames),
                monthGenitiveNames = Months(date.MonthGenitiveNames),
                abbreviatedMonthGenitiveNames = Months(date.AbbreviatedMonthGenitiveNames),
                amDesignator = date.AMDesignator,
                pmDesignator = date.PMDesignator,
            },
        });
    }

    /// <summary>The twelve months of a .NET month array, whose thirteenth is the leap month of a
    /// lunisolar calendar, empty for every other.</summary>
    private static string[] Months(string[] names) => names[..12];

    /// <summary>
    /// The ISO code of the culture's currency, or null where the culture has none of its own, which
    /// is where .NET's own currency symbol is the generic <c>¤</c>: the invariant culture, a neutral
    /// culture (<c>pt</c>, <c>ar</c>) and one of no single country (<c>es-419</c>). Measured: .NET
    /// writes <c>¤ 1.234,50</c> for <c>pt</c>, while <see cref="RegionInfo"/> reads <c>pt</c> as
    /// Portugal and answers EUR, and <c>ar</c> as Argentina and answers ARS.
    /// </summary>
    private static string? IsoCurrency(CultureInfo culture)
    {
        if (culture.NumberFormat.CurrencySymbol == "¤" || culture.IsNeutralCulture) return null;
        try
        {
            return new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
