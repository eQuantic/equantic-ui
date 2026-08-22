using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Fase 5, slice 6 — explicit conversions, driven by the ONE conversion table (ValueFlow.Apply)
/// instead of a cast-only copy of the masks. The instrument: every case is a cast whose meaning
/// the bound tree carries — which REPRESENTATION each side has (a long is a BigInt, a char a
/// 1-length string, a decimal a Decimal), whether the context is checked, whether null propagates.
/// The old cast table knew none of that: `(int)aLong` put a BigInt into Math.trunc (TypeError),
/// `(long)aDouble` produced a plain number that later BigInt arithmetic choked on, and
/// `checked((byte)300)` wrapped instead of throwing.
/// </summary>
public class ExplicitCastConformanceTests
{
    [SkippableTheory]
    // ---- across the BigInt boundary: long/ulong casts change representation ----
    [InlineData("long l = 5000000000L; int i = (int)l; return i;")]                     // 705032704 — low 32 bits
    [InlineData("long l = 300; byte b = (byte)l; return b;")]                            // 44
    [InlineData("long l = 40000; short s = (short)l; return s;")]                        // -25536
    [InlineData("long l = 65633; char c = (char)l; return c.ToString();")]               // "a" — 65633 & 0xFFFF = 97
    [InlineData("long l = 5; double d = (double)l; return d;")]                          // 5
    [InlineData("long l = 7; float f = (float)l; return f.ToString();")]                 // "7"
    [InlineData("int i = -1; ulong u = (ulong)i; return u.ToString();")]                 // "18446744073709551615"
    [InlineData("long l = -1; ulong u = (ulong)l; return u.ToString();")]                // "18446744073709551615"
    [InlineData("ulong u = ulong.MaxValue; long l = (long)u; return l.ToString();")]     // "-1"
    [InlineData("double d = 3.99; long l = (long)d; return l.ToString();")]              // "3"
    [InlineData("double d = -3.99; long l = (long)d; return (l * 1000000000000L).ToString();")] // "-3000000000000" — the result IS a BigInt
    [InlineData("int i = 42; long l = (long)i; return (l + 3000000000L).ToString();")]   // "3000000042"
    // ---- narrowing between plain numbers wraps by C#'s rules ----
    [InlineData("int i = 300; byte b = (byte)i; return b;")]                             // 44
    [InlineData("int i = -1; byte b = (byte)i; return b;")]                              // 255
    [InlineData("int i = 40000; short s = (short)i; return s;")]                         // -25536
    [InlineData("uint u = 4294967295; int i = (int)u; return i;")]                       // -1
    [InlineData("int i = -1; uint u = (uint)i; return u.ToString();")]                   // "4294967295"
    [InlineData("double d = 3.99; byte b = (byte)d; return b;")]                         // 3 — truncate, then wrap
    [InlineData("double d = 256.5; byte b = (byte)d; return b;")]                        // 0
    [InlineData("float f = 1.99f; int i = (int)f; return i;")]                           // 1
    // ---- char is a 1-length string on this side ----
    [InlineData("int i = 65; char c = (char)i; return c.ToString();")]                   // "A"
    [InlineData("int i = 65601; char c = (char)i; return c.ToString();")]                // "A" — wraps at 2^16
    [InlineData("char c = 'A'; byte b = (byte)c; return b;")]                            // 65
    [InlineData("char c = 'ÿ'; byte b = (byte)(c + 1); return b;")]                      // 0 — 256 wraps
    [InlineData("char c = '\\uFFFF'; short s = (short)c; return s;")]                    // -1
    [InlineData("double d = 66.9; char c = (char)d; return c.ToString();")]              // "B"
    // ---- float narrowing rounds to single precision ----
    [InlineData("double d = 0.1; float f = (float)d; return f == 0.1f;")]                // true
    [InlineData("double d = 0.1; return (float)d == 0.1;")]                              // false — a single is not the double
    [InlineData("float f = (float)(1.0 / 3.0); return f.ToString();")]                   // "0.33333334"
    // ---- checked casts throw where C# throws ----
    [InlineData("int i = 300; try { byte b = checked((byte)i); return b; } catch (OverflowException) { return -1; }")]  // -1
    [InlineData("int i = 200; byte b = checked((byte)i); return b;")]                                                   // 200
    [InlineData("long l = 5000000000L; try { return checked((int)l); } catch (OverflowException) { return -1; }")]      // -1
    [InlineData("long l = 2000000000L; return checked((int)l);")]                                                       // 2000000000
    [InlineData("double d = 1e20; try { return checked((long)d).ToString(); } catch (OverflowException) { return \"of\"; }")] // "of"
    [InlineData("double d = 100.9; long l = checked((long)d); return l.ToString();")]                                   // "100"
    [InlineData("int i = -1; try { return checked((uint)i).ToString(); } catch (OverflowException) { return \"of\"; }")] // "of"
    [InlineData("int i = 70000; try { char c = checked((char)i); return c.ToString(); } catch (OverflowException) { return \"of\"; }")] // "of"
    // ---- a nullable target propagates null; a value converts as its underlying type ----
    [InlineData("double? d = 3.9; int? i = (int?)d; return i;")]                         // 3
    [InlineData("double? d = null; int? i = (int?)d; return i;")]                        // null
    [InlineData("long? l = 3000000000; int? i = (int?)l; return i;")]                    // -1294967296
    [InlineData("int? n = 5; long? l = (long?)n; return (l + 1).ToString();")]           // "6"
    // ---- decimal as the SOURCE of a cast (the Decimal object is not a number) ----
    [InlineData("decimal m = 3.99m; int i = (int)m; return i;")]                         // 3
    [InlineData("decimal m = -3.99m; int i = (int)m; return i;")]                        // -3
    [InlineData("decimal m = 3.99m; double d = (double)m; return d;")]                   // 3.99
    [InlineData("decimal m = 3.99m; long l = (long)m; return l.ToString();")]            // "3"
    // decimal → integral ALWAYS throws past the edge (no unchecked wrap exists for it in C#)
    [InlineData("decimal m = 300.5m; try { byte b = (byte)m; return b; } catch (OverflowException) { return -1; }")] // -1
    // ---- an enum cast whose operand is a long indexes the same map ----
    [InlineData("long l = 3; return ((DayOfWeek)l).ToString();")]                        // "Wednesday"
    public void ExplicitCast_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
