using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A number is read from text in the culture the call names, as .NET 10 reads it (#1087): with no
/// provider, the current culture and a null one, the page's culture; with the invariant culture, the
/// invariant one. The browser read the invariant culture's symbols whatever the call said, and the build
/// refused the current culture (EQ2108), so `7,01` could not be read as a number on a pt-BR page. Each
/// case reads a list of texts and writes what each became, `x` for a refusal, in the invariant culture.
/// The cultures: the invariant one, a point-decimal one, two comma-decimal ones, two whose thousands
/// separator is a no-break space (sv-SE's U+00A0, fr-FR's U+202F) and whose minus is U+2212 in two of
/// them, one whose signs carry a direction mark (`ar`), one whose separator is an apostrophe (de-CH), and
/// one whose words for a real that is not a number are its own (fi-FI's `epäluku`).
/// </summary>
public class NumberReadInTheCultureConformanceTests
{
    public static readonly string?[] Cultures = [null, "en-US", "pt-BR", "de-DE", "sv-SE", "fr-FR", "ar", "de-CH", "fi-FI"];

    /// <summary>The texts every number case reads: each culture's decimal point and thousands separator,
    /// each one's spaces, its signs and the stand-ins for them, an exponent, the parentheses.</summary>
    private const string Texts =
        """
        var texts = new[] { "7,01", "7.01", "1.000", "1,000", "1 000", "1 000", "1 000", "1'000",
            "-5", "−5", "‎-5", "+5", "‎+5", "5-", "5−", "- 5", " 3 ", "(5)",
            "1e3", "1E-2", "1e−2", "1e+2", "-1,5e2", ",5", "5,", "", "x" };
        """;

    private static readonly string[] Cases =
    [
        // decimal under NumberStyles.Float in the current culture: the spreadsheet's status bar.
        Texts + """
            string R(string t) => decimal.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Select(R));
            """,
        // decimal with no provider: NumberStyles.Number, the thousands and a trailing sign allowed.
        Texts + """
            string R(string t) => decimal.TryParse(t, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Select(R));
            """,
        // double with a null provider: Float with the thousands, and the culture's words for what is
        // not a finite number.
        Texts + """
            var words = new[] { "∞", "-∞", "−∞", "+∞", "NaN", "+NaN", "-NaN", "−NaN",
                "Infinity", "-Infinity", "epäluku", " nan ", "ليس رقمًا" };
            string R(string t) => double.TryParse(t, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                (IFormatProvider?)null, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Concat(words).Select(R));
            """,
        // int with no provider: .NET's fast path, which allows no white space after a sign.
        Texts + """
            string R(string t) => int.TryParse(t, out var i) ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Select(R));
            """,
        // int under NumberStyles.Number: the whole grammar, the thousands of the culture included.
        Texts + """
            string R(string t) => int.TryParse(t, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture,
                out var i) ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Select(R));
            """,
        // The currency: its symbol, its own point and separator, and the number's until the symbol is met.
        """
        var texts = new[] { "R$ 5,00", "R$5", "5,00 R$", "€5", "5 €", "5,5 €", "$1,234.50", "($5)", "-$5", "$-5",
            "5 kr", "kr 5", "CHF 5.50", "CHF 1'000", "¤5", "5¤", "R$ 1.000,5", "1.000,5 R$", "R$" };
        string R(string t) => decimal.TryParse(t, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.CurrentCulture,
            out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
        return string.Join("|", texts.Select(R));
        """,
        // The invariant culture named on any page reads the invariant symbols.
        Texts + """
            string R(string t) => decimal.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            string D(string t) => double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x";
            return string.Join("|", texts.Select(R)) + "#" + D("Infinity") + D("∞") + D("1,5");
            """,
        // Convert reads a text as Parse does, and throws what Parse throws, in the culture the call names.
        """
        string Of(Func<string> read)
        {
            try { return read(); }
            catch (FormatException) { return "FormatException"; }
            catch (OverflowException) { return "OverflowException"; }
        }
        return string.Join("|",
            Of(() => Convert.ToDecimal("7,01").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToDouble("1,5").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToInt32("\u22125").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToInt64("-5").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToDecimal((object)"2,5").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToDecimal("2.5", System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => Convert.ToSingle("2,5", System.Globalization.CultureInfo.CurrentCulture).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => decimal.Parse("1.000,5").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => byte.Parse("1.000").ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Of(() => double.Parse("abc").ToString(System.Globalization.CultureInfo.InvariantCulture)));
        """,
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ANumber_IsReadInTheCultureTheCallNames_AsInDotNet(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }

    /// <summary>A refusal says what .NET says, with the text it refused.</summary>
    [SkippableTheory]
    [InlineData("pt-BR")]
    [InlineData("sv-SE")]
    public void ARefusal_IsDotNetsFormatException(string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "try { return decimal.Parse(\"7.01.5\").ToString(); } catch (FormatException e) { return e.Message; }",
            culture: culture);
    }
}
