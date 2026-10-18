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
        // FormattableString holds its arguments and formats them after every hole is evaluated, where a
        // handler formats each hole as it goes (Copilot's review of #958).
        "var a = new System.Text.StringBuilder(\"a\"); var b = new System.Text.StringBuilder(\"a\"); var c = new System.Text.StringBuilder(\"a\"); "
            + "var x = 1234.5; var box = new System.Text.StringBuilder(\"1\"); "
            + "return FormattableString.Invariant($\"{a}|{a.Append(\"b\")}\") + \"/\" + FormattableString.CurrentCulture($\"{b}|{b.Append(\"b\")}\") + \"/\" "
            + "+ string.Create(System.Globalization.CultureInfo.InvariantCulture, $\"{c}|{c.Append(\"b\")}\") + \"/\" "
            + "+ FormattableString.Invariant($\"{x:N1}|{box,3}|{box.Append(\"2\"),-4}|\");",
        // A hole held as an object writes its number or its date in the culture the call names (Copilot's
        // review of #958).
        "object x = 1.5; object d = new DateTime(2024, 1, 15, 21, 30, 5); object m = 12.5m; "
            + "return FormattableString.Invariant($\"{x}|{d}|{m}\") + \"/\" + string.Create(System.Globalization.CultureInfo.InvariantCulture, $\"{x}|{d}|{m}\");",
        // A hole held as an interface or a type parameter writes the number or the date it holds in the culture
        // the call names (Copilot's second review of #958).
        "IFormattable x = 1.5; IFormattable d = new DateTime(2024, 1, 15, 21, 30, 5); "
            + "string Invariant<T>(T value) => FormattableString.Invariant($\"{value}\"); "
            + "string Created<T>(T value) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $\"{value}\"); "
            + "return FormattableString.Invariant($\"{x}|{d}\") + \"/\" + string.Create(System.Globalization.CultureInfo.InvariantCulture, $\"{x}|{d}\") + \"/\" "
            + "+ Invariant(2.5) + \"|\" + Invariant(new DateTime(2024, 1, 15)) + \"/\" + Created(2.5) + \"|\" + Created(new DateTime(2024, 1, 15));",
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
