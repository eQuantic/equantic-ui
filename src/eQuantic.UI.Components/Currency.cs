using System.Globalization;

namespace eQuantic.UI.Components;

/// <summary>
/// A currency: its ISO 4217 code, the symbol a reader knows it by, and how many digits its minor unit
/// has (2 for the euro's cents, 0 for the yen). An amount of it is a whole number of minor units, a
/// <c>long</c>, which is how money is kept: a sum of cents is exact where a sum of fractions is not.
/// <para>
/// The symbol is the currency's, whatever the page's culture: a page in English shows <c>€</c> for euros,
/// where .NET's <c>C</c> would write the culture's own currency. The culture decides everything else:
/// its separators, its signs, and which side of the amount the symbol goes on.
/// </para>
/// </summary>
public sealed record Currency(string Code, string Symbol, int MinorDigits)
{
    public static readonly Currency Euro = new Currency("EUR", "€", 2);
    public static readonly Currency UsDollar = new Currency("USD", "$", 2);
    public static readonly Currency PoundSterling = new Currency("GBP", "£", 2);
    public static readonly Currency BrazilianReal = new Currency("BRL", "R$", 2);
    public static readonly Currency SwissFranc = new Currency("CHF", "CHF", 2);
    public static readonly Currency Yen = new Currency("JPY", "¥", 0);

    /// <summary>.NET's patterns for a positive amount (<c>CurrencyPositivePattern</c>): <c>$</c> is the
    /// symbol and <c>n</c> the number.</summary>
    private static readonly string[] Positive = ["$n", "n$", "$ n", "n $"];

    /// <summary>.NET's patterns for a negative amount (<c>CurrencyNegativePattern</c>), <c>-</c> being the
    /// culture's negative sign.</summary>
    private static readonly string[] Negative =
    [
        "($n)", "-$n", "$-n", "$n-", "(n$)", "-n$", "n-$", "n$-", "-n $",
        "-$ n", "n $-", "$ n-", "$ -n", "n- $", "($ n)", "(n $)", "$- n",
    ];

    /// <summary>
    /// An amount, in minor units, as the page's culture writes money with this currency's symbol and
    /// digits: <c>1 234,50 €</c> in pt-PT and <c>€1,234.50</c> in English for 123450 euro cents. What .NET's
    /// <c>C</c> writes with a copy of the culture's <c>NumberFormatInfo</c> whose symbol and digits are this
    /// currency's.
    /// </summary>
    public string Format(long amount)
    {
        var culture = NumberFormatInfo.CurrentInfo;
        var negative = amount < 0;
        var major = ToMajor(amount);
        var number = Digits(negative ? -major : major, culture);
        var pattern = negative ? Negative[culture.CurrencyNegativePattern] : Positive[culture.CurrencyPositivePattern];
        var text = "";
        foreach (var part in pattern)
        {
            text += part switch
            {
                '$' => Symbol,
                'n' => number,
                '-' => culture.NegativeSign,
                _ => part.ToString(),
            };
        }
        return text;
    }

    /// <summary>
    /// The amount as a decimal number of the currency's major unit: 12.50 for 1250 euro cents. Divided as
    /// a decimal, so the minor units stay exact.
    /// </summary>
    public decimal ToMajor(long amount) => amount / Scale();

    /// <summary>
    /// The major-unit number as a whole number of minor units, or null when it has more fraction digits
    /// than the currency has or does not fit a <c>long</c>: 12.5 euros is 1250 cents, and 12.505 is no
    /// amount of euros at all.
    /// </summary>
    public long? ToMinor(decimal major)
    {
        // Measured against the range before multiplying: a decimal that large overflows the product.
        if (major > long.MaxValue / Scale() || major < long.MinValue / Scale()) return null;
        var scaled = major * Scale();
        if (decimal.Truncate(scaled) != scaled) return null;
        if (scaled > long.MaxValue || scaled < long.MinValue) return null;
        return (long)scaled;
    }

    /// <summary>Ten to the power of the minor digits: what a major unit is worth in minor ones.</summary>
    private decimal Scale()
    {
        var scale = 1m;
        for (var i = 0; i < MinorDigits; i++) scale *= 10m;
        return scale;
    }

    /// <summary>The number laid out as the culture writes a number, grouped, with the currency's digits,
    /// and then its separators swapped for the culture's currency ones, where a few cultures differ
    /// (de-AT groups money with a point, and numbers with a space).</summary>
    private string Digits(decimal value, NumberFormatInfo culture)
    {
        var plain = value.ToString("N" + MinorDigits);
        var point = MinorDigits > 0 ? plain.LastIndexOf(culture.NumberDecimalSeparator, StringComparison.Ordinal) : -1;
        var whole = point < 0 ? plain : plain.Substring(0, point);
        var fraction = point < 0 ? "" : plain.Substring(point + culture.NumberDecimalSeparator.Length);
        if (culture.NumberGroupSeparator.Length > 0)
            whole = whole.Replace(culture.NumberGroupSeparator, culture.CurrencyGroupSeparator);
        return fraction.Length > 0 ? whole + culture.CurrencyDecimalSeparator + fraction : whole;
    }
}
