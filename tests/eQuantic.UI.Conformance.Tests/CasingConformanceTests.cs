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
/// The runtime cases as .NET 10 does over ICU 78, which is Unicode 17, Ubuntu 26.04's, where
/// <c>ubuntu-latest</c> runs, and over macOS 26's (#1032). On a host whose ICU is older, Ubuntu 24.04's or
/// Windows' own, .NET leaves the letters Unicode 16 and 17 cased as they are, so there the cases that read
/// every code point leave those letters out and still compare every other one, and the case of a letter a
/// later Unicode cased is skipped rather than held against a Unicode the runtime has left. On a host
/// whose ICU moves past Unicode 17 they fail, which is when the runtime has to move.
/// </para>
/// </summary>
public class CasingConformanceTests
{
    public static readonly string?[] Cultures = [null, "en-US", "pt-BR", "de-DE", "tr-TR", "az-Latn-AZ", "lt-LT", "el-GR"];

    private const string Invariant = "System.Globalization.CultureInfo.InvariantCulture";
    private const string Current = "System.Globalization.CultureInfo.CurrentCulture";

    /// <summary>Whether this host's ICU cases by a Unicode older than the 17 the runtime follows: it leaves
    /// the Beria Erfe capitals, which Unicode 17 cased, as they are.</summary>
    private static readonly bool CasesBehindTheRuntime =
        char.ConvertFromUtf32(0x16EA0).ToLowerInvariant() == char.ConvertFromUtf32(0x16EA0);

    /// <summary>On a host whose ICU is older, the code units of the case pairs Unicode 16 and 17 added,
    /// which every case below leaves out there; nothing on a host whose ICU has them.</summary>
    private static readonly string NewerUnits = CasesBehindTheRuntime
        ? "if (unit == 0x19b || unit == 0x264 || unit == 0x1c89 || unit == 0x1c8a || unit >= 0xa7cb && unit <= 0xa7cf "
          + "|| unit >= 0xa7d2 && unit <= 0xa7d5 || unit >= 0xa7da && unit <= 0xa7dc) continue; "
        : "";

    /// <summary>The same for the code points of the first supplementary plane: Garay and Beria Erfe.</summary>
    private static readonly string NewerPoints = CasesBehindTheRuntime
        ? "if (point >= 0x10d50 && point <= 0x10d85 || point >= 0x16ea0 && point <= 0x16ed3) continue; "
        : "";

    /// <summary>A digest of what a casing does to every code unit of the BMP: how many it changes, and a
    /// hash of each one changed with what it became. Two numbers that differ if any one of the 65,536
    /// is cased otherwise.</summary>
    private static string EveryCodeUnit(string upper, string lower) =>
        "var s = \"\"; "
        + "for (var way = 0; way < 2; way++) { var count = 0; var hash = 0; "
        + "for (var unit = 0; unit < 65536; unit++) { " + NewerUnits + "var c = (char)unit; var text = c.ToString(); "
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

    /// <summary>The cases of a letter a later Unicode cased, whose answer is the Unicode of the host's ICU.</summary>
    private static readonly string[] LaterUnicode =
    [
        // Unicode 16's capital of a letter an older ICU does not case, its Garay, and Unicode 17's Beria
        // Erfe, as code units: the harness's JSON spells a character past the BMP one way on each side.
        "var s = \"\\u019b\\u0264\".ToUpper() + \"\\ua7dc\\ua7cb\".ToLower() + char.ConvertFromUtf32(0x16ebb).ToUpper() "
            + "+ char.ConvertFromUtf32(0x10d50).ToLowerInvariant(); var r = \"\"; "
            + "for (var k = 0; k < s.Length; k++) r += (int)s[k] + \",\"; return r;",
    ];

    /// <summary>The cases that read every code point, which leave out the letters a later Unicode cased on a
    /// host whose ICU is older.</summary>
    private static readonly string[] EveryCodePoint =
    [
        // Every code unit, through the string's casing and the char's.
        EveryCodeUnit("text.ToUpper()", "text.ToLower()"),
        EveryCodeUnit("text.ToUpperInvariant()", "text.ToLowerInvariant()"),
        EveryCodeUnit("char.ToUpper(c).ToString()", "char.ToLower(c).ToString()"),
        EveryCodeUnit("char.ToUpperInvariant(c).ToString()", "char.ToLowerInvariant(c).ToString()"),
        // Every code point of the first supplementary plane, which holds the cased scripts past the BMP.
        "var s = \"\"; for (var way = 0; way < 2; way++) { var count = 0; var hash = 0; "
            + "for (var point = 0x10000; point < 0x20000; point++) { " + NewerPoints + "var text = char.ConvertFromUtf32(point); "
            + "var changed = way == 0 ? text.ToUpper() : text.ToLower(); if (changed == text) continue; "
            + "count++; hash = (hash * 31 + point % 1000003) % 1000003; "
            + "for (var k = 0; k < changed.Length; k++) hash = (hash * 31 + changed[k]) % 1000003; } "
            + "s += count + \":\" + hash + \"|\"; } return s;",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() => InEveryCulture(Cases);

    public static IEnumerable<object?[]> EveryCodePointInEveryCulture() => InEveryCulture(EveryCodePoint);

    public static IEnumerable<object?[]> LaterUnicodeInEveryCulture() => InEveryCulture(LaterUnicode);

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
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }

    [SkippableTheory]
    [MemberData(nameof(LaterUnicodeInEveryCulture))]
    public void ALetterALaterUnicodeCased_ChangesCaseAsDotNetDoesOverIcu78(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Skip.If(CasesBehindTheRuntime, "This host's ICU cases by a Unicode older than the 17 the runtime follows.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
