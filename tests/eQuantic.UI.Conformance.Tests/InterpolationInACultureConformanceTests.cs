using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An interpolation written in the culture a call names, as .NET writes it, in the invariant culture,
/// pt-BR and sv-SE (#839): <c>FormattableString.Invariant($"…")</c> and
/// <c>string.Create(CultureInfo.InvariantCulture, $"…")</c> write every hole in the invariant culture
/// whatever culture the page is in, and <c>FormattableString.CurrentCulture($"…")</c> and
/// <c>string.Create</c> with the current culture or a null provider write them in the page's. The calls
/// went out as <c>FormattableString.invariant(…)</c> and <c>string.create(…)</c>, names nothing defines.
/// A hole with a format and an alignment, a date, a negative number and a hole whose value writes its own
/// text are each written as .NET writes them.
/// </summary>
public class InterpolationInACultureConformanceTests
{
    private static readonly string?[] Cultures = [null, "pt-BR", "sv-SE"];

    private const string Values =
        "var d = new DateTime(2024, 1, 15, 21, 30, 5); var x = 1234.5; var n = -3; var f = 0.1f; var m = 12.50m; "
        + "var flag = true; string none = null; var t = (1.5, \"a\"); ";

    private static readonly string[] Cases =
    [
        Values + "return FormattableString.Invariant($\"{x}|{n}|{d}|{f}|{m}|{flag}|[{none}]|{x:N2}|{x,10:F1}|{d:D}|{n,-4}|{t}\");",
        Values + "return FormattableString.CurrentCulture($\"{x}|{n}|{d}|{f}|{m}|{flag}|[{none}]|{x:N2}|{x,10:F1}|{d:D}|{n,-4}|{t}\");",
        Values + "return string.Create(System.Globalization.CultureInfo.InvariantCulture, $\"{x}|{n}|{d:g}|{x:N2}|{m,8}\");",
        Values + "return string.Create(System.Globalization.CultureInfo.CurrentCulture, $\"{x}|{n}|{d:g}|{x:N2}|{m,8}\") + \"/\" "
            + "+ string.Create(null, $\"{x}|{n}|{d:g}\");",
        // A hole that is itself an interpolation is written in the page's culture before it is a hole,
        // as .NET writes it.
        Values + "return FormattableString.Invariant($\"{x}|{$\"{x}\"}|{x.ToString()}\");",
    ];

    public static IEnumerable<object?[]> CasesInEachCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEachCulture))]
    public void AnInterpolationInTheCultureACallNames_IsWrittenAsDotNetWritesIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
