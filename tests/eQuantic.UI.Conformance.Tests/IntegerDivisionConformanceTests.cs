using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An integer division or remainder that .NET refuses, on both sides (#333). A zero divisor is a
/// DivideByZeroException and <c>MinValue / -1</c> an OverflowException on int and long, the
/// remainder included and whatever the context; the twin answered Infinity, NaN, 2147483648 or a
/// BigInt's RangeError. Each case compares .NET's own message, and the controls beside them divide
/// what .NET divides.
/// </summary>
public class IntegerDivisionConformanceTests
{
    [SkippableTheory]
    // ---- int: a zero divisor, and MinValue by -1 ----
    [InlineData("int zero = 0; try { return (5 / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int zero = 0; try { return (5 % zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int min = int.MinValue, minusOne = -1; try { return (min / minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int min = int.MinValue, minusOne = -1; try { return (min % minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int min = int.MinValue, minusOne = -1; try { return unchecked(min / minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int min = int.MinValue, minusOne = -1; try { return checked(min / minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int zero = 0; try { return checked(5 / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int min = int.MinValue; try { return (min / -1).ToString(); } catch (Exception e) { return e.Message; }")] // a constant -1 is checked too
    [InlineData("int min = int.MinValue; try { return (min % -1).ToString(); } catch (Exception e) { return e.Message; }")]
    // ---- the other widths a number carries ----
    [InlineData("uint zero = 0; try { return (5u / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("uint zero = 0; try { return (5u % zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("char c = 'A'; int zero = 0; try { return (c / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("sbyte s = 5, zero = 0; try { s /= zero; return s.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("sbyte s = sbyte.MinValue, minusOne = -1; s /= minusOne; return s.ToString();")]             // "-128" — int arithmetic, wrapped back
    [InlineData("short s = short.MinValue, minusOne = -1; return (s / minusOne).ToString();")]              // "32768" — an int
    // ---- long, a BigInt ----
    [InlineData("long zero = 0; try { return (5L / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long zero = 0; try { return (5L % zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long min = long.MinValue, minusOne = -1; try { return (min / minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long min = long.MinValue, minusOne = -1; try { return (min % minusOne).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("ulong zero = 0; try { return (5UL / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long x = 7, zero = 0; try { x /= zero; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long x = long.MinValue, minusOne = -1; try { x %= minusOne; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long x = long.MaxValue; return (x / 2L + x % 2L).ToString();")]                            // control
    // A long target divided by an int: the int converts to the long's BigInt before the helper.
    [InlineData("long x = 7; int d = 2; x /= d; return x.ToString();")]                                     // "3"
    [InlineData("long x = 7; int zero = 0; try { x /= zero; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long x = 7; double d = x / 2.0; return d.ToString();")]                                    // "3.5" — a long operand, a double division
    // A uint beside an int computes in long, as C# promotes the pair: neither operand is a long.
    [InlineData("uint u = 7; int d = 2; return (u / d).ToString();")]                                       // "3"
    [InlineData("uint u = 7; int d = -2; return (u / d).ToString();")]                                      // "-3"
    [InlineData("uint u = 7; int d = 2; return (u % d).ToString();")]                                       // "1"
    [InlineData("uint u = 7; int zero = 0; try { return (u / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("uint u = 7; int zero = 0; try { return (u % zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("uint u = 4000000000; int d = -1; return (u / d).ToString();")]                             // "-4000000000"
    [InlineData("uint u = 7; return (u / -2).ToString();")]                                                 // "-3" — a negative constant is no uint
    [InlineData("int i = -7; uint d = 2; return (i / d).ToString();")]                                      // "-3"
    [InlineData("uint? u = 7; int? d = 0; try { return (u / d).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("uint? u = null; int? d = 2; var c = u / d; return c == null ? \"null\" : c.ToString();")] // "null"
    [InlineData("uint? u = 7; int? d = -2; return (u % d).ToString();")]                                    // "1"
    // Controls: the pair's other operators, which ValueFlow's BigInts already answered.
    [InlineData("uint u = 4000000000; int i = 1; return (u + i).ToString();")]                              // "4000000001"
    [InlineData("uint u = 1; int i = -2; return (u * i).ToString();")]                                      // "-2"
    [InlineData("uint u = 1; int i = -2; return (u < i) ? \"lt\" : \"ge\";")]                              // "ge"
    [InlineData("uint u = 6; int i = -2; return (u & i).ToString();")]                                      // "6"
    [InlineData("uint? u = 1; int? i = null; var c = u + i; return c == null ? \"null\" : c.ToString();")] // "null"
    // ---- the compound forms, and their targets evaluated once ----
    [InlineData("int x = 5, zero = 0; try { x %= zero; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int x = int.MinValue, minusOne = -1; try { x /= minusOne; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("var xs = new[] { 10, 20 }; int i = 0, zero = 0; var threw = false; try { xs[i++] /= zero; } catch (Exception) { threw = true; } return (threw ? \"threw \" : \"no \") + i;")] // "threw 1"
    [InlineData("var xs = new[] { 10, 20 }; int i = 0, three = 3; xs[i++] %= three; return (xs[0] * 10 + i).ToString();")]          // "11"
    [InlineData("var d = new Dictionary<int, int> { [0] = 7 }; int zero = 0; try { d[0] /= zero; return d[0].ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("var d = new Dictionary<int, int> { [0] = 7 }; int zero = 0; try { d[0] %= zero; return d[0].ToString(); } catch (Exception e) { return e.Message; }")]
    // ---- a nullable divides only what it holds ----
    [InlineData("int? a = 5, b = 0; try { return (a / b).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int? a = 5, b = null; return (a / b) == null ? \"null\" : \"value\";")]                    // "null"
    [InlineData("int? x = null; x /= 2; return x == null ? \"null\" : x.ToString();")]                  // "null", not 0
    [InlineData("int? x = 7, y = null; x /= y; return x == null ? \"null\" : x.ToString();")]           // "null"
    [InlineData("int? x = 7; int zero = 0; try { x /= zero; return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int? x = 7; x %= 3; return x.ToString();")]                                              // "1"
    [InlineData("int? x = null; x %= 3; return x == null ? \"null\" : \"value\";")]                    // "null"
    [InlineData("sbyte? s = sbyte.MinValue, m = -1; s /= m; return s.ToString();")]                      // "-128"
    [InlineData("long? a = null, b = 1; var c = a / b; return c == null ? \"null\" : c.ToString();")]    // "null", not a TypeError
    [InlineData("long? a = 7, b = 2; return (a / b).ToString();")]                                        // "3"
    [InlineData("long? a = 7, zero = 0; try { return (a / zero).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long? x = null; x /= 2; return x == null ? \"null\" : \"value\";")]                   // "null"
    [InlineData("long? a = 7, b = 2; return (a % b).ToString();")]                                        // "1"
    [InlineData("long? a = null, b = 2; var c = a % b; return c == null ? \"null\" : c.ToString();")]    // "null"
    [InlineData("long? a = 5; var c = a / null; return c == null ? \"null\" : c.ToString();")]           // "null", a literal null
    [InlineData("int? a = 5; var c = a % null; return c == null ? \"null\" : c.ToString();")]            // "null"
    // ---- controls: what .NET divides, the twin divides ----
    [InlineData("int a = -7, b = 2; return (a / b * 10 + a % b).ToString();")]                              // "-31"
    [InlineData("int a = 10, b = 3; a /= b; return a.ToString();")]                                        // "3"
    [InlineData("int count = 3, selected = 2, direction = 1; return ((selected + direction + count) % count).ToString();")] // "0"
    public void AnIntegerDivisionNetRefuses_ThrowsItsException(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
