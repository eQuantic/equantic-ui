using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for enums (regression for #13). Enum members compile to their member-name string,
/// so equality/switch/ternary behave identically to .NET. (enum.ToString() is intentionally not
/// asserted: the transpiler emits a camelCase name while .NET returns the PascalCase name — a
/// documented representation choice, not a conformance target.)
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
        """;

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
    public void AnEnumsText_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }

    /// <summary>
    /// Enum's statics read the enum's shape, written inline: they named an object after the enum,
    /// which no module declares, and threw (#480). Parse reads a name, names joined by commas and a
    /// number, keeps case unless told not to, and throws where .NET throws; TryParse leaves the
    /// enum's default when it fails; GetNames and GetValues follow the values; IsDefined reads its
    /// argument as the enum, a number or a declared name.
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
    public void EnumsStatics_ReadTheEnumsShape(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Enums);
    }
}
