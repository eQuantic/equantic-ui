using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Fase 6, slice 2 — the numeric BCL surface as a TABLE. Every case here is one entry of
/// <c>PrimitiveStaticStrategy</c>'s method table, executed on both sides: the 71 EQ1001-fenced
/// members were pure functions with derivable JS forms, and each now has its emission plus the
/// case that proves it. Values are chosen to touch the SEMANTICS, not just the happy path:
/// magnitude ties, negative zero, half-to-even midpoints, BigInt widths, wrap-at-the-edge
/// rotations. What stays fenced is impossible BY CONSTRUCTION (Int128, the Unicode numeric-value
/// table, the intern pool), not merely unwritten.
/// </summary>
public class NumericBclConformanceTests
{
    [SkippableTheory]
    // ---- Double: the *Pi family (exact at the special points — the reason they are helpers) ----
    [InlineData("return double.AcosPi(1.0);")]                                   // 0
    [InlineData("return double.AcosPi(-1.0);")]                                  // 1
    [InlineData("return double.AcosPi(0.0);")]                                   // 0.5
    [InlineData("return double.AcosPi(0.3);")]                                   // generic value
    [InlineData("return double.AsinPi(1.0);")]                                   // 0.5
    [InlineData("return double.AsinPi(0.3);")]
    [InlineData("return double.AtanPi(1.0);")]                                   // 0.25
    [InlineData("return double.AtanPi(0.3);")]
    [InlineData("return double.Atan2Pi(1.0, 1.0);")]                             // 0.25
    [InlineData("return double.Atan2Pi(-1.0, -1.0);")]                           // -0.75
    [InlineData("return double.SinPi(1.0);")]                                    // 0 — EXACT (sin(π) is not)
    [InlineData("return double.SinPi(0.5);")]                                    // 1
    [InlineData("return double.SinPi(2.5);")]                                    // 1
    [InlineData("return double.SinPi(-0.5);")]                                   // -1
    [InlineData("return double.SinPi(0.3);")]
    [InlineData("return double.SinPi(1.3);")]                                    // the inexact residue path, ported bit for bit
    [InlineData("return double.CosPi(0.5);")]                                    // 0 — EXACT
    [InlineData("return double.CosPi(1.0);")]                                    // -1
    [InlineData("return double.CosPi(2.0);")]                                    // 1
    [InlineData("return double.CosPi(0.3);")]
    [InlineData("return double.CosPi(0.7);")]                                    // one ULP off -CosPi(0.3) in .NET — and here
    [InlineData("return double.TanPi(0.25);")]                                   // 0.9999999999999999 — .NET's own polynomial
    [InlineData("return double.TanPi(0.75);")]                                   // -1 — the reciprocal path rounds THIS one exactly
    [InlineData("return double.TanPi(1.0) == 0;")]                               // true — the value is -0 by parity; JSON can't say -0
    [InlineData("return double.TanPi(2.0);")]                                    // 0
    [InlineData("return double.TanPi(0.3);")]
    [InlineData("return double.TanPi(0.6);")]
    [InlineData("return double.TanPi(0.7);")]                                    // carries the 0.7-0.5 rounding, like .NET
    [InlineData("return double.TanPi(0.8);")]
    [InlineData("return double.TanPi(1.3);")]                                    // differs from TanPi(0.3) in .NET — the residue is 1.3-1
    [InlineData("var (s, c) = double.SinCos(0.0); return s * 10 + c;")]          // 1
    [InlineData("var (s, c) = double.SinCosPi(0.5); return s * 10 + c;")]        // 10
    // ---- Double: composed exponentials/logs ----
    [InlineData("return double.Exp10M1(1.0);")]                                  // 9
    [InlineData("return double.Exp2M1(3.0);")]                                   // 7
    [InlineData("return double.Exp2M1(0.5);")]
    [InlineData("return double.Log10P1(9.0);")]                                  // 1
    [InlineData("return double.Log2P1(7.0);")]                                   // 3
    [InlineData("return double.Log2P1(0.5);")]
    // ---- Double: sign, classification, neighbors ----
    [InlineData("return double.CopySign(3.0, -2.0);")]                           // -3
    [InlineData("return double.CopySign(-3.0, 2.0);")]                           // 3
    [InlineData("return double.CopySign(3.5, -0.0);")]                           // -3.5 — the sign BIT
    [InlineData("return double.IsNegative(-0.0);")]                              // true — the bit again
    [InlineData("return double.IsNegative(3.0);")]                               // false
    [InlineData("return double.IsPositive(0.0);")]                               // true
    [InlineData("return double.IsPositive(-0.0);")]                              // false
    [InlineData("return double.IsEvenInteger(4.0);")]                            // true
    [InlineData("return double.IsEvenInteger(4.5);")]                            // false
    [InlineData("return double.IsOddInteger(-3.0);")]                            // true
    [InlineData("return double.IsNormal(1.0);")]                                 // true
    [InlineData("return double.IsNormal(1e-310);")]                              // false — subnormal
    [InlineData("return double.IsSubnormal(1e-310);")]                           // true
    [InlineData("return double.IsSubnormal(1.0);")]                              // false
    [InlineData("return double.IsRealNumber(1.0);")]                             // true
    [InlineData("return double.IsRealNumber(double.NaN);")]                      // false
    [InlineData("return double.IsPow2(8.0);")]                                   // true
    [InlineData("return double.IsPow2(0.25);")]                                  // true — negative exponent
    [InlineData("return double.IsPow2(double.Epsilon);")]                        // true — subnormal power
    [InlineData("return double.IsPow2(12.0);")]                                  // false
    [InlineData("return double.BitIncrement(1.0);")]                             // 1.0000000000000002
    [InlineData("return double.BitDecrement(1.0);")]                             // 0.9999999999999999
    // Compared, not printed: the value is Epsilon on both sides, but the runner's JSON spells
    // the exponent differently (.NET "5E-324", JS "5e-324").
    [InlineData("return double.BitIncrement(0.0) == double.Epsilon;")]           // true
    [InlineData("return double.BitDecrement(0.0) == -double.Epsilon;")]          // true
    [InlineData("return double.ILogB(8.0);")]                                    // 3
    [InlineData("return double.ILogB(0.5);")]                                    // -1
    [InlineData("return double.ILogB(1e-310);")]                                 // -1030 — subnormal exponent
    // ---- Double: arithmetic with a contract ----
    [InlineData("return double.Ieee754Remainder(5.0, 3.0);")]                    // -1 — rounds the quotient
    [InlineData("return double.Ieee754Remainder(3.0, 2.0);")]                    // -1 — half-to-EVEN
    [InlineData("return double.Ieee754Remainder(5.0, 2.0);")]                    // 1 — half-to-even again
    [InlineData("return double.Lerp(0.0, 10.0, 0.5);")]                          // 5
    [InlineData("return double.Lerp(2.0, 4.0, 0.25);")]                          // 2.5
    [InlineData("return double.FusedMultiplyAdd(2.0, 3.0, 4.0);")]               // 10
    [InlineData("return double.FusedMultiplyAdd(0.1, 0.2, 0.3);")]               // ONE rounding — naive differs
    [InlineData("return double.MultiplyAddEstimate(2.0, 3.0, 4.0);")]            // 10
    [InlineData("return double.ScaleB(3.0, 4);")]                                // 48
    [InlineData("return double.ScaleB(1.0, -3);")]                               // 0.125
    [InlineData("return double.RootN(27.0, 3);")]                                // 3
    [InlineData("return double.RootN(-8.0, 3);")]                                // -2 — odd root of a negative
    [InlineData("return double.RootN(16.0, 2);")]                                // 4
    [InlineData("return double.RootN(16.0, 4);")]                                // 2
    [InlineData("return double.ClampNative(5.0, 1.0, 3.0);")]                    // 3
    // ---- Double: the min/max family and its tie rules ----
    [InlineData("return double.MaxMagnitude(-5.0, 3.0);")]                       // -5
    [InlineData("return double.MaxMagnitude(-3.0, 3.0);")]                       // 3 — tie goes to the greater
    [InlineData("return double.MinMagnitude(-5.0, 3.0);")]                       // 3
    [InlineData("return double.MinMagnitude(-3.0, 3.0);")]                       // -3 — tie goes to the lesser
    [InlineData("return double.MaxMagnitudeNumber(double.NaN, 3.0);")]           // 3 — NaN is IGNORED
    [InlineData("return double.MinMagnitudeNumber(-5.0, double.NaN);")]          // -5
    [InlineData("return double.MaxNumber(double.NaN, 3.0);")]                    // 3
    [InlineData("return double.MinNumber(3.0, double.NaN);")]                    // 3
    [InlineData("return double.MaxNative(2.0, 3.0);")]                           // 3
    [InlineData("return double.MinNative(2.0, 3.0);")]                           // 2
    // ---- Int32: the bit surface ----
    [InlineData("return int.BigMul(100000, 100000).ToString();")]                // "10000000000" — exact past 2^32
    [InlineData("return int.CopySign(5, -1);")]                                  // -5
    [InlineData("var (q, r) = int.DivRem(7, 2); return q * 10 + r;")]            // 31
    [InlineData("var (q, r) = int.DivRem(-7, 2); return q * 10 + r;")]           // -31 — trunc toward zero
    [InlineData("return int.IsPow2(8);")]                                        // true
    [InlineData("return int.IsPow2(0);")]                                        // false
    [InlineData("return int.IsPow2(-8);")]                                       // false
    [InlineData("return int.LeadingZeroCount(1);")]                              // 31
    [InlineData("return int.LeadingZeroCount(0);")]                              // 32
    [InlineData("return int.Log2(8);")]                                          // 3
    [InlineData("return int.Log2(0);")]                                          // 0 — .NET defines it so
    [InlineData("return int.MaxMagnitude(-5, 3);")]                              // -5
    [InlineData("return int.MaxMagnitude(-3, 3);")]                              // 3
    [InlineData("return int.MinMagnitude(-3, 3);")]                              // -3
    [InlineData("return int.PopCount(255);")]                                    // 8
    [InlineData("return int.PopCount(-1);")]                                     // 32 — all bits
    [InlineData("return int.RotateLeft(1, 31);")]                                // int.MinValue
    [InlineData("return int.RotateLeft(0x12345678, 8);")]                        // 0x34567812
    [InlineData("return int.RotateLeft(5, 32);")]                                // 5 — count wraps
    [InlineData("return int.RotateRight(1, 1);")]                                // int.MinValue
    [InlineData("return int.TrailingZeroCount(8);")]                             // 3
    [InlineData("return int.TrailingZeroCount(0);")]                             // 32
    // ---- Int64: the same surface on BigInt ----
    [InlineData("return long.CopySign(5L, -1L).ToString();")]                    // "-5"
    [InlineData("var (q, r) = long.DivRem(7L, 2L); return (q * 10L + r).ToString();")]   // "31"
    [InlineData("var (q, r) = long.DivRem(-9000000000L, 7L); return (q * 10L + r).ToString();")] // exact past 2^32
    [InlineData("return long.IsPow2(4294967296L);")]                             // true — 2^32
    [InlineData("return long.IsPow2(0L);")]                                      // false
    [InlineData("return long.LeadingZeroCount(1L).ToString();")]                 // "63"
    [InlineData("return long.LeadingZeroCount(0L).ToString();")]                 // "64"
    [InlineData("return long.Log2(4294967296L).ToString();")]                    // "32"
    [InlineData("return long.Log2(0L).ToString();")]                             // "0"
    [InlineData("return long.MaxMagnitude(-5000000000L, 3L).ToString();")]       // "-5000000000"
    [InlineData("return long.MaxMagnitude(-3L, 3L).ToString();")]                // "3"
    [InlineData("return long.MinMagnitude(-3L, 3L).ToString();")]                // "-3"
    [InlineData("return long.PopCount(255L).ToString();")]                       // "8"
    [InlineData("return long.PopCount(-1L).ToString();")]                        // "64"
    [InlineData("return long.RotateLeft(1L, 63).ToString();")]                   // long.MinValue
    [InlineData("return long.RotateLeft(1L, 64).ToString();")]                   // "1" — count wraps
    [InlineData("return long.RotateRight(1L, 1).ToString();")]                   // long.MinValue
    [InlineData("return long.TrailingZeroCount(4294967296L).ToString();")]       // "32"
    [InlineData("return long.TrailingZeroCount(0L).ToString();")]                // "64"
    // ---- Char: surrogate mechanics ----
    [InlineData("return char.ConvertToUtf32('\\uD83D', '\\uDE00');")]            // 128512 — 😀
    [InlineData("return char.IsSurrogatePair(\"a\\uD83D\\uDE00\", 1);")]         // true
    [InlineData("return char.IsSurrogatePair(\"ab\", 0);")]                      // false
    [InlineData("return char.IsSurrogatePair(\"a\\uD83D\\uDE00\", 2);")]         // false — lone low at the end
    public void NumericBcl_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
