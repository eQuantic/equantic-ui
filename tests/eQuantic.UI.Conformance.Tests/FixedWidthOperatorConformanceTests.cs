using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A bitwise operator and a negation keep their width, on both sides, a nullable target included
/// (found in the review of #379). JavaScript computes <c>&amp;</c>, <c>|</c>, <c>^</c> and <c>&gt;&gt;</c> in signed
/// 32 bits, so a uint with its top bit set came back negative and its right shift carried a sign in;
/// <c>&gt;&gt;&gt;</c> answers an unsigned 32-bit number whatever the width; and a negation answered one more
/// than int or long holds where C#'s <c>checked</c> throws and an explicit <c>unchecked</c> wraps.
/// </summary>
public class FixedWidthOperatorConformanceTests
{
    [SkippableTheory]
    // ---- a uint's bitwise operators, compound and binary ----
    [InlineData("uint x = uint.MaxValue; x &= uint.MaxValue; return x.ToString();")]                         // "4294967295"
    [InlineData("uint? x = uint.MaxValue; x &= uint.MaxValue; return x.ToString();")]
    [InlineData("uint x = 0x80000000; x |= 1; return x.ToString();")]                                       // "2147483649"
    [InlineData("uint x = 1; x ^= 0xFFFFFFFF; return x.ToString();")]                                       // "4294967294"
    [InlineData("uint x = uint.MaxValue; x >>= 1; return x.ToString();")]                                   // "2147483647"
    [InlineData("uint? x = 0x80000000; x >>= 4; return x.ToString();")]                                     // "134217728"
    [InlineData("uint a = uint.MaxValue; return (a & a).ToString();")]
    [InlineData("uint a = uint.MaxValue; return (a >> 1).ToString();")]
    [InlineData("uint a = 0xF0000000, b = 0x0F0000FF; return (a | b).ToString() + \"|\" + (a ^ b).ToString();")]
    // ---- >>> answers an unsigned 32-bit number, which a narrower or signed width brings back ----
    [InlineData("int x = -1; x >>>= 0; return x.ToString();")]                                              // "-1"
    [InlineData("int x = -16; return (x >>> 2).ToString();")]                                               // "1073741820"
    [InlineData("sbyte s = -1; s >>>= 1; return s.ToString();")]                                            // "-1"
    [InlineData("short s = -2; s >>>= 1; return s.ToString();")]                                            // "-1"
    // ---- controls: the widths JavaScript already answers ----
    [InlineData("int x = -1; x &= 0x7F; return x.ToString();")]                                             // "127"
    [InlineData("sbyte s = -1; s ^= 0x7F; return s.ToString();")]                                           // "-128"
    [InlineData("byte? b = 200; b |= 100; return b.ToString();")]                                           // "236"
    // ---- a long's shift: a BigInt count masked to six bits, the high bits discarded, >>> on its pattern ----
    [InlineData("long l = -1; l >>= 1; return l.ToString();")]                                              // "-1": the count was a number
    [InlineData("long l = 1; l <<= 3; return l.ToString();")]                                               // "8"
    [InlineData("long? l = 4; l >>= 1; return l.ToString();")]                                              // "2"
    [InlineData("long l = 1; return (l << 63).ToString();")]                                                // long.MinValue
    [InlineData("long l = 1; return (l << 64).ToString();")]                                                // "1": the count is masked
    [InlineData("long l = 1; int n = 65; return (l << n).ToString();")]                                     // "2"
    [InlineData("long l = -8; return (l >>> 1).ToString();")]                                               // "9223372036854775804"
    [InlineData("long l = 1; try { return checked(l << 63).ToString(); } catch (Exception e) { return e.Message; }")] // a shift never throws
    [InlineData("ulong u = ulong.MaxValue; u >>>= 60; return u.ToString();")]                               // "15"
    [InlineData("ulong u = 1; return (u << 63).ToString();")]                                               // control
    [InlineData("long l = -8; return (l >> 1).ToString();")]                                                // control: "-4"
    // ---- a complement of an unsigned width is unsigned ----
    [InlineData("uint x = 0; return (~x).ToString();")]                                                     // "4294967295"
    [InlineData("uint? x = 0; var y = ~x; return y.ToString();")]
    [InlineData("ulong u = 0; return (~u).ToString();")]                                                    // "18446744073709551615"
    [InlineData("ulong? u = 5; return (~u).ToString();")]                                                   // "18446744073709551610"
    [InlineData("byte b = 0; return (~b).ToString();")]                                                     // "-1": an int, control
    [InlineData("long l = 0; return (~l).ToString();")]                                                     // "-1": control
    // ---- a negation, in the context it sits in ----
    [InlineData("int x = int.MinValue; try { return checked(-x).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("int? x = int.MinValue; try { return checked(-x).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("long l = long.MinValue; return unchecked(-l).ToString();")]                                // "-9223372036854775808"
    [InlineData("long? l = long.MinValue; return unchecked(-l).ToString();")]
    [InlineData("int x = int.MinValue; return unchecked(-x).ToString();")]                                  // "-2147483648"
    [InlineData("sbyte? s = -128; var y = -s; return y.ToString();")]                                       // "128", an int
    [InlineData("uint u = 5; var y = -u; return y.ToString();")]                                            // "-5", a long
    public void AFixedWidthOperator_KeepsItsWidth(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
