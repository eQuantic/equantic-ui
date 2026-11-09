using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for enums (regression for #13). An enum member is held in the browser as its camelCase
/// name, or its number for a [Flags] enum, so equality, switch and ternary behave as in .NET, and its
/// text, its formats and the statics of <c>Enum</c> read that back to what .NET writes (#452, #480).
/// </summary>
public class EnumConformanceTests
{
    private const string Prelude = "enum Status { Active, Pending, Inactive }";

    [SkippableTheory]
    [InlineData("Status.Active == Status.Active")]
    [InlineData("Status.Active == Status.Pending")]
    [InlineData("Status.Active != Status.Pending")]
    [InlineData("Status.Active == Status.Active ? \"yes\" : \"no\"")]
    [InlineData("Status.Pending switch { Status.Active => 1, Status.Pending => 2, _ => 0 }")]
    [InlineData("Status.Inactive switch { Status.Active => 1, Status.Pending => 2, _ => 0 }")]
    public void Enums_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Prelude);
    }

    /// <summary>
    /// Numeric casts: the transpiler bridges the string representation with the enum's compile-time
    /// name↔value table, so `(int)enum` yields the real underlying value and `(EnumType)int` yields a
    /// member comparable to other members. These produce primitives (int/bool), so they conform exactly.
    /// </summary>
    [SkippableTheory]
    [InlineData("(int)Status.Active")]     // -> 0
    [InlineData("(int)Status.Pending")]    // -> 1
    [InlineData("(int)Status.Inactive")]   // -> 2
    [InlineData("(Status)1 == Status.Pending")]    // int -> enum, compared by member -> true
    [InlineData("(Status)2 == Status.Pending")]    // -> false
    [InlineData("(int)(Status)2")]                 // round-trip -> 2
    public void EnumCasts_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Prelude);
    }

    private const string FlagsPrelude = "[System.Flags] enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 }";

    /// <summary>
    /// A <c>[Flags]</c> enum is represented numerically, so bitwise combination, masking, and
    /// <c>HasFlag</c> all behave like .NET (they evaluate to int/bool, comparable exactly).
    /// </summary>
    [SkippableTheory]
    [InlineData("(int)(Perm.Read | Perm.Write)")]                  // -> 3
    [InlineData("(int)(Perm.Read | Perm.Write | Perm.Exec)")]      // -> 7
    [InlineData("(int)(Perm.Read & Perm.Write)")]                  // -> 0
    [InlineData("((Perm.Read | Perm.Write) & Perm.Write) != 0")]   // mask test -> true
    [InlineData("(Perm.Read | Perm.Write).HasFlag(Perm.Read)")]    // -> true
    [InlineData("(Perm.Read | Perm.Write).HasFlag(Perm.Exec)")]    // -> false
    [InlineData("(Perm.Read | Perm.Write | Perm.Exec).HasFlag(Perm.Write)")] // -> true
    [InlineData("(int)(Perm)3")]                                   // int -> flags -> int round-trip -> 3
    [InlineData("Perm.Read == Perm.Read")]                         // -> true
    public void Flags_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, FlagsPrelude);
    }

    private const string Enums = """
        public enum Status { Active, Pending, Inactive }
        public enum Rank { Zeta, Alpha, Mid = 5 }
        [System.Flags] public enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 }
        [System.Flags] public enum Bare { A = 1, B = 2 }
        public enum Level { Low = 1, High = 2 }
        public enum U : uint { Low = 1, High = 0x80000000 }
        [System.Flags] public enum Wide : long { None = 0, A = 1, B = 1L << 40 }
        public enum Tiny : byte { Zero = 0, One = 1 }
        [System.Flags] public enum Huge : long { None = 0, A = 1, B = 1L << 40, Top = 1L << 62 }
        public enum Span64 : long { Min = long.MinValue, Zero = 0, Max = long.MaxValue }
        [System.Flags] public enum Big : ulong { None = 0, Low = 1, Top = 1UL << 63 }
        public enum UMax : ulong { Zero = 0, Max = ulong.MaxValue }
        [System.Flags] public enum UFlags : uint { None = 0, A = 1, Top = 0x80000000 }
        [System.Flags] public enum Small : byte { None = 0, A = 1, B = 0x80 }
        """;

    /// <summary>
    /// An enum reads with its underlying type's width and sign, and its arguments run in the order
    /// they are written, a provider it ignores included: a 32-bit operator read a uint's high bit
    /// negative and dropped a long's flags above bit 31, a named argument ran in its parameter's
    /// place, and an ignored provider never ran at all.
    /// </summary>
    [SkippableTheory]
    [InlineData("return Enum.Parse<U>(\"High\") == U.High;")]                                                         // true
    [InlineData("return U.High.ToString(\"X\") + Enum.Parse<Wide>(\"A, B\").ToString();")]                          // "80000000A, B"
    [InlineData("try { Enum.Parse<U>(\"4294967296\"); return \"parsed\"; } catch { return \"refused\"; }")]          // "refused"
    [InlineData("var order = \"\"; bool B() { order += \"b\"; return true; } string T() { order += \"t\"; return \"pending\"; } var s = Enum.Parse<Status>(ignoreCase: B(), value: T()); return s + order;")] // "Pendingbt"
    [InlineData("var arr = new Status[2]; int i = 0; string T() { i = 1; return \"Inactive\"; } Enum.TryParse<Status>(result: out arr[i], value: T()); return arr[0] + \",\" + arr[1];")] // "Inactive,Active"
    [InlineData("int calls = 0; IFormatProvider P() { calls++; return null; } var st = Status.Inactive; return Status.Pending.ToString(P()) + st.ToString(\"D\", P()) + calls;")] // "Pending22"
    [InlineData("int n = 257; long m = 258; return ((Tiny)n == Tiny.One) + \",\" + unchecked((Tiny)257) + \",\" + (Tiny)m;")] // "True,One,2"
    [InlineData("var arr = new Status[3]; int i = 0; string order = \"\"; bool B() { order += \"b\"; i = 1; return true; } string T() { order += \"t\"; i = 5; return \"inactive\"; } Enum.TryParse<Status>(ignoreCase: B(), result: out arr[i++], value: T()); return order + \",\" + arr[0] + \",\" + arr[1] + \",\" + i;")] // "bt,Active,Inactive,5"
    [InlineData("try { return Enum.GetName(typeof(Status), (object)true); } catch (Exception e) { return e.Message; }")] // "The value passed in must be an enum base..."
    [InlineData("try { return Enum.IsDefined(typeof(Status), (object)true).ToString(); } catch (Exception e) { return e.Message; }")] // "Unknown enum type."
    public void AnEnum_ReadsItsWidth_AndRunsItsArgumentsInOrder(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// A 64-bit enum is held as a BigInt, as a <c>long</c> is (#551): a member past 2^53 was written as a
    /// number that is not its value, and its casts, its text, its statics and its order lost the low
    /// digits.
    /// </summary>
    [SkippableTheory]
    [InlineData("return (long)Huge.Top;")]                                                                        // "4611686018427387904"
    [InlineData("var h = Huge.Top; return h.ToString(\"D\") + \"|\" + h.ToString(\"X\") + \"|\" + h;")]             // "4611686018427387904|4000000000000000|Top"
    [InlineData("return Enum.Parse<Span64>(\"9223372036854775807\") == Span64.Max;")]                              // true
    [InlineData("var x = Span64.Max; var n = Span64.Min; return x.ToString(\"D\") + \"|\" + n.ToString(\"D\") + \"|\" + (long)x;")] // "9223372036854775807|-9223372036854775808|9223372036854775807"
    [InlineData("return string.Join(\",\", Enum.GetValues<Span64>());")]                                           // "Zero,Max,Min"
    [InlineData("var u = UMax.Max; return (ulong)u;")]                                                             // "18446744073709551615"
    [InlineData("long v = 1L << 62; return ((Huge)v).ToString() + \"|\" + ((Huge)v == Huge.Top);")]               // "Top|True"
    [InlineData("var t = Huge.Top; var a = Huge.A; return (int)t + \"|\" + (int)(t | a);")]                         // "0|1"
    [InlineData("long v = (1L << 62) + 5; return ((Span64)v).ToString();")]                                        // "4611686018427387909"
    [InlineData("var h = Huge.Top; return h;")]                                                                    // 4611686018427387904, held as a BigInt
    [InlineData("var d = new Dictionary<Huge, int> { [Huge.Top] = 1, [Huge.B] = 2 }; return d[Huge.Top] + d[Huge.B];")] // 3
    [InlineData("var h = Huge.Top; return h switch { Huge.A => 1, Huge.Top => 2, _ => 0 };")]                      // 2
    [InlineData("Huge h = default; return h == Huge.None;")]                                                       // true
    [InlineData("var h = Huge.Top; return $\"{h}|{h:D}|{h:X}|{h,5}\";")]                                           // "Top|4611686018427387904|4000000000000000|  Top"
    public void A64BitEnum_IsHeldAsALongIs(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// An enum's operators compute at its underlying type's width (#555): JavaScript's own computed in
    /// signed 32 bits, so a uint flag's high bit read negative and a long's flags above bit 31 vanished.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = Huge.A; var b = Huge.B; return (long)(a | b);")]                     // "1099511627777"
    [InlineData("var a = UFlags.A; var t = UFlags.Top; return (a | t).HasFlag(t);")]           // true
    [InlineData("var t = Big.Top; return (ulong)(t | t);")]                                    // "9223372036854775808"
    [InlineData("var a = Huge.A; return (long)~a;")]                                           // "-2"
    [InlineData("var x = UFlags.A; return (uint)~x;")]                                         // 4294967294
    [InlineData("var s = Small.A; return (byte)~s;")]                                          // 254
    [InlineData("var b = Big.Low; var t = Big.Top; return (ulong)~b + \"|\" + (ulong)~t + \"|\" + (~b).ToString(\"X\");")] // "18446744073709551614|9223372036854775807|FFFFFFFFFFFFFFFE"
    [InlineData("var t = UFlags.Top; return (uint)(t & UFlags.Top);")]                          // 2147483648
    [InlineData("var h = Huge.Top; h |= Huge.A; return h.ToString();")]                        // "A, Top"
    [InlineData("var h = Huge.Top | Huge.B; return (h & ~Huge.B).ToString();")]                // "Top"
    [InlineData("var u = UFlags.Top; u ^= UFlags.Top; return u.ToString();")]                  // "None"
    [InlineData("var t = Huge.Top; var b = Huge.B; return t > b;")]                            // true
    [InlineData("var t = Big.Top; var l = Big.Low; return (t > l) + \"|\" + (t < l);")]        // "True|False"
    [InlineData("var u = UFlags.Top; var a = UFlags.A; return u > a;")]                        // true
    [InlineData("var s = Status.Pending; s++; return s.ToString();")]                          // "Inactive"
    [InlineData("var s = Status.Active; return (s + 2).ToString();")]                          // "Inactive"
    [InlineData("var s = Status.Inactive; var a = Status.Active; return s - a;")]              // 2
    [InlineData("var h = Huge.B; h++; return (long)h;")]                                       // "1099511627777"
    [InlineData("var b = Big.Top; return (b - 1).ToString(\"D\");")]                           // "9223372036854775807"
    [InlineData("Status? s = null; Status? t = Status.Active; return (s is < Status.Inactive) + \"|\" + (t is < Status.Inactive);")] // "False|True": a null never matches a relation
    [InlineData("var h = Huge.A; h += 1; var s = h.ToString(); h -= 2; return s + \"|\" + (long)h;")] // "2|-1"
    [InlineData("var x = Span64.Zero; x += 5; return (long)x;")] // "5"
    [InlineData("try { return Enum.GetName(typeof(Huge), 1.5) ?? \"null\"; } catch (ArgumentException) { return \"arg\"; } catch (InvalidOperationException) { return \"op\"; }")] // "arg": a fraction is no underlying type
    public void AnEnumsOperators_ComputeAtItsWidth(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// An enum's conversions are C#'s enumeration conversions, each the numeric conversion between the
    /// underlying types, and its comparisons read its values (#551, #555): a checked cast throws where
    /// .NET throws, a foreach over numbers reads each as a member where every one became the zero member,
    /// GetValueOrDefault answers the zero member where it wrote its declared name, a flags enum's
    /// CompareTo answers where it called a method no number has, a relational pattern compares values
    /// where it compared keys, and an object unboxes only the underlying type.
    /// </summary>
    [SkippableTheory]
    [InlineData("var t = Huge.Top; try { return checked((int)t).ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("var t = Huge.Top; return unchecked((int)t);")]                                                                  // 0
    [InlineData("int n = 300; try { return checked((Tiny)n).ToString(); } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("var r = \"\"; foreach (Status s in new[] { 2, 0, 1 }) r += s + \",\"; return r;")]                            // "Inactive,Active,Pending,"
    [InlineData("Status? s = null; Huge? h = null; return s.GetValueOrDefault() + \"|\" + h.GetValueOrDefault();")]              // "Active|None"
    [InlineData("var a = Huge.Top; var b = Huge.A; var p = Perm.Write; return a.CompareTo(b) + \"|\" + p.CompareTo(Perm.Read) + \"|\" + b.CompareTo(a);")] // "1|1|-1"
    [InlineData("var s = Status.Inactive; return (s is > Status.Pending) + \"|\" + (s is < Status.Active);")]                   // "True|False"
    [InlineData("var s = Status.Pending; var h = Huge.Top; return (double)s + \"|\" + (double)h;")]                               // "1|4.611686018427388E+18"
    [InlineData("object o = 1; try { var h = (Huge)o; return \"cast\"; } catch (InvalidCastException) { return \"refused\"; }")] // "refused"
    [InlineData("object o = 1L; return ((Huge)o).ToString();")]                                                                  // "A"
    [InlineData("return Enum.IsDefined(typeof(Huge), 1L) + \"|\" + Enum.GetName(typeof(Huge), 1);")]                            // "True|A"
    [InlineData("var l = new List<Span64> { Span64.Max, Span64.Min, Span64.Zero }; l.Sort(); return string.Join(\",\", l);")]  // "Min,Zero,Max"
    [InlineData("Huge? a = Huge.A; Huge? n = null; return (a | Huge.B) + \"|\" + ((n | Huge.B) == null);")]                     // "A, B|True"
    [InlineData("var s = Status.Active; s += 2; var u = UFlags.A; u |= UFlags.Top; return s + \"|\" + (uint)u;")]               // "Inactive|2147483649"
    public void AnEnumsConversions_AreDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// A value no member names is legal C#, and is held as its number: it went through the cast's
    /// name↔value tables, had no entry in either, and was undefined, so two different ones were equal
    /// and every one printed "undefined" (#404).
    /// </summary>
    [SkippableTheory]
    [InlineData("Level l = default; return (int)l;")]                     // 0
    [InlineData("return ((Level)0).ToString();")]                         // "0"
    [InlineData("int n = 7; return ((Level)n).ToString();")]              // "7"
    [InlineData("int n = 7; return (int)(Level)n;")]                      // 7
    [InlineData("return ((Level)7).ToString();")]                         // "7"
    [InlineData("int a = 7, b = 8; return (Level)a == (Level)b;")]        // false
    [InlineData("int a = 1; return (Level)a == Level.Low;")]              // true
    public void AValueNoMemberNames_IsItsNumber(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// An enum's text is .NET's in every shape: a flags member, a combination, zero with a None and
    /// without one, a value no member names, a nullable enum holding a value and holding null, and an
    /// enum concatenated into a string, which wrote <c>undefined</c> (#452, #535).
    /// </summary>
    [SkippableTheory]
    [InlineData("Perm p = Perm.Read; return p.ToString();")]                              // "Read"
    [InlineData("var p = Perm.Read | Perm.Write; return p.ToString();")]                  // "Read, Write"
    [InlineData("var p = Perm.Read | Perm.Write | Perm.Exec; return $\"{p}\";")]          // "Read, Write, Exec"
    [InlineData("var p = Perm.None; return p.ToString();")]                               // "None"
    [InlineData("var b = (Bare)0; return b.ToString();")]                                 // "0"
    [InlineData("var b = (Bare)8; return b.ToString();")]                                 // "8"
    [InlineData("var p = (Perm)9; return p.ToString();")]                                 // "9"
    [InlineData("Status? s = Status.Pending; return s.ToString();")]                      // "Pending"
    [InlineData("Status? s = null; return \"[\" + s.ToString() + \"]\";")]                // "[]"
    [InlineData("Status? s = Status.Inactive; return $\"<{s}>\";")]                       // "<Inactive>"
    [InlineData("var x = Rank.Mid; return \",\" + x;")]                                    // ",Mid"
    [InlineData("var r = \"\"; foreach (var x in new[] { Rank.Mid, Rank.Zeta }) r += \",\" + x; return r;")] // ",Mid,Zeta"
    [InlineData("var p = Perm.Write; return \"p=\" + p;")]                                 // "p=Write"
    [InlineData("return Status.Pending.ToString(\"D\");")]                                // "1"
    [InlineData("var r = Rank.Mid; return r.ToString(\"X\") + \"|\" + r.ToString(\"d\");")]  // "00000005|5"
    [InlineData("var s = (Status)3; return s.ToString(\"F\") + \"|\" + s.ToString(\"G\");")] // "Pending, Inactive|3"
    [InlineData("var f = \"D\"; return Rank.Mid.ToString(f) + Rank.Alpha.ToString(\"g\");")]  // "5Alpha"
    [InlineData("try { Rank.Mid.ToString(\"Q\"); return \"ok\"; } catch { return \"refused\"; }")] // "refused"
    [InlineData("var r = Rank.Mid; return $\"{r:D}|{r,6}|{r,-6:X}|\";")]                  // "5|   Mid|00000005|"
    [InlineData("Status? s = Status.Pending; Status? n = null; return $\"[{s,10}][{n,3}][{s:D}]\";")] // "[   Pending][   ][1]"
    public void AnEnumsText_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// Enum's statics read the enum's shape, written inline: they named an object after the enum,
    /// which no module declares, and threw (#480). Parse reads a name, names joined by commas and a
    /// number, keeps case unless told not to, and throws where .NET throws; TryParse leaves the
    /// enum's default when it fails, and the overload that takes a Type null; GetNames and GetValues
    /// follow the values; IsDefined reads its argument as the enum, a number, a declared name, or an
    /// object holding any of them; and an object holding an enum casts back to it.
    /// </summary>
    [SkippableTheory]
    [InlineData("return Enum.Parse<Status>(\"Pending\") == Status.Pending;")]                                   // true
    [InlineData("return Enum.Parse<Status>(\" Inactive \").ToString();")]                                       // "Inactive"
    [InlineData("return Enum.Parse<Status>(\"pending\", true).ToString();")]                                    // "Pending"
    [InlineData("try { Enum.Parse<Status>(\"pending\"); return \"parsed\"; } catch { return \"refused\"; }")]   // "refused"
    [InlineData("return Enum.Parse<Status>(\"2\").ToString();")]                                                // "Inactive"
    [InlineData("return Enum.Parse<Perm>(\"Read, Exec\").ToString();")]                                         // "Read, Exec"
    [InlineData("return Enum.TryParse<Status>(\"Active\", out var s) ? s.ToString() : \"no\";")]                // "Active"
    [InlineData("var ok = Enum.TryParse<Status>(\"nope\", out var s); return ok + \":\" + s;")]                  // "False:Active"
    [InlineData("var ok = Enum.TryParse<Rank>(\"mid\", true, out var r); return ok + \":\" + r;")]               // "True:Mid"
    [InlineData("return Enum.TryParse<Status>(\"Pending\", out _);")]                                            // true
    [InlineData("var s = (Status)0; var ok = Enum.TryParse(\"Inactive\", out s); return ok + \":\" + s;")]       // "True:Inactive"
    [InlineData("return string.Join(\",\", Enum.GetNames<Rank>());")]                                             // "Zeta,Alpha,Mid"
    [InlineData("var v = Enum.GetValues<Rank>(); return v.Length + \":\" + v[0] + \",\" + v[2] + \":\" + (v[2] == Rank.Mid);")] // "3:Zeta,Mid:True"
    [InlineData("return string.Join(\",\", Enum.GetNames(typeof(Perm)));")]                                       // "None,Read,Write,Exec"
    [InlineData("return Enum.IsDefined(Rank.Mid) && !Enum.IsDefined((Rank)3);")]                                 // true
    [InlineData("return Enum.IsDefined(typeof(Rank), 5) && !Enum.IsDefined(typeof(Rank), 2);")]                   // true
    [InlineData("return Enum.IsDefined(typeof(Status), \"Pending\") && !Enum.IsDefined(typeof(Status), \"pending\");")] // true
    [InlineData("return Enum.GetName(Status.Pending) + \",\" + (Enum.GetName(typeof(Status), 7) ?? \"null\") + \",\" + Enum.GetName(typeof(Status), 2) + \",\" + (Enum.GetName(Perm.Read | Perm.Write) ?? \"none\");")] // "Pending,null,Inactive,none"
    [InlineData("object o = Status.Pending; object n = \"Inactive\"; object m = 7; return Enum.IsDefined(typeof(Status), o) + \":\" + Enum.IsDefined(typeof(Status), n) + \":\" + Enum.IsDefined(typeof(Status), m);")] // "True:True:False"
    [InlineData("var ok = Enum.TryParse(typeof(Status), \"nope\", out object? r); return ok + \":\" + (r == null);")] // "False:True"
    [InlineData("var ok = Enum.TryParse(typeof(Status), \"inactive\", true, out object? r); return ok + \":\" + ((Status)r! == Status.Inactive);")] // "True:True"
    [InlineData("return ((Status)Enum.Parse(typeof(Status), \"Pending\")).ToString();")]  // "Pending"
    [InlineData("var s = Status.Inactive; return ((Rank)s).ToString() + \"|\" + (Status)Rank.Alpha;")] // "2|Pending"
    [InlineData("object o = 1; object t = \"Pending\"; string r = ((Status)o).ToString(); try { var s = (Status)t; return r + \"|cast\"; } catch { return r + \"|refused\"; }")] // "Pending|refused"
    [InlineData("try { return Enum.IsDefined(typeof(Status), null).ToString(); } catch (Exception e) { return e.Message; }")] // "Value cannot be null. (Parameter 'value')"
    public void EnumsStatics_ReadTheEnumsShape(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }
}
