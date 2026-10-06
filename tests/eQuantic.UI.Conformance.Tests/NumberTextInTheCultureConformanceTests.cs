using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A number's text with no specifier is the culture's, wherever C# writes it: a concatenation, a plain
/// or an aligned interpolation hole, <c>ToString()</c>, <c>string.Format("{0}")</c> and
/// <c>string.Concat</c> (#454). The server renders a component in the request's culture, and the
/// browser wrote the invariant text for every shape but <c>string.Format</c>, so the SSR markup and the
/// hydrated page printed the same value two ways: <c>1,5</c> and <c>1.5</c> in pt-BR, a minus sign the
/// culture spells (sv-SE's U+2212, <c>ar</c>'s with a left-to-right mark) and a hyphen. Measured on
/// .NET 10: every one of these shapes writes the culture's text, a negative integer's included, and
/// <c>ToString(CultureInfo.InvariantCulture)</c> is how a value written for a machine says so.
/// </summary>
public class NumberTextInTheCultureConformanceTests
{
    /// <summary>The cultures every case runs in: the invariant one, a comma-decimal one, one whose
    /// group separator is the other's point, one whose minus sign is not a hyphen, and one whose signs
    /// carry a direction mark.</summary>
    public static readonly string?[] Cultures = [null, "pt-BR", "de-DE", "sv-SE", "ar"];

    private static readonly string[] Cases =
    [
        // A double, in every shape C# writes one with no specifier.
        "double d = 1234.5, n = -1.5, z = -0.0, big = 1e21, small = -1e-5; "
            + "return $\"{d}|{n}|{z}|{big}|{small}\" + \"|\" + d + \"|\" + n + \"|\" + n.ToString() + \"|\" + string.Format(\"{0}\", n);",
        // What is not a finite number writes the culture's words.
        "return $\"{double.NaN}|{double.PositiveInfinity}|{double.NegativeInfinity}|\" + float.NegativeInfinity;",
        // A float, in its own digits.
        "float f = 0.1f, g = -2.5f; return \"f=\" + f + \"|\" + g + \"|\" + $\"[{f,8}][{g,-8}]\" + f.ToString() + \"|\" + (f + g);",
        // A decimal keeps its scale.
        "decimal m = 1234.50m, k = -0.5m; return $\"{m}|{k}|\" + m + \"|\" + k.ToString() + \"|\" + string.Concat(\"v=\", k);",
        // A negative integer of every signed width, and a long past a double's digits.
        "int i = -5; long l = -9007199254740993L; short s = -3; sbyte b = -1; "
            + "return $\"{i}|{l}|{s}|{b}|\" + i + \"|\" + l + \"|\" + i.ToString() + \"|\" + l.ToString() + \"|\" + (i * 2);",
        // An unsigned integer reads the same in every culture.
        "uint u = 7; byte y = 8; ushort h = 9; ulong w = 10; return $\"{u}|{y}|{h}|{w}|\" + u + y + \"|\" + w.ToString();",
        // An aligned hole pads the culture's text, and so does a composite placeholder.
        "double d = -1234.5; int i = -42; return $\"[{d,10}][{d,-10}][{i,5}]\" + string.Format(\"[{0,10}][{1,-5}]\", d, i);",
        // A nullable number writes its value's text, or nothing.
        "double? some = -1.5, none = null; int? count = -3; "
            + "return $\"{some}|{none}|{count}|\" + some + \"|\" + none + \"|\" + some.ToString() + \"|\" + none.ToString() + \"|\" + count;",
        // The current culture, or a null provider, is the call with none.
        "double d = -1.5; int i = -7; return d.ToString(System.Globalization.CultureInfo.CurrentCulture) + \"|\" "
            + "+ d.ToString((IFormatProvider)null) + \"|\" + i.ToString(System.Globalization.CultureInfo.CurrentCulture) + \"|\" "
            + "+ string.Concat(\"v=\", d, \";\", i);",
        // A value written for a MACHINE says so, and keeps its shape whoever reads the page.
        "double d = -1.5; return d.ToString(System.Globalization.CultureInfo.InvariantCulture) + \"px|\" "
            + "+ (-5).ToString(System.Globalization.CultureInfo.InvariantCulture) + \"|\" "
            + "+ 1.5m.ToString(System.Globalization.CultureInfo.InvariantCulture) + \"|\" "
            + "+ string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0}|{1}\", d, -5);",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ANumbersText_WithNoSpecifier_IsTheCulturesAsInDotNet(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
