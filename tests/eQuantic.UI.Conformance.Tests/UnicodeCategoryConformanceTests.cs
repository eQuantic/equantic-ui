using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A character's general category, executed on both sides: .NET's own table under
/// <c>CharUnicodeInfo.GetUnicodeCategory</c> and <c>char.GetUnicodeCategory</c>, and on the web the
/// platform's Unicode property escapes (<c>$eq.text.unicodeCategory</c>), cast to the number here
/// so the two answers compare as numbers. One character per category,
/// in the order of <c>UnicodeCategory</c>, plus the overloads: a code point, a char, and a string
/// with an index, which reads a surrogate pair whole. The code editor asks it to know a mark that
/// begins a text element, which has no advance of its own. The characters are old enough that no
/// Unicode version either platform carries disagrees about them.
/// </summary>
public class UnicodeCategoryConformanceTests
{
    [SkippableTheory]
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0041)")]     // Lu, A
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0061)")]     // Ll, a
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x01C5)")]     // Lt, Dz with caron
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x02B0)")]     // Lm, modifier h
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x05D0)")]     // Lo, alef
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0301)")]     // Mn, combining acute
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0903)")]     // Mc, Devanagari visarga
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x20DD)")]     // Me, enclosing circle
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0035)")]     // Nd, 5
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x2163)")]     // Nl, Roman four
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x00BD)")]     // No, one half
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0020)")]     // Zs, space
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x2028)")]     // Zl, line separator
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x2029)")]     // Zp, paragraph separator
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0009)")]     // Cc, tab
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x200B)")]     // Cf, zero-width space
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0xD800)")]     // Cs, a lone high surrogate
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0xE000)")]     // Co, private use
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x005F)")]     // Pc, underscore
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x002D)")]     // Pd, hyphen-minus
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0028)")]     // Ps, open parenthesis
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0029)")]     // Pe, close parenthesis
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x00AB)")]     // Pi, left guillemet
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x00BB)")]     // Pf, right guillemet
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0021)")]     // Po, exclamation mark
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x002B)")]     // Sm, plus
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0024)")]     // Sc, dollar
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x005E)")]     // Sk, circumflex
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x00A9)")]     // So, copyright
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x1F600)")]    // So, astral
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(0x0378)")]     // Cn, unassigned
    [InlineData("(int)char.GetUnicodeCategory('a')")]                                        // the char overload
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory('\\u0301')")]  // CharUnicodeInfo's char overload
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(\"x\\U0001F600\", 1)")] // a pair, read whole
    [InlineData("(int)char.GetUnicodeCategory(\"x\\U0001F600\", 1)")]                        // the char type's (string, index)
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(\"ab\", 1)")]  // a plain index
    [InlineData("(int)System.Globalization.CharUnicodeInfo.GetUnicodeCategory(index: 1, s: \"a1\")")]                                                                                         // named out of order
    public void UnicodeCategory_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
