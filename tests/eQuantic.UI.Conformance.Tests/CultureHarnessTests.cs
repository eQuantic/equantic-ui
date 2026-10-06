using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The culture each side of a case runs in is the one the case names, and the invariant one when it
/// names none, on every host (#471). The browser side used to read the HOST's locale whenever no
/// culture was installed: on a machine whose default locale is pt-BR, <c>(1234.5).ToString("N2")</c>
/// printed <c>1.234,50</c> on the browser side against the invariant .NET side's <c>1,234.50</c>, so a
/// case passed or failed by the machine it ran on.
/// </summary>
public class CultureHarnessTests
{
    /// <summary>
    /// With no culture installed, the runtime never asks <c>Intl</c> for the host's locale: every
    /// <c>Intl</c> constructor and every <c>toLocale…String</c> is replaced by one that throws when it
    /// is handed no locale, and the case still answers what the invariant .NET side answers. A path
    /// that falls back to the host fails here, on every machine, rather than only on one whose locale
    /// is not English.
    /// </summary>
    [SkippableFact]
    public void WithNoCultureInstalled_TheBrowserSideNeverReadsTheHostsLocale()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        const string statements =
            "double d = -1234.5; float f = 0.1f; decimal m = 1.5m; var t = new DateTime(2026, 9, 24, 22, 30, 15); "
            + "return $\"{d:N2}|{d:C}|{d:P1}|{d:F1}|{d:E2}|{d:G}|{d:0.00}|{d}|{f}|{m}|{double.NaN}|{double.NegativeInfinity}|{-5}\" "
            + "+ $\"|{t:d}|{t:D}|{t:G}|{t:T}|{t:f}|{t:M}|{t:Y}|{t:yyyy MMMM dddd tt g}|{t}\" "
            + "+ \"|\" + d + \"|\" + new DateOnly(2026, 9, 24) + \"|\" + new TimeOnly(10, 30);";

        var js = Transpiler.TranspileStatements(statements);
        var url = ConformanceRunner.RuntimeJsUrl()
            ?? throw new InvalidOperationException("Could not locate the bundled runtime.js.");
        const string poison = """
            for (const name of ['NumberFormat', 'DateTimeFormat', 'PluralRules', 'Collator']) {
              const Real = Intl[name];
              Intl[name] = function (locale, options) {
                if (locale === undefined || (Array.isArray(locale) && locale.length === 0))
                  throw new Error(`Intl.${name} was asked for the host's locale`);
                return new Real(locale, options);
              };
            }
            for (const type of [Number, Date, BigInt]) {
              for (const method of ['toLocaleString', 'toLocaleDateString', 'toLocaleTimeString']) {
                const real = type.prototype[method];
                if (real === undefined) continue;
                type.prototype[method] = function (locale, options) {
                  if (locale === undefined) throw new Error(`${method} was asked for the host's locale`);
                  return real.call(this, locale, options);
                };
              }
            }

            """;
        var program = $"import {{ $eq }} from '{url}';\n{poison}console.log(JSON.stringify((() => {js})()));";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(statements);
        actual.Should().Be(expected,
            "a page with no culture installed is in the invariant culture, and asks nothing of the host");
    }

    /// <summary>
    /// A case that names a culture runs in it on both sides: the .NET side as a thread in it, the
    /// browser side as a page booted in it, with the data the server writes for it.
    /// </summary>
    [SkippableTheory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("ar")]
    public void ACaseThatNamesACulture_RunsInItOnBothSides(string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "return (1234.5).ToString(\"N2\") + \"|\" + (-1.5).ToString(\"G\") + \"|\" "
            + "+ new DateTime(2026, 9, 24).ToString(\"d\");",
            culture: culture);
    }
}
