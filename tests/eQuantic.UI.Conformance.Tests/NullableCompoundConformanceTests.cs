using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A compound assignment or an increment on a NULLABLE number, on both sides (#372). C# lifts the
/// operator: a null target, or a null operand, answers null, and a value follows its type's rule, a
/// float rounding to a single, a narrow width wrapping, a decimal on the Decimal's methods, a checked
/// context throwing. JavaScript's own operators read null as 0 and knew none of the rules.
/// </summary>
public class NullableCompoundConformanceTests
{
    [SkippableTheory]
    // ---- a null target, or a null operand, stays null ----
    [InlineData("int? x = null; x += 1; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; x -= 1; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; x *= 2; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; x <<= 1; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; x &= 3; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = 5, y = null; x += y; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = 5; x += null; return x == null ? \"null\" : x.ToString();")]
    [InlineData("double? d = null; d *= 2; return d == null ? \"null\" : d.ToString();")]
    [InlineData("decimal? m = null; m += 1m; return m == null ? \"null\" : m.ToString();")]
    [InlineData("float? f = null; f += 0.2f; return f == null ? \"null\" : \"value\";")]
    [InlineData("long? l = null; l += 1; return l == null ? \"null\" : l.ToString();")]
    [InlineData("byte? b = null; b += 10; return b == null ? \"null\" : b.ToString();")]
    // ---- a value follows its type's rule ----
    [InlineData("int? x = 5; x += 1; return x.ToString();")]                                                   // "6"
    [InlineData("float? f = 0.1f; f += 0.2f; return ((double)f.Value).ToString();")]                         // the single
    [InlineData("float? f = 0.1f; f *= 3; return ((double)f.Value).ToString();")]
    [InlineData("byte? b = 250; b += 10; return b.ToString();")]                                               // "4"
    [InlineData("short? s = short.MaxValue; s += 1; return s.ToString();")]                                     // "-32768"
    [InlineData("decimal? m = 1.5m; m += 1m; return m.ToString();")]                                            // "2.5"
    [InlineData("decimal? m = 0.1m; m *= 3; return m.ToString();")]                                             // "0.3"
    [InlineData("long? l = long.MaxValue; l -= 1; return l.ToString();")]
    [InlineData("long? l = 5; l *= 3; return l.ToString();")]                                                   // "15"
    [InlineData("int? x = int.MaxValue; try { checked { x += 1; } return x.ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("double? d = 1.5; d *= 2; return d.ToString();")]                                               // "3"
    [InlineData("int? x = 6; x %= 4; return x.ToString();")]                                                    // "2" — the division family, since #374
    // ---- the increments, prefix and postfix, in value position too ----
    [InlineData("int? x = null; x++; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; ++x; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = null; x--; return x == null ? \"null\" : x.ToString();")]
    [InlineData("int? x = 5; var old = x++; return old + \"|\" + x;")]                                          // "5|6"
    [InlineData("int? x = null; var old = x++; return (old == null ? \"null\" : \"v\") + \"|\" + (x == null ? \"null\" : \"v\");")]
    [InlineData("int? x = 5; var now = ++x; return now + \"|\" + x;")]                                          // "6|6"
    [InlineData("byte? b = 255; b++; return b.ToString();")]                                                   // "0"
    [InlineData("byte? b = 255; var old = b++; return old + \"|\" + b;")]                                      // "255|0"
    [InlineData("sbyte? s = 127; s++; return s.ToString();")]                                                  // "-128"
    [InlineData("decimal? m = null; m++; return m == null ? \"null\" : m.ToString();")]
    [InlineData("decimal? m = 1.5m; m++; return m.ToString();")]                                               // "2.5"
    [InlineData("long? l = null; l++; return l == null ? \"null\" : l.ToString();")]
    [InlineData("long? l = 9007199254740993; l++; return l.ToString();")]                                      // "9007199254740994"
    [InlineData("float? f = null; f++; return f == null ? \"null\" : \"value\";")]
    [InlineData("float? f = 0.1f; f++; return ((double)f.Value).ToString();")]                                 // the single
    [InlineData("double? d = null; d--; return d == null ? \"null\" : d.ToString();")]
    [InlineData("char? c = null; c++; return c == null ? \"null\" : c.ToString();")]
    [InlineData("char? c = 'a'; c++; return c.ToString();")]                                                  // "b"
    [InlineData("int? x = int.MaxValue; try { checked { x++; } return x.ToString(); } catch (Exception e) { return e.Message; }")]
    // ---- the target is evaluated once, whatever it holds ----
    [InlineData("var xs = new int?[] { 1, null }; int i = 0; xs[i++] += 5; return i + \"|\" + xs[0] + \"|\" + (xs[1] == null ? \"null\" : \"v\");")] // "1|6|null"
    [InlineData("var xs = new int?[] { null, 2 }; int i = 0; xs[i++] += 5; return i + \"|\" + (xs[0] == null ? \"null\" : \"v\") + \"|\" + xs[1];")] // "1|null|2"
    [InlineData("var xs = new int?[] { null, 2 }; int i = 0; xs[i++]++; return i + \"|\" + (xs[0] == null ? \"null\" : \"v\") + \"|\" + xs[1];")]    // "1|null|2"
    [InlineData("var xs = new byte?[] { 255 }; int i = 0; var old = xs[i++]++; return i + \"|\" + old + \"|\" + xs[0];")]                              // "1|255|0"
    // ---- a dictionary entry takes the same rule ----
    [InlineData("var d = new Dictionary<string, int?> { [\"a\"] = null }; d[\"a\"] += 1; return d[\"a\"] == null ? \"null\" : \"v\";")]
    [InlineData("var d = new Dictionary<string, byte?> { [\"a\"] = 250 }; d[\"a\"] += 10; return d[\"a\"].ToString();")]                              // "4"
    [InlineData("var d = new Dictionary<string, decimal?> { [\"a\"] = 1.5m }; d[\"a\"] += 1m; return d[\"a\"].ToString();")]                            // "2.5"
    // ---- the other lifted unary operators ----
    [InlineData("int? x = null; var y = -x; return y == null ? \"null\" : y.ToString();")]
    [InlineData("int? x = null; var y = ~x; return y == null ? \"null\" : y.ToString();")]
    [InlineData("int? x = null; var y = +x; return y == null ? \"null\" : y.ToString();")]
    [InlineData("decimal? m = null; var y = -m; return y == null ? \"null\" : y.ToString();")]
    [InlineData("int? x = 5; var y = -x; return y.ToString();")]                                               // "-5"
    [InlineData("decimal? m = 1.5m; var y = -m; return y.ToString();")]                                        // "-1.5"
    [InlineData("long? l = 5; var y = ~l; return y.ToString();")]                                               // "-6"
    [InlineData("long? l = 5; var y = +l; return y.ToString();")]                                               // "5": `+5n` throws
    [InlineData("long? l = 5; return (l + 1L).ToString();")]                                                    // "6" — control
    public void ANullableTarget_KeepsNullAndItsTypesRule(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
