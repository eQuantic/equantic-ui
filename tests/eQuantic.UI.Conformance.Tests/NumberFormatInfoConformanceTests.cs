using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A culture's <c>NumberFormatInfo</c> read in the browser answers what .NET answers (#1765): every scalar
/// member, by both doors (<c>NumberFormatInfo.CurrentInfo</c> and <c>CultureInfo.CurrentCulture.NumberFormat</c>),
/// and the invariant culture's on any page. The cultures: the invariant one, a point-decimal one whose symbol
/// leads, comma-decimal ones whose symbol trails (pt-PT, fr-FR with its U+202F) and leads with a space
/// (nl-NL), one that groups with an apostrophe (de-CH), one whose money groups apart from its numbers (de-AT),
/// and one whose signs carry a direction mark (ar). Each read failed the build (EQ2004) where a money field
/// asks which side its currency's symbol goes on.
/// </summary>
public class NumberFormatInfoConformanceTests
{
    public static readonly string?[] Cultures = [null, "en-US", "pt-PT", "fr-FR", "nl-NL", "de-CH", "de-AT", "ar"];

    private static readonly string[] Cases =
    [
        // Every scalar the culture's data carries, through CurrentInfo.
        """
        var f = System.Globalization.NumberFormatInfo.CurrentInfo;
        return string.Join("|", f.NumberDecimalSeparator, f.NumberGroupSeparator, f.NumberDecimalDigits,
            f.NumberNegativePattern, f.NegativeSign, f.PositiveSign, f.CurrencySymbol, f.CurrencyDecimalSeparator,
            f.CurrencyGroupSeparator, f.CurrencyDecimalDigits, f.CurrencyPositivePattern, f.CurrencyNegativePattern,
            f.PercentSymbol, f.PercentDecimalSeparator, f.PercentGroupSeparator, f.PercentDecimalDigits,
            f.PercentPositivePattern, f.PercentNegativePattern, f.PerMilleSymbol, f.PositiveInfinitySymbol,
            f.NegativeInfinitySymbol);
        """,
        // The other door, read inline, and a decision a component makes on it.
        """
        var leads = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyPositivePattern is 0 or 2;
        return (leads ? "symbol first" : "symbol last") + "|" + System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        """,
        // The invariant culture's, on any page, by both doors.
        """
        return System.Globalization.NumberFormatInfo.InvariantInfo.CurrencySymbol + "|"
            + System.Globalization.CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator + "|"
            + System.Globalization.NumberFormatInfo.InvariantInfo.CurrencyNegativePattern;
        """,
        // Handed to a method that takes one, as a component's helper does.
        """
        string Point(System.Globalization.NumberFormatInfo format) => format.NumberDecimalSeparator + format.NumberGroupSeparator;
        return Point(System.Globalization.NumberFormatInfo.CurrentInfo) + "|" + Point(System.Globalization.NumberFormatInfo.InvariantInfo);
        """,
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ACulturesNumberFormat_ReadsAsDotNets(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
