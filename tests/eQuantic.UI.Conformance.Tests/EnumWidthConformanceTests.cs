using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An enum is held at its underlying type's width, and its operators and conversions compute as
/// .NET computes them: a <c>long</c>- or <c>ulong</c>-backed one as a BigInt, as every 64-bit integer
/// is held here, where a member past 2^53 was a number that is not its value (#551), and a flags one's
/// <c>|</c>, <c>&amp;</c>, <c>^</c> and <c>~</c> in its own width, where JavaScript's computed in signed 32
/// bits (#555). A long is compared through its text, which the harness reads exactly.
/// </summary>
public class EnumWidthConformanceTests
{
    private const string Enums = """
        [System.Flags] public enum Wide : long { None = 0, Low = 1, Top = 1L << 62 }
        [System.Flags] public enum W : long { None = 0, A = 1, B = 1L << 40 }
        [System.Flags] public enum U : uint { None = 0, A = 1, Top = 0x80000000 }
        [System.Flags] public enum Big : ulong { None = 0, A = 1, Top = 1UL << 63 }
        [System.Flags] public enum Tiny : byte { None = 0, A = 1, B = 2, Max = 255 }
        [System.Flags] public enum Half : ushort { None = 0, A = 1 }
        public enum Huge : long { Small = 1, Top = 9223372036854775807 }
        public enum Unsigned64 : ulong { Zero = 0, Max = 18446744073709551615 }
        public enum WideSigned : long { Neg = -5, Pos = 1L << 60 }
        public enum Status { None, Pending, Done }
        """;

    /// <summary>
    /// A 64-bit enum is a BigInt wherever it is held: its members, the values Parse, GetValues and a
    /// cast answer, its text in every format, its order, its arithmetic and its keys (#551).
    /// </summary>
    [SkippableTheory]
    [InlineData("var w = Wide.Top; return ((long)w).ToString();")]                                                     // "4611686018427387904"
    [InlineData("return ((long)Wide.Top).ToString();")]                                                                // the constant cast too
    [InlineData("var w = Wide.Top; return w.ToString(\"D\") + \"|\" + w.ToString(\"X\") + \"|\" + w;")]              // "4611686018427387904|4000000000000000|Top"
    [InlineData("var w = Wide.Top; return $\"{w:D}|{w,5}|{w:X}\";")]                                                   // "4611686018427387904|  Top|4000000000000000"
    [InlineData("return Enum.Parse<Huge>(\"9223372036854775807\") == Huge.Top;")]                                    // true
    [InlineData("return Enum.Parse<Wide>(\"9223372036854775807\").ToString(\"D\");")]                                // "9223372036854775807"
    [InlineData("return (Enum.Parse<Wide>(\"Top\") == Wide.Top) + \"|\" + (Enum.Parse<W>(\"A, B\") == (W.A | W.B));")] // "True|True"
    [InlineData("var v = Enum.GetValues<WideSigned>(); return v[0] + \",\" + v[1];")]                                  // "Pos,Neg": unsigned order
    [InlineData("var v = Enum.GetValues<Wide>(); return v[2] == Wide.Top;")]                                           // true
    [InlineData("return Enum.IsDefined(typeof(Wide), 4611686018427387904L) + \"|\" + Enum.GetName(typeof(Huge), 9223372036854775807L);")] // "True|Top"
    [InlineData("long v = 9223372036854775806; var h = (Huge)v; return h.ToString() + \"|\" + ((long)h).ToString();")] // a value no member names
    [InlineData("var b = Big.Top; return b.ToString(\"D\") + \"|\" + b;")]                                            // "9223372036854775808|Top"
    [InlineData("return Enum.Parse<Big>(\"18446744073709551615\").ToString(\"D\");")]                                 // "18446744073709551615"
    [InlineData("var u = Unsigned64.Max; return u.ToString() + \"|\" + u.ToString(\"D\") + \"|\" + ((ulong)u).ToString();")]
    [InlineData("var w = Wide.Top; return w switch { Wide.Top => \"top\", Wide.Low => \"low\", _ => \"other\" };")]  // "top"
    [InlineData("var d = new Dictionary<Wide, int> { [Wide.Top] = 1 }; return d[Wide.Top] + \"|\" + d.ContainsKey((Wide)(1L << 62));")] // "1|True"
    [InlineData("var s = new SortedSet<WideSigned> { WideSigned.Pos, WideSigned.Neg }; var r = \"\"; foreach (var x in s) r += x + \",\"; return r;")] // "Neg,Pos,": signed order
    [InlineData("return new[] { Wide.Low, Wide.Top }.Max().ToString();")]                                              // "Top"
    [InlineData("var a = WideSigned.Neg; var b = WideSigned.Pos; return (a < b) + \"|\" + a.CompareTo(b);")]           // "True|-1"
    [InlineData("Wide z = 0; var d = default(Wide); return ((long)z).ToString() + z + d;")]                            // "0NoneNone"
    [InlineData("var w = Wide.Low; return ((long)(w + 1)).ToString();")]                                               // "2"
    [InlineData("var a = W.B; var b = W.A; return (a - b).ToString();")]                                               // "1099511627775": a long
    [InlineData("var w = Wide.Top; w++; return ((long)w).ToString();")]                                                // "4611686018427387905"
    [InlineData("Wide? n = null; var r = n | Wide.Low; return r == null;")]                                             // true: lifted
    [InlineData("W? x = W.A; var y = x | W.B; return ((long)y!.Value).ToString();")]                                   // "1099511627777"
    public void ALongEnum_IsHeldAsABigInt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// A flags enum combines in its own width (#555): a long's flags above bit 31 vanished, a uint's
    /// high bit read negative so <c>HasFlag</c> answered false, a ulong's top bit or'd with itself was 0,
    /// and a byte's or a ushort's complement was negative.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = W.A; var b = W.B; return ((long)(a | b)).ToString();")]                 // "1099511627777"
    [InlineData("var a = W.A; var b = W.B; return (a | b).ToString();")]                         // "A, B"
    [InlineData("var u = U.A; return (u | U.Top).HasFlag(U.Top);")]                              // true
    [InlineData("var u = U.A | U.Top; return ((uint)u).ToString();")]                            // "2147483649"
    [InlineData("var u = U.Top; var a = U.A; return (u > a) + \"|\" + ((u | a) > a);")]          // "True|True"
    [InlineData("var t = Big.Top; return ((ulong)(t | t)).ToString();")]                         // "9223372036854775808"
    [InlineData("var w = Wide.Top | Wide.Low; return w.HasFlag(Wide.Top) + \",\" + w.HasFlag(Wide.Low);")] // "True,True"
    [InlineData("var a = Tiny.A; return (~a).ToString(\"D\");")]                                 // "254"
    [InlineData("var h = Half.A; return (~h).ToString(\"D\");")]                                 // "65534"
    [InlineData("var u = U.A; return ((uint)~u).ToString();")]                                   // "4294967294"
    [InlineData("var w = W.A; return ((long)~w).ToString();")]                                   // "-2"
    [InlineData("var b = Big.Top; return ((ulong)~b).ToString();")]                              // "9223372036854775807"
    [InlineData("var u = U.A; u |= U.Top; return u.HasFlag(U.Top) + \"|\" + (uint)u;")]          // "True|2147483649"
    [InlineData("var w = W.None; w |= W.B; w ^= W.A; return ((long)w).ToString() + \"|\" + w;")] // "1099511627777|A, B"
    [InlineData("var w = W.A | W.B; w &= W.B; return w.ToString();")]                            // "B"
    [InlineData("var t = Tiny.A; t--; t--; return t.ToString(\"D\");")]                         // "255": a byte wraps
    [InlineData("var u = U.A; var v = U.Top; return (u - v).ToString();")]                       // "2147483649": a uint
    [InlineData("var t = Tiny.B; byte n = 255; return (t + n).ToString(\"D\");")]                // "1": wraps unchecked
    public void AFlagsEnum_CombinesInItsWidth(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// An enum that is not a flags one computes on its VALUES as well: its operators applied to the
    /// keys the browser holds, so <c>s | t</c> was 0, <c>s++</c> NaN, and <c>HasFlag</c> answered
    /// equality, where .NET tests the bits of any enum.
    /// </summary>
    [SkippableTheory]
    [InlineData("return Status.Pending.HasFlag(Status.None) + \"|\" + ((Status)3).HasFlag(Status.Pending) + \"|\" + Status.Done.HasFlag(Status.Pending);")] // "True|True|False"
    [InlineData("var s = Status.Pending; var t = Status.Done; return (s | t).ToString();")]          // "3"
    [InlineData("var s = Status.Pending; s++; var t = Status.Done; t++; return s + \"|\" + t;")]     // "Done|3"
    [InlineData("var s = Status.Done; s -= 1; s += 0; return s.ToString();")]                         // "Pending"
    [InlineData("var s = Status.Pending; s |= Status.Done; return s.ToString();")]                    // "3"
    [InlineData("var s = Status.Done; var t = Status.Pending; return (s - t) + \"|\" + s.CompareTo(t) + \"|\" + (s > t);")] // "1|1|True"
    [InlineData("var s = Status.Done; return (s is > Status.Pending) + \"|\" + (s is < Status.Pending);")] // "True|False"
    [InlineData("Status? n = null; var s = Status.Done; return (n < s) + \"|\" + ((n | s) == null);")]  // "False|True"
    public void AnEnumThatIsNoFlagsOne_ComputesOnItsValues(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// An enum converts as its underlying type does, both ways: <c>(int)</c> of a 64-bit one takes its
    /// low 32 bits, a value brought into a narrower enum wraps and throws checked, a number becomes the
    /// member that holds it, and a <c>foreach</c> over enums into an int reads the values, where every
    /// one of them carried the key the browser holds.
    /// </summary>
    [SkippableTheory]
    [InlineData("var w = Wide.Top | Wide.Low; return ((int)w).ToString();")]                                     // "1"
    [InlineData("long l = 1L << 62; return ((Wide)l == Wide.Top).ToString();")]                                   // "True"
    [InlineData("long m = -1; return ((ulong)(Wide)m).ToString();")]                                              // "18446744073709551615"
    [InlineData("var w = Wide.Top; return ((double)w).ToString(System.Globalization.CultureInfo.InvariantCulture);")] // "4.611686018427388E+18"
    [InlineData("var r = \"\"; foreach (int v in new[] { Status.Pending, Status.Done }) r += v; return r;")]       // "12"
    [InlineData("double d = 3.7; return ((Status)d).ToString();")]                                                // "3"
    [InlineData("double d = 300.0; return ((Tiny)d).ToString(\"D\");")]                                           // "44"
    [InlineData("int i = 300; try { return checked((Tiny)i).ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("var w = Wide.Top; try { return checked((int)w).ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("var s = Status.Done; return ((int)(char)s).ToString() + \"|\" + ((decimal)s).ToString();")]      // "2|2"
    [InlineData("int i = -1; return ((byte)(Tiny)i) + \"|\" + ((uint)(U)i);")]                                     // "255|4294967295"
    [InlineData("var u = U.Top; return ((long)(Wide)u).ToString();")]                                              // "2147483648"
    [InlineData("var w = (Wide)(-1L); return ((uint)(U)w).ToString();")]                                           // "4294967295"
    [InlineData("Status? s = Status.Done; int? n = (int?)s; Status? none = null; int? m = (int?)none; return n + \"|\" + (m == null);")] // "2|True"
    [InlineData("int? n = 1; var s = (Status?)n; return s.ToString();")]                                          // "Pending"
    [InlineData("var t = Tiny.B; byte n = 255; try { return checked(t + n).ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("var x = (Tiny)255; try { checked { x++; } return x.ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    public void AnEnum_ConvertsAsItsUnderlyingType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }
}
