using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A text and a char change case as .NET 10 changes them (#848), in the culture the call names (#725).
/// .NET changes each code unit, or surrogate pair, to exactly one other, by the simple mappings of the
/// host's ICU and the culture's rule for the two i's; the browser ran JavaScript's full mappings, which
/// lengthen <c>ß</c> to <c>SS</c>, read a final sigma in its context and know no culture.
/// Measured on .NET 10 in eight cultures: the invariant one, three whose casing is the common one, the
/// two whose i's differ (tr and az), and the two whose JavaScript locale casing differs from the plain
/// one (lt, el), which .NET does not follow.
/// <para>
/// The runtime follows the servers a page is rendered on, Linux's ICU 74 (Unicode 15.1) and Windows'
/// own, and the cases that read every code point, or a letter Unicode 16 cased, compare there. Apple's
/// ICU is the operating system's and moved first: .NET 10 on macOS 26 upper-cases <c>ƛ</c> to
/// <c>Ƛ</c>, so on a Mac whose ICU does, those cases are skipped rather than held against a casing no
/// server has. On Linux and Windows they fail when the host's ICU moves, which is when the runtime's
/// list has to.
/// </para>
/// </summary>
public class CasingConformanceTests
{
    public static readonly string?[] Cultures = [null, "en-US", "pt-BR", "de-DE", "tr-TR", "az-Latn-AZ", "lt-LT", "el-GR"];

    private const string Invariant = "System.Globalization.CultureInfo.InvariantCulture";
    private const string Current = "System.Globalization.CultureInfo.CurrentCulture";

    /// <summary>A digest of what a casing does to every code unit of the BMP: how many it changes, and a
    /// hash of each one changed with what it became. Two numbers that differ if any one of the 65,536
    /// is cased otherwise.</summary>
    private static string EveryCodeUnit(string upper, string lower) =>
        "var s = \"\"; "
        + "for (var way = 0; way < 2; way++) { var count = 0; var hash = 0; "
        + "for (var unit = 0; unit < 65536; unit++) { var c = (char)unit; var text = c.ToString(); "
        + $"var changed = way == 0 ? {upper} : {lower}; "
        + "if (changed == text) continue; count++; hash = (hash * 31 + unit) % 1000003; "
        + "for (var k = 0; k < changed.Length; k++) hash = (hash * 31 + changed[k]) % 1000003; } "
        + "s += count + \":\" + hash + \"|\"; } return s;";

    private static readonly string[] Cases =
    [
        // A mapping that lengthens is the simple one: the letter itself, or a capital with a
        // prosgegrammeni; a capital sigma lowers to the medial one wherever it stands.
        "return \"stra\\u00dfe\".ToUpper() + \"|\" + \"\\ufb01\".ToUpper() + \"|\" + \"\\u1fb3\\u1f80\".ToUpper() + \"|\" "
            + "+ \"\\u039f\\u0394\\u039f\\u03a3 \\u039f\\u0394\\u039f\\u03a3\".ToLower() + \"|\" + \"\\u0149\\u01f0\".ToUpper();",
        // The two i's are the culture's: tr and az have their own, the invariant culture keeps the
        // dotted capital and the dotless small as they are.
        "return \"i\\u0131I\\u0130\".ToUpper() + \"|\" + \"i\\u0131I\\u0130\".ToLower() + \"|\" "
            + "+ \"i\\u0131I\\u0130\".ToUpperInvariant() + \"|\" + \"i\\u0131I\\u0130\".ToLowerInvariant();",
        // A culture named is the one read; a null one is the current one for a string.
        "return \"i\\u0131\".ToUpper(" + Invariant + ") + \"|\" + \"I\\u0130\".ToLower(" + Current + ") + \"|\" "
            + "+ \"i\\u0131\".ToUpper(null) + \"|\" + \"I\\u0130\".ToLower(culture: " + Invariant + ");",
        // A char is cased as a string of one is, through every overload.
        "return char.ToUpper('\\u00df').ToString() + char.ToUpper('i') + char.ToLower('\\u0130') + char.ToUpperInvariant('\\u0131') "
            + "+ char.ToLowerInvariant('I') + char.ToUpper('i', " + Current + ") + char.ToLower('I', " + Invariant + ") "
            + "+ char.ToUpper('\\u1fb3') + char.ToLower('\\u03a3');",
        // A char's culture may not be null, and the char is evaluated before it is refused.
        "var log = \"\"; char C() { log += \"c\"; return 'a'; } "
            + "try { char.ToUpper(C(), null); } catch (ArgumentNullException e) { log += \"|\" + e.ParamName + \"|\" + e.Message; } "
            + "try { char.ToLower(C(), null); } catch (ArgumentNullException e) { log += \"|\" + e.ParamName; } return log;",
        // A surrogate pair is cased as its code point, and a lone surrogate is left as it is.
        "var s = \"\\ud801\\udc28x\\ud801\".ToUpper() + \"\\ud801\\udc00X\".ToLower(); var r = \"\"; "
            + "for (var k = 0; k < s.Length; k++) r += (int)s[k] + \",\"; return r;",
    ];

    /// <summary>The cases whose answer is the Unicode version of the host's ICU.</summary>
    private static readonly string[] EveryCodePoint =
    [
        // Unicode 16's capital of a letter the servers' ICU does not case.
        "return \"\\u019b\\u0264\".ToUpper() + \"|\" + \"\\ua7dc\\ua7cb\".ToLower();",
        // Every code unit, through the string's casing and the char's.
        EveryCodeUnit("text.ToUpper()", "text.ToLower()"),
        EveryCodeUnit("text.ToUpperInvariant()", "text.ToLowerInvariant()"),
        EveryCodeUnit("char.ToUpper(c).ToString()", "char.ToLower(c).ToString()"),
        EveryCodeUnit("char.ToUpperInvariant(c).ToString()", "char.ToLowerInvariant(c).ToString()"),
        // Every code point of the first supplementary plane, which holds the cased scripts past the BMP.
        "var s = \"\"; for (var way = 0; way < 2; way++) { var count = 0; var hash = 0; "
            + "for (var point = 0x10000; point < 0x20000; point++) { var text = char.ConvertFromUtf32(point); "
            + "var changed = way == 0 ? text.ToUpper() : text.ToLower(); if (changed == text) continue; "
            + "count++; hash = (hash * 31 + point % 1000003) % 1000003; "
            + "for (var k = 0; k < changed.Length; k++) hash = (hash * 31 + changed[k]) % 1000003; } "
            + "s += count + \":\" + hash + \"|\"; } return s;",
    ];

    /// <summary>Whether this host is a Mac whose ICU cases by a later Unicode than the servers'.</summary>
    private static readonly bool CasesAheadOfTheServers =
        OperatingSystem.IsMacOS() && "ƛ".ToUpperInvariant() != "ƛ";

    public static IEnumerable<object?[]> CasesInEveryCulture() => InEveryCulture(Cases);

    public static IEnumerable<object?[]> EveryCodePointInEveryCulture() => InEveryCulture(EveryCodePoint);

    private static IEnumerable<object?[]> InEveryCulture(string[] cases) =>
        cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ATextOrAChar_ChangesCaseAsDotNetDoes(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }

    [SkippableTheory]
    [MemberData(nameof(EveryCodePointInEveryCulture))]
    public void EveryCodePoint_ChangesCaseAsAServersDotNetDoes(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Skip.If(CasesAheadOfTheServers,
            "This Mac's ICU cases by Unicode 16, ahead of the Linux and Windows servers the runtime follows.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
