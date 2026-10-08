using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>DateOnly</c>, a <c>TimeOnly</c> and a <c>DateTimeOffset</c> print as .NET prints them, through
/// the formatter a <c>DateTime</c> goes through (#469): their <c>ToString</c> reached each twin's own
/// <c>toString</c>, which knew six tokens, no specifier and no culture, and a provider crossed as a name
/// no browser defines. Measured on .NET 10: with no format a DateOnly is its culture's <c>d</c>, a
/// TimeOnly its <c>t</c>, and a DateTimeOffset its <c>G</c> with its offset; a DateOnly takes the
/// date's specifiers and a TimeOnly the time's, and refuses the rest; a DateTimeOffset's <c>o</c> and
/// <c>K</c> write its offset, and its <c>u</c> and <c>R</c> convert by it. A date in a concatenation or
/// a hole is its text in the culture too (#454).
/// </summary>
public class DateTypeTextConformanceTests
{
    private const string Day = "var d = new DateOnly(2026, 9, 24); ";
    private const string Time = "var t = new TimeOnly(22, 5, 15, 250); ";
    private const string Offset =
        "var o = new DateTimeOffset(new DateTime(2026, 9, 24, 10, 30, 15, 250), TimeSpan.FromHours(1)); ";

    private static readonly string[] Cases =
    [
        // A DateOnly, through every specifier it takes, a picture of it, and the strings by name.
        Day + "return $\"{d.ToString()}|{d.ToString(\"d\")}|{d.ToString(\"D\")}|{d.ToString(\"m\")}|{d.ToString(\"Y\")}|{d.ToString(\"o\")}|{d.ToString(\"R\")}\";",
        Day + "return $\"{d.ToString(\"dd/MM/yyyy\")}|{d.ToString(\"ddd, d MMM yyyy\")}|{d.ToString(\"yyyy gg\")}|{d.ToLongDateString()}|{d.ToShortDateString()}\";",
        // A TimeOnly, likewise.
        Time + "return $\"{t.ToString()}|{t.ToString(\"t\")}|{t.ToString(\"T\")}|{t.ToString(\"o\")}|{t.ToString(\"r\")}|{t.ToString(\"HH:mm:ss.fff\")}|{t.ToString(\"h:mm tt\")}|{t.ToLongTimeString()}|{t.ToShortTimeString()}\";",
        // A DateTimeOffset: its own clock, its offset where a form writes one, and UTC where one converts.
        Offset + "return $\"{o.ToString()}|{o.ToString(\"o\")}|{o.ToString(\"u\")}|{o.ToString(\"r\")}|{o.ToString(\"s\")}|{o.ToString(\"d\")}|{o.ToString(\"G\")}|{o.ToString(\"F\")}\";",
        "var o = new DateTimeOffset(2026, 9, 24, 1, 30, 15, TimeSpan.FromMinutes(-210)); "
            + "return $\"{o}|{o.ToString(\"o\")}|{o.ToString(\"u\")}|{o.ToString(\"r\")}|{o.ToString(\"zzz K\")}|{o.ToString(\"%z\")}|{o.ToString(\"zz\")}|{o.ToString(\"yyyy-MM-dd HH:mm:ss.fff zzz\")}\";",
        // Each type in a concatenation, a plain hole, an aligned hole and a composite placeholder.
        Day + Time + Offset + "var w = new DateTime(2026, 9, 24, 10, 30, 15); "
            + "return \"on \" + d + \" at \" + t + \"|\" + o + \"|\" + w + $\"|{d}|{t}|{o}|{w}|[{d,12}]\" + string.Format(\"|{0}|{1:T}|{2:D}\", d, t, o);",
        // A provider: the invariant culture writes the invariant patterns, the current one and a null
        // are the call with none.
        Day + Time + Offset + "return $\"{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{d.ToString(\"D\", System.Globalization.CultureInfo.InvariantCulture)}|{t.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{o.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{o.ToString(System.Globalization.CultureInfo.CurrentCulture)}|{d.ToString((IFormatProvider)null)}\";",
        // A null or an empty format is the type's own text, and a variable's is read when it runs.
        Day + Time + Offset + "string f = null; return $\"{d.ToString((string)null)}|{d.ToString(\"\")}|{t.ToString(\"\")}|{o.ToString((string)null)}|{t.ToString(f)}|{o.ToString(f)}\";",
        // A null one writes nothing.
        "DateOnly? n = null; TimeOnly? m = new TimeOnly(9, 5); DateTimeOffset? none = null; "
            + "return $\"[{n}]|[{n?.ToString(\"D\")}]|{m}|[{none}]|\" + m?.ToString(\"T\") + \"|\" + n.ToString();",
        // What the type does not take is .NET's FormatException.
        Day + "try { return d.ToString(\"t\"); } catch (Exception e) { return e.Message; }",
        Day + "try { return d.ToString(\"yyyy HH\"); } catch (Exception e) { return e.Message; }",
        Day + "try { return d.ToString(\"dd:MM\"); } catch (Exception e) { return e.Message; }",
        Time + "try { return t.ToString(\"d\"); } catch (Exception e) { return e.Message; }",
        Time + "try { return t.ToString(\"HH/mm\"); } catch (Exception e) { return e.Message; }",
        Offset + "try { return o.ToString(\"U\"); } catch (Exception e) { return e.Message; }",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements =>
            NumberTextInTheCultureConformanceTests.Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ADateTypesText_IsDotNets(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
