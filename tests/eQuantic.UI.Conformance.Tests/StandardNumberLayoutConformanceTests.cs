using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>N</c>, <c>F</c>, <c>C</c> and <c>P</c> as .NET lays them out from the culture's
/// <c>NumberFormatInfo</c> (#634): its patterns, symbols, separators, group sizes and default digits.
/// <c>Intl</c> laid them out, and wrote ar-EG's own digits where .NET writes ASCII ones, a no-break
/// space where .NET's currency and percent patterns have a plain one, and two digits with no precision
/// where .NET reads the culture's, three on ICU.
/// </summary>
public class StandardNumberLayoutConformanceTests
{
    /// <summary>The invariant culture, a comma-decimal one, one whose currency follows the number, one
    /// whose group separator is a narrow no-break space, one whose own digits are not ASCII, and one
    /// whose default digits ICU and NLS disagree on.</summary>
    public static readonly string?[] Cultures = [null, "pt-BR", "de-DE", "fr-FR", "ar-EG", "en-US"];

    public static IEnumerable<object?[]> EveryCulture() => Cultures.Select(culture => new object?[] { culture });

    [SkippableTheory]
    [MemberData(nameof(EveryCulture))]
    public void AStandardNumberFormat_IsLaidOutAsDotNetLaysItOut(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "double d = -1234.5, p = 0.125; decimal m = 1234.5m; int i = -42; "
            + "return d.ToString(\"N\") + \"|\" + d.ToString(\"N2\") + \"|\" + d.ToString(\"F\") + \"|\" "
            + "+ d.ToString(\"C\") + \"|\" + d.ToString(\"C0\") + \"|\" + p.ToString(\"P\") + \"|\" "
            + "+ p.ToString(\"P1\") + \"|\" + m.ToString(\"C\") + \"|\" + i.ToString(\"N0\") + \"|\" + $\"{d:N1}|{p:P0}\";",
            culture: culture);
    }
}
