using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A DateTime's own ToString prints as .NET prints it (#388). It reached the twin's
/// <c>toString(pattern)</c>, which knew custom tokens only, so <c>d.ToString("D")</c> printed
/// <c>D</c>, and <c>d.ToString("G", CultureInfo.InvariantCulture)</c> threw
/// <c>CultureInfo is not defined</c> in the browser. The formatter it now goes through wrote the
/// round-trip and sortable forms from <c>toISOString()</c>, UTC, so a page off UTC shifted the
/// hour, and it had no <c>R</c>, <c>u</c> or <c>U</c>. Both sides run on the same machine, so
/// <c>U</c>, which reads the value as local time, meets the same time zone.
/// </summary>
public class DateTimeToStringConformanceTests
{
    private const string Moment = "var d = new DateTime(2026, 9, 24, 10, 30, 15, 250); ";

    [SkippableTheory]
    // Every one-letter standard specifier, with no provider.
    [InlineData(Moment + "return $\"{d.ToString(\"d\")}|{d.ToString(\"D\")}|{d.ToString(\"f\")}|{d.ToString(\"F\")}|{d.ToString(\"g\")}|{d.ToString(\"G\")}\";")]
    [InlineData(Moment + "return $\"{d.ToString(\"m\")}|{d.ToString(\"M\")}|{d.ToString(\"t\")}|{d.ToString(\"T\")}|{d.ToString(\"y\")}|{d.ToString(\"Y\")}\";")]
    [InlineData(Moment + "return $\"{d.ToString(\"o\")}|{d.ToString(\"O\")}|{d.ToString(\"r\")}|{d.ToString(\"R\")}|{d.ToString(\"s\")}|{d.ToString(\"u\")}|{d.ToString(\"U\")}\";")]
    // With the invariant culture, the current culture and a null, which is the current one.
    // (Named where it is used: a culture read from a variable is EQ2108, which the harness, running
    // what it transpiles, would not report.)
    [InlineData(Moment + "return $\"{d.ToString(\"D\", System.Globalization.CultureInfo.InvariantCulture)}|{d.ToString(\"G\", System.Globalization.CultureInfo.InvariantCulture)}|{d.ToString(\"o\", System.Globalization.CultureInfo.InvariantCulture)}|{d.ToString(\"yyyy-MM-dd\", System.Globalization.CultureInfo.InvariantCulture)}\";")]
    [InlineData(Moment + "return $\"{d.ToString(\"D\", System.Globalization.CultureInfo.CurrentCulture)}|{d.ToString(\"g\", null)}\";")]
    // A provider alone is the call with none, the invariant one invariant.
    [InlineData(Moment + "return $\"{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{d.ToString(System.Globalization.CultureInfo.CurrentCulture)}|{d.ToString((IFormatProvider)null)}|{d.ToString()}\";")]
    // A custom picture, drawn token by token: the control, single-letter tokens, names, a
    // fraction of a second, and an `F` fraction that drops its point when it is all zeros.
    [InlineData(Moment + "return $\"{d.ToString(\"yyyy-MM-dd\")}|{d.ToString(\"d/M/yyyy\")}|{d.ToString(\"dd MMM yyyy\")}|{d.ToString(\"ddd, MMM d\")}|{d.ToString(\"h:mm tt\")}\";")]
    [InlineData(Moment + "var w = new DateTime(2026, 9, 24, 10, 30, 15); "
        + "return $\"{d.ToString(\"HH:mm:ss.fff\")}|{d.ToString(\"HH:mm:ss.FFF\")}|{w.ToString(\"HH:mm:ss.FFF\")}|{d.ToString(\"'at' HH\\\\:mm\")}|{d.ToString(\"%d\")}\";")]
    // The first year: `Date` read a year below 100 as 1900 plus it.
    [InlineData("return $\"{DateTime.MinValue.ToString(\"yyyy-MM-dd\")}|{DateTime.MinValue.ToString(\"o\")}|{new DateTime(50, 6, 1).ToString(\"D\")}\";")]
    // A null DateTime? writes nothing, and its conditional ToString is null.
    [InlineData("DateTime? n = null; return $\"[{n.ToString()}]|{n?.ToString(\"D\") ?? \"none\"}\";")]
    public void ADateTimesToString_PrintsAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
