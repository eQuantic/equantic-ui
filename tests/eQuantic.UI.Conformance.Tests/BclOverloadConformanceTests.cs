using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The overloads the BCL surface audit reached once it probed a static surface by name AND ARITY,
/// from the first overload of each arity it can speak (<c>BclSurfaceAuditTests</c>). By name alone
/// it probed the shortest overload and graded the family by it, and <c>char.IsLetter(s, i)</c>
/// answered false on the web for as long as <c>char.IsLetter(c)</c> was native.
/// <para>
/// Every line the regrouping added to <c>bcl-surface.baseline.txt</c> as <c>native</c> or
/// <c>eq</c> is a CLAIM that the translation behaves as .NET does, and a verdict only says that a
/// strategy answered. So each runs here on both sides, with the values its semantics turn on: a
/// null, an index past the end, a base's two's complement, an empty sequence. A section names the
/// baseline lines it proves. An exception is compared by its message where .NET's is one line;
/// an <c>ArgumentOutOfRangeException</c> writes its actual value after the host's newline, so
/// there only the throw is compared.
/// </para>
/// </summary>
public class BclOverloadConformanceTests
{
    /// <summary>
    /// Char.IsControl, IsDigit, IsLetter, IsLetterOrDigit, IsLower, IsNumber, IsPunctuation,
    /// IsSeparator, IsSymbol, IsUpper, IsWhiteSpace and GetUnicodeCategory, each (String,Int32): the
    /// character AT the index, and a surrogate pair there read as the one code point it is.
    /// </summary>
    [SkippableTheory]
    [InlineData("return char.IsControl(\"a\\tb\", 1);")]                    // true
    [InlineData("return char.IsControl(\"ab\", 1);")]                       // false
    [InlineData("return char.IsDigit(\"a\\U0001D7CE\", 1);")]               // true: MATHEMATICAL BOLD DIGIT ZERO, a pair
    [InlineData("return char.IsLetterOrDigit(\"-7\", 1);")]                 // true
    [InlineData("return char.IsLetterOrDigit(\"a-\", 1);")]                 // false
    [InlineData("return char.IsLetterOrDigit(\"-\\U0001D400\", 1);")]       // true: a letter in a pair
    [InlineData("return char.IsLower(\"Ab\", 1);")]                         // true
    [InlineData("return char.IsLower(\"aB\", 1);")]                         // false
    [InlineData("return char.IsLower(\"A\\U0001D41A\", 1);")]               // true: MATHEMATICAL BOLD SMALL A
    [InlineData("return char.IsNumber(\"a\\u00BD\", 1);")]                  // true: one half, No
    [InlineData("return char.IsNumber(\"a\\u2163\", 1);")]                  // true: Roman four, Nl
    [InlineData("return char.IsNumber(\"ab\", 1);")]                        // false
    [InlineData("return char.IsPunctuation(\"a!\", 1);")]                   // true
    [InlineData("return char.IsPunctuation(\"a+\", 1);")]                   // false
    [InlineData("return char.IsPunctuation(\"a\\U00010100\", 1);")]         // true: AEGEAN WORD SEPARATOR LINE, a pair
    [InlineData("return char.IsSeparator(\"a b\", 1);")]                    // true
    [InlineData("return char.IsSeparator(\"a\\u2028\", 1);")]               // true: LINE SEPARATOR
    [InlineData("return char.IsSeparator(\"a\\tb\", 1);")]                  // false: a tab is a control
    [InlineData("return char.IsSymbol(\"a+\", 1);")]                        // true
    [InlineData("return char.IsSymbol(\"a$\", 1);")]                        // true
    [InlineData("return char.IsSymbol(\"a\\U0001F600\", 1);")]              // true: an emoji, a pair
    [InlineData("return char.IsSymbol(\"\\U0001F600\", 1);")]               // false: the LOW half is read alone
    [InlineData("return char.IsUpper(\"a\\U0001D400\", 1);")]               // true
    [InlineData("return char.IsLetter(\"\\U0001D400\", 1);")]               // false: the low half again
    [InlineData("return char.IsWhiteSpace(\"a\\u2028\", 1);")]              // true
    [InlineData("return char.IsWhiteSpace(\"a\\u0085\", 1);")]              // true: NEXT LINE is .NET white space
    [InlineData("return char.IsWhiteSpace(\"a\\uFEFF\", 1);")]              // false: a byte order mark is not
    [InlineData("return char.IsWhiteSpace('\\u0085');")]                    // true: the char overload reads the same set
    [InlineData("return char.IsWhiteSpace('\\uFEFF');")]                    // false
    [InlineData("return (int)char.GetUnicodeCategory(\"x\\U0001F600\", 2);")] // 16, Surrogate: the low half alone
    [InlineData("return (int)char.GetUnicodeCategory(\"x\" + '\\uD83D', 1);")] // 16: a high half with nothing after it
    public void CharAtAnIndex_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Double.Log, Math.Log and MathF.Log (value, newBase); Double.Round and MathF.Round with digits,
    /// and with digits and a mode; Math.Round(Decimal,Int32) and (Decimal,Int32,MidpointRounding).
    /// </summary>
    [SkippableTheory]
    [InlineData("return double.Log(8.0, 2.0);")]                            // 3
    [InlineData("return double.Log(1000.0, 10.0);")]                       // 3
    [InlineData("return Math.Log(10.0, 3.0);")]                             // 2.095903274289385
    [InlineData("return Math.Log(8.0, 0.5);")]                              // -3
    [InlineData("return 1.0 / Math.Log(1.0, 0.0) < 0;")]                    // true: 0 over -Infinity is -0
    [InlineData("return 1.0 / Math.Log(1.0, double.PositiveInfinity) > 0;")] // true
    [InlineData("return double.IsNaN(Math.Log(double.NaN, 1.0));")]         // true
    [InlineData("return double.IsNaN(Math.Log(-8.0, 2.0));")]               // true
    [InlineData("return double.IsNaN(Math.Log(2.0, -2.0));")]               // true
    [InlineData("return double.IsNegativeInfinity(Math.Log(0.0, 2.0));")]   // true
    [InlineData("return (double)MathF.Log(10f, 3f);")]                      // 2.0959031581878662: in singles
    [InlineData("return (double)MathF.Log(1000f, 10f);")]                   // 3
    [InlineData("return double.Round(2.345, 2);")]                          // 2.35
    [InlineData("return double.Round(2.675, 2);")]                          // 2.68
    [InlineData("return double.Round(0.125, 2);")]                          // 0.12: an exact midpoint goes to even
    [InlineData("return double.Round(0.375, 2);")]                          // 0.38
    [InlineData("return double.Round(123.456, 15);")]                       // 123.456
    [InlineData("return double.Round(1e17, 2) == 1e17;")]                   // true: past the limit, unchanged
    [InlineData("return double.Round(1.23456789012345678, 14);")]
    [InlineData("return 1.0 / double.Round(-0.4, 0) < 0;")]                 // true: -0 keeps its sign
    [InlineData("try { return double.Round(1.0, -1); } catch { return -1.0; }")] // -1
    [InlineData("return double.Round(1.005, 2, MidpointRounding.AwayFromZero);")]        // 1: 100.49999999999999 underneath
    [InlineData("return double.Round(2.5, 0, MidpointRounding.AwayFromZero);")]          // 3
    [InlineData("return double.Round(-2.5, 0, MidpointRounding.AwayFromZero);")]         // -3
    [InlineData("return double.Round(2.45, 1, MidpointRounding.ToZero);")]               // 2.4
    [InlineData("return double.Round(-2.41, 1, MidpointRounding.ToNegativeInfinity);")]  // -2.5
    [InlineData("return double.Round(2.41, 1, MidpointRounding.ToPositiveInfinity);")]   // 2.5
    [InlineData("return Math.Round(-2.345m, 2).ToString();")]               // "-2.34"
    [InlineData("return Math.Round(1.5m, 5).ToString();")]                  // "1.5": digits past the scale keep it
    [InlineData("return Math.Round(79228162514264337593543950335m, 2).ToString();")] // decimal.MaxValue, unchanged
    [InlineData("try { return Math.Round(1m, 29).ToString(); } catch { return \"threw\"; }")]  // "threw"
    [InlineData("try { return Math.Round(1m, -1).ToString(); } catch { return \"threw\"; }")]  // "threw"
    [InlineData("return Math.Round(0.125m, 2, MidpointRounding.AwayFromZero).ToString();")]   // "0.13"
    [InlineData("return Math.Round(2.5m, 0, MidpointRounding.ToEven).ToString();")]           // "2"
    [InlineData("return Math.Round(-0.5m, 0, MidpointRounding.ToZero).ToString();")]          // "0"
    [InlineData("return (double)MathF.Round(0.125f, 2);")]                  // 0.11999999731779099
    [InlineData("return (double)MathF.Round(1e9f, 2);")]                    // 1000000000
    [InlineData("return (double)MathF.Round(2.345f, 2, MidpointRounding.AwayFromZero);")]        // 2.3499999046325684
    [InlineData("return (double)MathF.Round(-2.5f, 0, MidpointRounding.ToEven);")]               // -2
    [InlineData("return (double)MathF.Round(2.45f, 1, MidpointRounding.ToZero);")]               // 2.4000000953674316
    [InlineData("return (double)MathF.Round(3.14159f, 3, MidpointRounding.ToPositiveInfinity);")] // 3.1419999599456787
    public void LogAndRound_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// String.Compare (String,Int32,String,Int32,Int32), with a Boolean, and (String,String,Boolean);
    /// String.Concat (Object,Object), (Object,Object,Object) and (String,String,String,String);
    /// String.Equals (String,String,StringComparison); String.Format (String,Object,Object) and
    /// (String,Object,Object,Object); String.Join (Char,String[],Int32,Int32).
    /// </summary>
    [SkippableTheory]
    [InlineData("return string.Compare(\"xabc\", 1, \"abd\", 0, 2);")]     // 0: "ab" against "ab"
    [InlineData("return string.Compare(\"xabc\", 1, \"abd\", 0, 3);")]     // -1: "abc" against "abd"
    [InlineData("return string.Compare(\"ab\", 0, \"abc\", 0, 5);")]       // -1: the length clamps to each string
    [InlineData("return string.Compare(\"a\", 0, \"B\", 0, 1);")]          // -1: the culture's order, where ordinal is 1
    [InlineData("return string.Compare(\"abc\", 0, \"ABC\", 0, 3);")]      // -1: lower case first
    [InlineData("return string.Compare(null, 0, \"a\", 0, 0);")]           // -1: null first
    [InlineData("return string.Compare(\"a\", 0, null, 0, 0);")]           // 1
    [InlineData("return string.Compare(null, 0, null, 0, 0);")]            // 0
    [InlineData("return string.Compare(\"ab\", 2, \"ab\", 0, 1);")]        // -1: an empty range at the end
    [InlineData("try { return string.Compare(\"ab\", 3, \"ab\", 0, 1); } catch { return 99; }")]   // 99: past the end
    [InlineData("try { return string.Compare(\"ab\", -1, \"ab\", 0, 1); } catch { return 99; }")]  // 99
    [InlineData("try { return string.Compare(\"ab\", 0, \"ab\", 0, -1); } catch { return 99; }")]  // 99
    [InlineData("try { return string.Compare(null, 0, \"a\", 0, 1); } catch { return 99; }")]      // 99: a null has no range
    [InlineData("return string.Compare(\"xABc\", 1, \"abD\", 0, 2, true);")]  // 0
    [InlineData("return string.Compare(\"xABc\", 1, \"abD\", 0, 3, true);")]  // -1
    [InlineData("return string.Compare(\"xABc\", 1, \"abD\", 0, 2, false);")] // 1
    [InlineData("return string.Compare(\"a\", \"A\", true);")]             // 0
    [InlineData("return string.Compare(\"a\", \"A\", false);")]            // -1
    [InlineData("return string.Compare(\"B\", \"a\", true);")]             // 1
    [InlineData("return string.Compare(\"\\u00E9\", \"E\", true);")]       // 1: an accent is not a case
    [InlineData("return string.Compare(null, \"a\", true);")]              // -1
    [InlineData("return string.Compare(\"a\", null, false);")]             // 1
    [InlineData("bool ignore = true; return string.Compare(\"a\", \"A\", ignore);")] // 0: the flag is a value
    [InlineData("return string.Compare(\"ab\", \"a\\u00ADb\", false);")]   // 0: a soft hyphen is ignorable
    // The siblings of the same arity, which share the translation: a StringComparison in the flag's
    // place, and over two ranges. An ordinal comparison answers the DIFFERENCE, as .NET's does.
    [InlineData("return string.Compare(\"a\", \"c\", StringComparison.Ordinal);")]           // -2
    [InlineData("return string.Compare(\"abc\", \"ab\", StringComparison.Ordinal);")]        // 1: the lengths
    [InlineData("return string.Compare(\"a\", \"C\", StringComparison.OrdinalIgnoreCase);")] // -2
    [InlineData("return string.Compare(\"\\u00E9\", \"E\", StringComparison.OrdinalIgnoreCase);")] // 132
    [InlineData("return string.Compare(\"a\", \"B\", StringComparison.InvariantCulture);")]  // -1
    [InlineData("return string.Compare(null, \"a\", StringComparison.Ordinal);")]              // -1
    [InlineData("try { return string.Compare(\"a\", \"a\", (StringComparison)99).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return string.Compare(\"xabc\", 1, \"abd\", 0, 3, StringComparison.Ordinal);")]    // -1
    [InlineData("return string.Compare(null, 0, \"a\", 0, 5, StringComparison.Ordinal);")]            // -1: a null answers first
    [InlineData("return string.Compare(\"ab\", 2, \"cd\", 0, 0, StringComparison.Ordinal);")]       // 0
    [InlineData("return string.Compare(\"a\", 0, \"A\", 0, 1, StringComparison.OrdinalIgnoreCase);")] // 0
    [InlineData("try { return string.Compare(\"ab\", 3, \"ab\", 0, 0, StringComparison.Ordinal).ToString(); } catch (Exception e) { return e.Message; }")]
    // A surrogate pair ignores case as the code point it encodes: Deseret's two cases share their
    // high surrogate, a pair orders after any unit, and the difference is of the code points.
    [InlineData("return string.Compare(\"\\U00010400\", \"\\U00010428\", StringComparison.OrdinalIgnoreCase);")] // 0
    [InlineData("return string.Equals(\"\\U00010428\\U00010429\", \"\\U00010400\\U00010401\", StringComparison.OrdinalIgnoreCase);")] // true
    [InlineData("return string.Compare(\"\\U00010428\", \"\\U0001E922\", StringComparison.OrdinalIgnoreCase);")] // -58624
    [InlineData("return string.Compare(\"\\U00010428\", \"\\uFFFF\", StringComparison.OrdinalIgnoreCase);")] // 1: where the units say -10238
    [InlineData("return string.Compare(\"\\uFFFF\", \"\\U00010428\", StringComparison.OrdinalIgnoreCase);")] // -1
    [InlineData("return string.Compare(\"\\U00010428\", '\\uD801' + \"x\", StringComparison.OrdinalIgnoreCase);")] // 1: a lone high surrogate is no pair
    [InlineData("var high = '\\uD801'; return string.Compare(high + \"\\U00010428\", high + \"\\U00010400\", StringComparison.OrdinalIgnoreCase);")] // 0
    [InlineData("return string.Compare(\"\\U00010428xy\", \"\\U00010400X\", StringComparison.OrdinalIgnoreCase);")] // 1: the lengths
    [InlineData("return string.Compare(\"\\U00010428\", 0, \"\\U00010400\", 0, 1, StringComparison.OrdinalIgnoreCase);")] // 0: the range cuts the pair
    [InlineData("return string.Compare(\"x\\U00010428\", 1, \"y\\U00010400\", 1, 2, StringComparison.OrdinalIgnoreCase);")] // 0
    // The overloads graded before the regrouping, which the same change reached: the two-argument
    // Compare read a null as a receiver, and CompareOrdinal answered a sign and could not order a null.
    [InlineData("return string.Compare(null, \"a\");")]                  // -1
    [InlineData("return string.Compare(\"b\", \"a\");")]                // 1
    [InlineData("return string.CompareOrdinal(\"a\", \"c\");")]         // -2
    [InlineData("return string.CompareOrdinal(\"abc\", \"ab\");")]      // 1
    [InlineData("return string.CompareOrdinal(null, \"a\");")]            // -1
    [InlineData("return string.CompareOrdinal(\"a\", null);")]            // 1
    [InlineData("return string.CompareOrdinal(null, null);")]               // 0
    [InlineData("return string.Concat(1, 2);")]                            // "12", where JavaScript adds
    [InlineData("return string.Concat((object)\"a\", null);")]             // "a"
    [InlineData("return string.Concat(true, 1.5);")]                       // "True1.5"
    [InlineData("return string.Concat(0.1 + 0.2, 'x');")]                  // "0.30000000000000004x"
    [InlineData("return string.Concat(1e21, -0.0);")]                      // "1E+21-0"
    [InlineData("object o = null; return string.Concat(o, 'a', false);")]  // "aFalse"
    [InlineData("return string.Concat(1, 2, 3);")]                         // "123"
    [InlineData("string n = null; return string.Concat(\"a\", n, \"b\", n);")] // "ab"
    [InlineData("return string.Concat(\"a\", \"b\", \"c\", \"d\");")]      // "abcd"
    [InlineData("return string.Equals(null, null, StringComparison.OrdinalIgnoreCase);")]   // true
    [InlineData("return string.Equals(null, \"a\", StringComparison.OrdinalIgnoreCase);")]  // false
    [InlineData("return string.Equals(\"a\", null, StringComparison.Ordinal);")]            // false
    [InlineData("var c = StringComparison.OrdinalIgnoreCase; return string.Equals(\"a\", \"A\", c);")] // true: the comparison is a value
    [InlineData("return string.Equals(\"a\", \"A\", StringComparison.CurrentCultureIgnoreCase);")]    // true
    [InlineData("return string.Equals(\"\\u00E9\", \"E\", StringComparison.InvariantCultureIgnoreCase);")] // false
    [InlineData("return string.Equals(\"a\\u0301\", \"\\u00E1\", StringComparison.InvariantCulture);")]   // true: canonically equal
    [InlineData("return string.Equals(\"a\\u0301\", \"\\u00E1\", StringComparison.Ordinal);")]            // false
    [InlineData("return string.Equals(\"\\u212A\", \"k\", StringComparison.OrdinalIgnoreCase);")] // false: the Kelvin sign is not a k
    [InlineData("return string.Equals(\"\\u017F\", \"s\", StringComparison.OrdinalIgnoreCase);")] // false: nor a long s an s
    [InlineData("return string.Equals(\"\\u00DF\", \"SS\", StringComparison.OrdinalIgnoreCase);")] // false
    [InlineData("return string.Equals(\"\\u00E9\", \"\\u00C9\", StringComparison.OrdinalIgnoreCase);")] // true
    [InlineData("try { return string.Equals(\"a\", \"a\", (StringComparison)99) ? \"equal\" : \"different\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("return string.Format(\"{1}{0}\", \"a\", \"b\");")]        // "ba"
    [InlineData("return string.Format(\"{{{0}}}\", 1, 2);")]               // "{1}"
    [InlineData("return string.Format(\"{0:F2}|{1:D3}\", 1.5, 7);")]       // "1.50|007"
    [InlineData("return string.Format(\"{0}{1}\", null, \"x\");")]         // "x"
    [InlineData("return string.Format(\"{0}{1}{2}\", 1, \"b\", 'c');")]    // "1bc"
    [InlineData("return string.Format(\"{2}-{0}\", 1, 2, 3);")]            // "3-1"
    [InlineData("try { return string.Format(\"{2}\", 1, 2); } catch (Exception e) { return e.Message; }")] // past the values: FormatException
    [InlineData("return string.Join(',', new[] { \"a\", \"b\", \"c\" }, 1, 2);")]  // "b,c"
    [InlineData("return string.Join('-', new[] { \"a\", \"b\" }, 0, 0);")]         // ""
    [InlineData("return string.Join('-', new[] { \"a\", null, \"c\" }, 0, 3);")]   // "a--c"
    [InlineData("return string.Join(',', new[] { \"a\", \"b\" }, 2, 0);")]         // "": an empty range at the end
    [InlineData("try { return string.Join(',', new[] { \"a\" }, 1, 1); } catch { return \"threw\"; }")]       // "threw"
    [InlineData("try { return string.Join(',', new[] { \"a\", \"b\" }, -1, 1); } catch { return \"threw\"; }")] // "threw"
    [InlineData("try { return string.Join(',', new[] { \"a\", \"b\" }, 0, -1); } catch { return \"threw\"; }")] // "threw"
    [InlineData("return string.Join(\", \", new[] { \"a\", \"b\", \"c\" }, 1, 2);")] // "b, c": the string separator's sibling
    public void StringStatics_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// TimeSpan.FromDays (Int32,Int32,Int64,Int64,Int64,Int64), FromHours (Int32,Int64,Int64,Int64,
    /// Int64), FromMinutes (Int64,Int64,Int64,Int64), FromSeconds (Int64,Int64,Int64) and
    /// FromMilliseconds (Int64,Int64): every component counts, each may be negative, and the span
    /// they add up to is checked against the range a span holds. FromMicroseconds (Int64) was
    /// graded before the regrouping and answered with a member the twin did not have.
    /// </summary>
    [SkippableTheory]
    [InlineData("return TimeSpan.FromDays(1, 2, 3, 4, 5, 6).ToString();")]       // "1.02:03:04.0050060"
    [InlineData("return TimeSpan.FromDays(1, -25).ToString();")]                 // "-01:00:00"
    [InlineData("return TimeSpan.FromDays(-1, 0, 0, 0, 0, -1).ToString();")]     // "-1.00:00:00.0000010"
    [InlineData("return TimeSpan.FromHours(1, 30).ToString();")]                 // "01:30:00"
    [InlineData("return TimeSpan.FromHours(2, 3, 4, 5, 6).ToString();")]         // "02:03:04.0050060"
    [InlineData("return TimeSpan.FromHours(1, seconds: 5).ToString();")]         // "01:00:05": named, in its own slot
    [InlineData("return TimeSpan.FromMinutes(1, 2, 3, 4).ToString();")]          // "00:01:02.0030040"
    [InlineData("return TimeSpan.FromSeconds(1, 2, 3).ToString();")]             // "00:00:01.0020030"
    [InlineData("return TimeSpan.FromMilliseconds(1, 2).ToString();")]           // "00:00:00.0010020"
    [InlineData("return TimeSpan.FromMicroseconds(15).ToString();")]             // "00:00:00.0000150"
    [InlineData("return TimeSpan.FromMicroseconds(-15).ToString();")]            // "-00:00:00.0000150"
    [InlineData("return TimeSpan.FromDays(10675199, 2, 48, 5, 477, 580).ToString();")] // the last microsecond a span holds
    [InlineData("try { return TimeSpan.FromDays(10675199, 2, 48, 5, 477, 581).ToString(); } catch (Exception e) { return e.Message; }")] // one past it
    [InlineData("try { return TimeSpan.FromDays(int.MaxValue, 0).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return TimeSpan.FromSeconds(long.MaxValue, 0, 0).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int n = 0; int H() { n = n * 10 + 1; return 1; } long S() { n = n * 10 + 2; return 5; } var t = TimeSpan.FromHours(seconds: S(), hours: H()); return n + \" \" + t.ToString();")] // "21 01:00:05"
    // The one-unit overloads, graded before the regrouping: a double is read to the TICK and
    // truncated, as .NET 7 and later read it, where the twin rounded it to the millisecond.
    [InlineData("return TimeSpan.FromSeconds(0.00001).Ticks.ToString();")]      // "100"
    [InlineData("return TimeSpan.FromSeconds(-1.23456789).Ticks.ToString();")]  // "-12345678"
    [InlineData("return TimeSpan.FromMilliseconds(0.5).Ticks.ToString();")]     // "5000"
    [InlineData("return TimeSpan.FromMicroseconds(1.5).Ticks.ToString();")]     // "15"
    [InlineData("return TimeSpan.FromDays(1.5).ToString();")]                   // "1.12:00:00"
    [InlineData("return TimeSpan.FromSeconds(922337203685.0).Ticks.ToString();")] // an integral double: the product in doubles
    [InlineData("return TimeSpan.FromMinutes(15372286728.0).Ticks.ToString();")]
    [InlineData("return TimeSpan.FromMilliseconds(922337203685477.0).Ticks.ToString();")]
    [InlineData("return TimeSpan.FromDays(3).ToString();")]                     // "3.00:00:00": the int overload
    [InlineData("try { return TimeSpan.FromHours(double.NaN).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return TimeSpan.FromHours(1e20).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return TimeSpan.FromSeconds(922337203686L).ToString(); } catch (Exception e) { return e.Message; }")]
    public void TimeSpanComponents_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Convert.ToByte, ToSByte, ToInt16, ToUInt16, ToInt32, ToUInt32, ToInt64 and ToUInt64, each
    /// (String,Int32): the text read in base 2, 8, 10 or 16, a base other than 10 reading the bits
    /// of the type (so "ffffffff" is -1 for an int), with .NET's words when it refuses. And
    /// Convert.ToString (Byte,Int32), with its siblings of the same arity, which share the
    /// translation and write a negative number's bits in any base but 10.
    /// </summary>
    [SkippableTheory]
    [InlineData("return Convert.ToInt32(\"ff\", 16);")]                          // 255
    [InlineData("return Convert.ToInt32(\"0xFF\", 16);")]                        // 255: base 16 takes its prefix
    [InlineData("return Convert.ToInt32(\"FFFFFFFF\", 16);")]                    // -1: the bits of an int
    [InlineData("return Convert.ToInt32(\"7fffffff\", 16);")]                    // int.MaxValue
    [InlineData("return Convert.ToInt32(\"-12\", 10);")]                         // -12
    [InlineData("return Convert.ToInt32(\"+ff\", 16);")]                         // 255
    [InlineData("return Convert.ToInt32(\"777\", 8);")]                          // 511
    [InlineData("return Convert.ToInt32(\"1010\", 2);")]                         // 10
    [InlineData("return Convert.ToInt32(\"-2147483648\", 10);")]                 // int.MinValue
    [InlineData("string s = null; return Convert.ToInt32(s, 16);")]              // 0
    [InlineData("try { return Convert.ToInt32(\"-1\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"12\", 3).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("string s = null; try { return Convert.ToInt32(s, 3).ToString(); } catch (Exception e) { return e.Message; }")] // the base is read first
    [InlineData("try { return Convert.ToInt32(\"1g\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"g\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"0x\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\" 1\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"100000000\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"2147483648\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt32(\"\", 16).ToString(); } catch { return \"threw\"; }")]
    [InlineData("return Convert.ToByte(\"ff\", 16);")]                           // 255
    [InlineData("try { return Convert.ToByte(\"100\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToByte(\"-1\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToSByte(\"80\", 16);")]                          // -128
    [InlineData("return Convert.ToSByte(\"11111111\", 2);")]                     // -1
    [InlineData("return Convert.ToSByte(\"-128\", 10);")]                        // -128
    [InlineData("try { return Convert.ToSByte(\"128\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToSByte(\"100\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToSByte(\"80000000\", 16).ToString(); } catch (Exception e) { return e.Message; }")] // past the width, sign bit set
    [InlineData("try { return Convert.ToInt16(\"FFFFFFFF\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToInt16(\"ffff\", 16);")]                        // -1
    [InlineData("return Convert.ToInt16(\"-32768\", 10);")]                      // -32768
    [InlineData("try { return Convert.ToInt16(\"65535\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt16(\"10000\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToUInt16(\"ffff\", 16);")]                       // 65535
    [InlineData("try { return Convert.ToUInt16(\"10000\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToUInt32(\"ffffffff\", 16);")]                   // 4294967295
    [InlineData("return Convert.ToUInt32(\"4294967295\", 10);")]                 // 4294967295
    [InlineData("try { return Convert.ToUInt32(\"4294967296\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToUInt32(\"-1\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToInt64(\"ffffffffffffffff\", 16).ToString();")]      // "-1"
    [InlineData("return Convert.ToInt64(\"8000000000000000\", 16).ToString();")]      // long.MinValue
    [InlineData("return Convert.ToInt64(\"-9223372036854775808\", 10).ToString();")]
    [InlineData("return (Convert.ToInt64(\"7fffffffffffffff\", 16) - 1).ToString();")] // a long, and arithmetic on it
    [InlineData("try { return Convert.ToInt64(\"9223372036854775808\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt64(\"10000000000000000\", 16).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToUInt64(\"ffffffffffffffff\", 16).ToString();")]     // "18446744073709551615"
    [InlineData("return Convert.ToUInt64(\"18446744073709551615\", 10).ToString();")]
    [InlineData("try { return Convert.ToUInt64(\"18446744073709551616\", 10).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToString((byte)255, 16);")]                      // "ff"
    [InlineData("return Convert.ToString((byte)5, 2);")]                         // "101"
    [InlineData("return Convert.ToString((byte)8, 8);")]                         // "10"
    [InlineData("try { return Convert.ToString((byte)1, 3); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToString((short)-1, 16);")]                      // "ffff": a short's bits
    [InlineData("return Convert.ToString((short)-2, 2);")]                       // "1111111111111110"
    [InlineData("return Convert.ToString((short)-1, 8);")]                       // "177777"
    [InlineData("return Convert.ToString(-1, 2);")]                              // thirty-two ones
    [InlineData("return Convert.ToString(-255, 16);")]                           // "ffffff01"
    [InlineData("return Convert.ToString(-8, 8);")]                              // "37777777770"
    [InlineData("return Convert.ToString(-5, 10);")]                             // "-5"
    [InlineData("return Convert.ToString(int.MinValue, 16);")]                   // "80000000"
    [InlineData("return Convert.ToString(-1L, 16);")]                            // "ffffffffffffffff"
    [InlineData("return Convert.ToString(long.MinValue, 2);")]
    [InlineData("return Convert.ToString(long.MinValue, 10);")]                  // "-9223372036854775808"
    [InlineData("return Convert.ToString((sbyte)-1, 16);")]                      // "ffff": an sbyte widens to a short
    [InlineData("return Convert.ToString(uint.MaxValue, 16);")]                  // "ffffffff": a uint widens to a long
    public void ConvertBases_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Math.DivRem (Byte,Byte); Enumerable.Max/1 and Min/1, the selector overloads; ToDictionary/1
    /// and /2; GroupBy/2 and ToLookup/2, an element selector beside the key.
    /// </summary>
    [SkippableTheory]
    [InlineData("var (q, r) = Math.DivRem((byte)200, (byte)7); return q * 10 + r;")]           // 284
    [InlineData("var t = Math.DivRem((byte)255, (byte)16); return t.Quotient * 100 + t.Remainder;")] // 1515
    [InlineData("byte zero = 0; try { Math.DivRem((byte)1, zero); return 1; } catch { return -1; }")] // -1
    [InlineData("try { return new int[0].Max(x => x); } catch { return -1; }")]                // -1: empty throws
    [InlineData("try { return new int[0].Min(x => x); } catch { return -1; }")]                // -1
    [InlineData("try { return new double[0].Max(x => x); } catch { return -1.0; }")]           // -1
    [InlineData("try { return new int[0].Max(x => x).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return new[] { 1.0, double.NaN, 3.0 }.Max(x => x);")]                         // 3: Max passes over NaN
    [InlineData("return double.IsNaN(new[] { double.NaN, double.NaN }.Max(x => x));")]         // true: unless all are
    [InlineData("return double.IsNaN(new[] { 1.0, double.NaN, 3.0 }.Min(x => x));")]           // true: Min answers NaN
    [InlineData("return 1.0 / new[] { -0.0, 0.0 }.Max(x => x) < 0;")]                          // true: the first of equals
    [InlineData("return new[] { \"b\", \"a\", \"c\" }.Max(s => s);")]                          // "c"
    [InlineData("return new[] { \"a\", \"B\" }.Max(s => s);")]                                 // "B": the culture's order
    [InlineData("return new[] { \"a\", \"B\" }.Min(s => s);")]                                 // "a"
    [InlineData("return new[] { null, \"b\" }.Min(s => s);")]                                  // "b": a null is passed over
    [InlineData("return new string[0].Max(s => s);")]                                          // null: a reference type answers null
    [InlineData("return new int?[] { null, 2, 5 }.Max(x => x);")]                              // 5
    [InlineData("return new int?[] { null, 2, 5 }.Min(x => x);")]                              // 2
    [InlineData("return new int?[] { null }.Max(x => x);")]                                    // null
    [InlineData("return new int?[0].Min(x => x);")]                                            // null
    [InlineData("return new[] { 1L, 5L }.Max(x => x).ToString();")]                            // "5": a long
    [InlineData("return new[] { 1.5m, 2.5m }.Max(x => x).ToString();")]                        // "2.5": a decimal
    [InlineData("return new[] { 1.5m, 2.5m }.Min(x => x).ToString();")]                        // "1.5"
    [InlineData("return new[] { 'a', 'z', 'm' }.Max(c => c);")]                                // "z"
    [InlineData("return (double)new[] { 1.5f, 2.5f }.Max(x => x);")]                           // 2.5
    [InlineData("return new[] { \"bb\", \"a\", \"ccc\" }.Max(s => s.Length);")]                // 3
    [InlineData("return Enumerable.Max(new[] { 1, 3, 2 }, x => x * 2);")]                      // 6: the static form
    [InlineData("return Enumerable.Max(selector: x => x * 2, source: new[] { 1, 3, 2 });")]    // 6: named, each in its own place
    [InlineData("int n = 0; int[] S() { n = n * 10 + 1; return new[] { 1, 3 }; } Func<int, int> F() { n = n * 10 + 2; return x => x; } var m = Enumerable.Min(selector: F(), source: S()); return n * 10 + m;")] // 211: run as written
    [InlineData("return new double?[] { null, double.NaN, 1.0 }.Max(x => x);")]                // 1
    [InlineData("return double.IsNaN(new double?[] { null, double.NaN, 1.0 }.Min(x => x).Value);")] // true
    [InlineData("int n = 0; var m = new[] { 1.0, double.NaN, 3.0 }.Min(x => { n++; return x; }); return n;")] // 2: Min stops at the NaN
    [InlineData("return new[] { new DateTime(2020, 1, 1), new DateTime(2021, 1, 1) }.Max(d => d).Year;")] // 2021
    // The overloads without a selector, graded before the regrouping: the same translation.
    [InlineData("try { return new List<int>().Max(); } catch { return -1; }")]                 // -1
    [InlineData("return new[] { 1.0, double.NaN }.Max();")]                                    // 1
    [InlineData("return new[] { \"b\", \"a\" }.Max();")]                                     // "b"
    [InlineData("return new List<long> { 1, 5 }.Max().ToString();")]                           // "5"
    [InlineData("return new[] { 1.5m, 2.5m }.Min().ToString();")]                              // "1.5"
    [InlineData("return new[] { 1, 2, 3 }.ToDictionary(x => x)[2];")]                          // 2
    [InlineData("try { new[] { 1, 2, 1 }.ToDictionary(x => x); return \"added\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { new[] { 1, 2, 1 }.ToDictionary(x => x, x => x * 10); return \"added\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { new string[] { \"a\", null }.ToDictionary(s => s); return \"added\"; } catch (Exception e) { return e.Message; }")] // a null key
    [InlineData("var d = new[] { 1, 2 }.ToDictionary(x => x); return d.ContainsKey(2);")]      // true
    [InlineData("var d = new[] { 1, 2 }.ToDictionary(x => x); d[3] = 3; return d.Count;")]     // 3
    [InlineData("return new[] { \"a\", \"bb\" }.ToDictionary(s => s.Length, s => s)[2];")]     // "bb"
    [InlineData("return new[] { \"a\", \"bb\" }.ToDictionary(elementSelector: s => s, keySelector: s => s.Length)[2];")] // "bb": named, in its own place
    [InlineData("return new[] { 1, 2, 3 }.Aggregate(func: (a, b) => a * 10 + b, seed: 4);")]  // 4123: the seed is the seed
    [InlineData("return new[] { 1, 2 }.ToDictionary(x => x, x => x * 10).Values.Sum();")]      // 30
    [InlineData("return string.Join(\"|\", new[] { 1, 2, 3, 4 }.GroupBy(x => x % 2, x => x * 10).Select(g => g.Key + \":\" + string.Join(\",\", g)));")] // "1:10,30|0:20,40"
    [InlineData("var l = new[] { 1, 2, 3, 4 }.ToLookup(x => x % 2, x => x * 10); return string.Join(\",\", l[1]) + \"|\" + l[5].Count() + \"|\" + l.Count;")] // "10,30|0|2"
    [InlineData("var l = new[] { \"a\", \"bb\", \"cc\" }.ToLookup(s => s.Length, s => s.ToUpper()); return string.Join(\",\", l[2]);")] // "BB,CC"
    public void DivRemAndLinqOverloads_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// ToDictionary's keys where the dictionary's shape decides the answer: a key a plain object
    /// holds only as its own entry ("__proto__", which an assignment hands to the prototype's
    /// setter), and a structural key (a record), whose dictionary is a value map that compares keys
    /// by value, where a plain object wrote every record as the same "[object Object]".
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[] { \"__proto__\", \"a\" }.ToDictionary(s => s).Count;")]                       // 2
    [InlineData("var d = new[] { \"__proto__\" }.ToDictionary(s => s, s => 1); return d.ContainsKey(\"__proto__\");")] // true
    [InlineData("var d = new[] { \"__proto__\" }.ToDictionary(s => s, s => 7); return d[\"__proto__\"];")]  // 7
    [InlineData("try { new[] { \"__proto__\", \"__proto__\" }.ToDictionary(s => s); return \"added\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("var d = new[] { new Point(1, 2), new Point(3, 4) }.ToDictionary(p => p, p => p.X); return d[new Point(3, 4)] * 10 + d.Count;")] // 32
    [InlineData("return new[] { new Point(1, 2), new Point(3, 4) }.ToDictionary(p => p).ContainsKey(new Point(3, 4));")] // true
    [InlineData("try { new[] { new Point(1, 2), new Point(1, 2) }.ToDictionary(p => p); return \"added\"; } catch { return \"threw\"; }")] // "threw": equal by value
    // GroupBy and ToLookup with an element selector group by the key's VALUE where the key is an
    // object on this side (a record, a date, a decimal), and the lookup's indexer finds a group the
    // same way. They compared with ===, so two equal records were two groups.
    [InlineData("return new[] { new Point(1, 2), new Point(1, 2), new Point(3, 4) }.GroupBy(p => p, p => p.X).Count();")]         // 2
    [InlineData("return new[] { new Point(1, 2), new Point(1, 2), new Point(3, 4) }.ToLookup(p => p, p => p.X).Count;")]          // 2
    [InlineData("return new[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2) }.GroupBy(d => d, d => d.Day).Count();")]      // 1
    [InlineData("return new[] { new DateTime(2026, 1, 2), new DateTime(2026, 1, 2) }.ToLookup(d => d, d => d.Day).Count;")]       // 1
    [InlineData("return new[] { 1.5m, 1.50m }.GroupBy(m => m, m => m).Count();")]                                               // 1: one decimal, two scales
    [InlineData("var l = new[] { new Point(1, 2), new Point(3, 4) }.ToLookup(p => p, p => p.X); return l[new Point(3, 4)].Sum() * 10 + l[new Point(9, 9)].Count();")] // 30
    [InlineData("return string.Join(\",\", new[] { new Point(1, 2), new Point(1, 2) }.GroupBy(p => p, p => p.Y).Select(g => g.Key.X + \":\" + g.Sum()));")] // "1:4"
    public void ToDictionaryKeys_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "public record Point(int X, int Y);");
    }

    /// <summary>
    /// <c>Max</c> and <c>Min</c> over a type of the app's own, which order by the <c>CompareTo</c> it
    /// wrote: its twin carries it as <c>compareTo</c>. A type with no such method is refused at build
    /// time (LinqStrategyTests), since .NET's default comparer throws for it.
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[] { new Score(3), new Score(7), new Score(5) }.Max().Value;")]      // 7
    [InlineData("return new[] { new Score(3), new Score(7), new Score(5) }.Min().Value;")]      // 3
    [InlineData("return new[] { new Score(3), null, new Score(1) }.Min().Value;")]              // 1: a null is passed over
    [InlineData("return new Score[0].Max() == null;")]                                          // true: a reference type answers null
    [InlineData("return new[] { new Score(2), new Score(9) }.Max(s => s).Value;")]              // 9: the selector's form
    [InlineData("return Enumerable.Min(new List<Score> { new Score(4), new Score(-4) }).Value;")] // -4: the static form
    public void MaxMinOverAComparableType_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements,
            "public record Score(int Value) : IComparable<Score> { public int CompareTo(Score other) => other is null ? 1 : Value - other.Value; }");
    }
}
