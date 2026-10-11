using System.Globalization;
using System.Text.Json;

namespace eQuantic.UI.Web;

/// <summary>
/// What the browser's formatter reads of a culture, serialized from .NET's own data for it: the
/// symbols, separators, group sizes, digits and patterns of its <see cref="NumberFormatInfo"/>, and
/// the patterns, separators and names of its <see cref="DateTimeFormatInfo"/>, from which the
/// runtime writes every number and date by hand. It is the culture bridge's FORMAT half
/// (<c>__EQ_CULTURE__.format</c>), written for every page whether or not the app has a string to
/// translate (#471), and what a culture switch fetches for the culture it switches to — the way
/// Flutter's <c>intl</c> reads a locale's <c>NumberSymbols</c> and <c>DateSymbols</c> and never the
/// platform's. Shipped rather than derived because the server's ICU and the browser's do not agree:
/// <c>ar</c>'s minus sign carries a left-to-right mark <c>Intl</c> keeps outside the sign,
/// <c>ar-SA</c>'s per mille is <c>؉</c> where <c>Intl</c> has none, <c>Intl</c> writes ar-EG's own
/// digits where .NET writes ASCII ones, and .NET reads three default digits for <c>N</c> on ICU
/// where it reads two on NLS (#634). The wire shape is
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
            numberFormat = new
            {
                numberDecimalSeparator = number.NumberDecimalSeparator,
                numberGroupSeparator = number.NumberGroupSeparator,
                numberGroupSizes = number.NumberGroupSizes,
                numberDecimalDigits = number.NumberDecimalDigits,
                numberNegativePattern = number.NumberNegativePattern,
                negativeSign = number.NegativeSign,
                positiveSign = number.PositiveSign,
                // `C` as .NET lays it out: its symbol, not an ISO code, in the culture's own pattern.
                currencySymbol = number.CurrencySymbol,
                currencyDecimalSeparator = number.CurrencyDecimalSeparator,
                currencyGroupSeparator = number.CurrencyGroupSeparator,
                currencyGroupSizes = number.CurrencyGroupSizes,
                currencyDecimalDigits = number.CurrencyDecimalDigits,
                currencyPositivePattern = number.CurrencyPositivePattern,
                currencyNegativePattern = number.CurrencyNegativePattern,
                percentSymbol = number.PercentSymbol,
                percentDecimalSeparator = number.PercentDecimalSeparator,
                percentGroupSeparator = number.PercentGroupSeparator,
                percentGroupSizes = number.PercentGroupSizes,
                percentDecimalDigits = number.PercentDecimalDigits,
                percentPositivePattern = number.PercentPositivePattern,
                percentNegativePattern = number.PercentNegativePattern,
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
                // The day a calendar starts its week on, as System.DayOfWeek counts.
                firstDayOfWeek = (int)date.FirstDayOfWeek,
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
}
