using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A string or a char keeps its value on its way into the module (#520), in every place the
/// transpiler spells one. A surrogate that is not half of a pair has no UTF-8 encoding, and a module
/// holding one could not be written: the build stopped with EQ0001, and this harness stopped before
/// Bun ran, which is why <c>BclOverloadConformanceTests</c> built its case as <c>"x" + '\\uD83D'</c>.
/// A char crossed in its C# spelling, which JavaScript reads its own way, and an interpolation's
/// format, a skipped parameter's default and <c>nameof</c> were quoted by hand.
/// <para>
/// Every case answers a number, a bool or ASCII text: the two sides' JSON spell a lone surrogate
/// differently (.NET's writer replaces it with U+FFFD, <c>JSON.stringify</c> escapes it in lower
/// case), so a string holding one is compared through what can be read of it.
/// </para>
/// </summary>
public class LiteralConformanceTests
{
    private const string Prelude =
        "public static class Halves { public const string High = \"x\\uD83D\"; public const char Low = '\\uDE00'; }";

    [SkippableTheory]
    // ---- a high half with nothing after it, and a low half with nothing before it ----
    [InlineData("return \"x\\uD83D\".Length;")]                                                         // 2
    [InlineData("return (int)\"x\\uD83D\"[1];")]                                                        // 55357
    [InlineData("return (int)char.GetUnicodeCategory(\"x\\uD83D\", 1);")]                               // 16, Surrogate
    [InlineData("return \"\\uDE00x\".Length;")]                                                         // 2
    [InlineData("return (int)\"\\uDE00x\"[0];")]                                                        // 56832
    [InlineData("return (int)char.GetUnicodeCategory(\"\\uDE00x\", 0);")]                               // 16
    [InlineData("var s = \"\\uDE00x\"; return char.IsLowSurrogate(s[0]) && !char.IsSurrogatePair(s, 0);")] // true
    // ---- a well-formed pair between the halves: the pair is one code point, each half is not ----
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return s.Length;")]                          // 4
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return char.IsSurrogatePair(s, 1);")]        // true
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return char.IsSurrogatePair(s, 0);")]        // false
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return char.ConvertToUtf32(s[1], s[2]);")]   // 128512
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return (int)char.GetUnicodeCategory(s, 1);")] // 28, OtherSymbol: the pair
    [InlineData("var s = \"\\uD83D\\uD83D\\uDE00\\uDE00\"; return (int)char.GetUnicodeCategory(s, 3);")] // 16: the last low half alone
    [InlineData("var s = \"x\\uD83D\" + \"\\uDE00\"; return char.ConvertToUtf32(s[1], s[2]);")]          // 128512: two literals' halves meet
    // ---- the same halves in an interpolated string's text, a folded constant and a char ----
    [InlineData("var n = 1; var s = $\"\\uD83D{n}\\uDE00\"; return $\"{s.Length}:{(int)s[0]}:{(int)s[2]}\";")] // "3:55357:56832"
    [InlineData("const string folded = \"x\" + \"\\uD83D\"; return (int)folded[1];")]                    // 55357
    [InlineData("return (int)'\\uD83D'.ToString()[0];")]                                                 // 55357
    public void ALoneSurrogate_KeepsItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A <c>const</c> of another type is inlined where it is read, through the same writer.</summary>
    [SkippableTheory]
    [InlineData("var s = Halves.High + Halves.Low; return char.ConvertToUtf32(s[1], s[2]);")]           // 128512
    [InlineData("return (int)Halves.High[1] + \":\" + (int)Halves.Low.ToString()[0];")]                  // "55357:56832"
    public void AnInlinedConstantsLoneSurrogate_KeepsItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>
    /// A char literal is written from its value. Its spelling was copied, and the escapes only C# has
    /// read their own way in JavaScript: <c>'\\a'</c> and <c>'\\e'</c> were <c>'a'</c> and <c>'e'</c>,
    /// <c>'\\U00000041'</c> nine characters, <c>'\\x041'</c> two, and <c>'\\x1'</c> a syntax error. A
    /// cast to a number folded the constant and was right all along, so each case keeps the char a char.
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = \"\\a\\e\"; return s[0] == '\\a' && s[1] == '\\e';")]                            // true
    [InlineData("return (int)'\\a'.ToString()[0] * 100 + (int)'\\e'.ToString()[0];")]                   // 727
    [InlineData("return \"\\u0001\"[0] == '\\x1';")]                                                     // true
    [InlineData("return \"A\"[0] == '\\x041' && \"A\"[0] == '\\U00000041';")]                            // true
    [InlineData("return \"A\" + '\\x041' + '\\U00000041';")]                                              // "AAA"
    [InlineData("var c = \"\\e\"[0]; switch (c) { case '\\e': return \"escape\"; case 'e': return \"letter\"; default: return \"other\"; }")] // "escape"
    [InlineData("var c = (char)7; return c is '\\a';")]                                                   // true
    public void ACharLiteral_IsItsValueAndNotItsSpelling(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// The strings the transpiler quoted by hand: an interpolation's format, the default a named
    /// argument skips, and <c>nameof</c>. <c>dd 'de' MMMM</c> closed the quotes it was written between
    /// and Bun refused the module; a default of <c>"it's"</c> did the same, and a char default was
    /// written with no quotes at all.
    /// </summary>
    [SkippableTheory]
    [InlineData("var d = new DateTime(2026, 10, 3); return $\"{d:dd 'de' MMMM}\";")]                      // "03 de October"
    [InlineData("return $\"{5:0'%'}\";")]                                                                 // "5%"
    [InlineData("string Join(string a = \"it's\", char c = ',', int b = 0) => a + c + b; return Join(b: 1);")] // "it's,1"
    [InlineData("string Join(string a = \"a\\\\b\\nc\", char c = '\\'', int b = 0) => a + c + b; return Join(b: 2);")] // "a\b(LF)c'2"
    [InlineData("var @class = 1; return nameof(@class);")]                                                 // "class"
    public void AStringQuotedByHand_KeepsItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    private const string Defaults =
        "public record Quoted(string A = \"it's\\n\\\\\", char C = '\\'', int B = 0);";

    /// <summary>A record's constructor skipped by a named argument fills the parameter from its
    /// default, on the creation path rather than the call's.</summary>
    [SkippableTheory]
    [InlineData("var q = new Quoted(B: 1); return q.A + q.C + q.B;")]                               // it's, a line feed, a backslash, a quote, 1
    [InlineData("return new Quoted(C: 'x').A.Length;")]                                            // 6
    public void ARecordsSkippedDefault_IsQuotedAsAnyString(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Defaults);
    }

    /// <summary>
    /// A raw interpolated string's braces are its text unless as many of them as it has dollars open
    /// a hole, so <c>{{</c> stays two braces there. Only a regular or a verbatim one reads a doubled
    /// brace as one, and the collapse ran on all of them.
    /// </summary>
    [SkippableTheory]
    [InlineData("var n = 1; return $$$\"\"\"a{{b}}c{{{n}}}\"\"\";")]                                       // "a{{b}}c1"
    [InlineData("var n = 1; return $$\"\"\"{a}{{n}}\"\"\";")]                                              // "{a}1"
    [InlineData("var n = 1; return $\"{{a}}{n}\";")]                                                      // "{a}1"
    [InlineData("var n = 1; return $@\"{{a}}{n}\";")]                                                     // "{a}1"
    public void ARawInterpolatedStringsBraces_AreItsText(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// The controls, the format characters and the separators are escaped in the module, and each is
    /// still the character C# holds: the short escapes, a NUL before a digit (which <c>\\0</c> cannot
    /// spell there), and U+2028 and U+2029 in both kinds of string.
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = \"a\\tb\\0c\\ad\\ee\\vf\\fg\\bh\\u007F\\u0085\"; var codes = \"\"; for (var i = 0; i < s.Length; i++) codes += (int)s[i] + \",\"; return codes;")]
    [InlineData("return \"\\u0000\" + 1 == \"\\u00001\" && (\"\\u00001\").Length == 2;")]                  // true
    [InlineData("var n = 1; var s = $\"\\u2028{n}\\u2029\"; return $\"{(int)s[0]}:{(int)s[2]}\";")]         // "8232:8233"
    [InlineData("var s = \"\\u2028\\u2029\\uFEFF\\u200D\\u00A0\\uE000\"; var codes = \"\"; for (var i = 0; i < s.Length; i++) codes += (int)s[i] + \",\"; return codes;")]
    public void AnInvisibleCharacter_KeepsItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
