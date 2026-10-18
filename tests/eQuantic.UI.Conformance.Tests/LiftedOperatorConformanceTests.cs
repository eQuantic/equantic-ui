using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A struct's operator applied over NULLABLE values is LIFTED in C# (#948): an equality finds two absent
/// values equal and an absent value unequal to any other, a relation is false where a value is absent, and
/// any other operator, a conversion included, answers null, the struct's own operator running only over
/// values. A record struct's equality is the one C# writes for it, member by member, and a struct's own
/// operator is the one the struct declares. The browser compared two nullable record structs by reference,
/// so two equal values built apart were unequal there, and it handed a null to a struct's own operator,
/// which threw reading a member of it.
/// </summary>
public class LiftedOperatorConformanceTests
{
    private const string Values = """
        public readonly record struct Spot(int Row, int Col);

        public readonly struct Reach
        {
            public Reach(int start, int length) { Start = start; Length = length; }
            public int Start { get; }
            public int Length { get; }
            // Equal by the START alone: a lift that calls this operator answers apart from a comparison of
            // the two values member by member.
            public static bool operator ==(Reach a, Reach b) => a.Start == b.Start;
            public static bool operator !=(Reach a, Reach b) => a.Start != b.Start;
            public static bool operator <(Reach a, Reach b) => a.Start < b.Start;
            public static bool operator >(Reach a, Reach b) => a.Start > b.Start;
            public static Reach operator +(Reach a, Reach b) => new(a.Start + b.Start, a.Length + b.Length);
            public static Reach operator -(Reach r) => new(-r.Start, r.Length);
        }

        public readonly struct Probe
        {
            public Probe(int number) { Number = number; }
            public int Number { get; }
            // Declared over the nullable values themselves: C# calls it as it is written, never lifted, so a
            // null reaches it and it answers for it.
            public static bool operator ==(Probe? a, Probe? b) => (a?.Number ?? -1) == (b?.Number ?? -1);
            public static bool operator !=(Probe? a, Probe? b) => !(a == b);
        }

        public readonly record struct Cents(int Amount)
        {
            public static implicit operator Cents(int amount) => new(amount);
            public static explicit operator int(Cents c) => c.Amount;
        }
        """;

    [SkippableTheory]
    // ---- two nullable record structs: both absent, one absent, two values built apart ----
    [InlineData("Spot? a = null, b = null; return a == b;")]                                  // true
    [InlineData("Spot? a = null, b = null; return a != b;")]                                  // false
    [InlineData("Spot? a = new Spot(1, 2), b = null; return a == b;")]                        // false
    [InlineData("Spot? a = new Spot(1, 2), b = null; return a != b;")]                        // true
    [InlineData("Spot? a = null, b = new Spot(1, 2); return a == b;")]                        // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a == b;")]              // true
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a != b;")]              // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(2, 1); return a == b;")]              // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(2, 1); return a != b;")]              // true
    // the shape #948 met: a value kept, the same value built again, the two compared
    [InlineData("Spot? target = new Spot(1, 2); var before = target; target = new Spot(1, 2); return target != before;")] // false
    // ---- a nullable beside a value, on either side ----
    [InlineData("Spot? a = new Spot(1, 2); Spot b = new Spot(1, 2); return a == b;")]         // true
    [InlineData("Spot a = new Spot(1, 2); Spot? b = new Spot(1, 2); return a == b;")]         // true
    [InlineData("Spot? a = null; Spot b = new Spot(1, 2); return a != b;")]                   // true
    [InlineData("Spot a = new Spot(1, 2); Spot? b = null; return a == b;")]                   // false
    // ---- the null literal ----
    [InlineData("Spot? a = null; return a == null;")]                                         // true
    [InlineData("Spot? a = new Spot(1, 2); return a == null;")]                               // false
    [InlineData("Spot? a = new Spot(1, 2); return null != a;")]                               // true
    [InlineData("Spot? a = null; return null == a;")]                                         // true
    // ---- each operand once, in order ----
    [InlineData("var log = \"\"; Spot? At(int i) { log += i; return new Spot(i, 0); } var same = At(1) == At(2); return log + same;")] // "12False"
    // ---- inside a lambda, a LINQ predicate, a query, a guard and a tuple ----
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), null, new Spot(1, 2), new Spot(3, 4) }; Spot? target = new Spot(1, 2); return xs.Count(x => x == target);")] // 2
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), null, new Spot(3, 4) }; Spot? none = null; return xs.Count(x => x == none);")] // 1
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), null, new Spot(3, 4) }; Spot? target = new Spot(1, 2); return xs.Where(x => x != target).Count();")] // 2
    [InlineData("var xs = new List<Spot> { new Spot(1, 2), new Spot(3, 4) }; Spot? target = new Spot(3, 4); return xs.Count(x => x == target);")] // 1
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), new Spot(3, 4) }; Spot? target = new Spot(3, 4); return (from x in xs where x == target select x).Count();")] // 1
    [InlineData("Spot? target = new Spot(1, 2); Func<Spot?, bool> same = x => x == target; return same(new Spot(1, 2)) && !same(null);")] // true
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a switch { var x when x == b => 1, _ => 0 };")] // 1
    [InlineData("(Spot?, int) a = (new Spot(1, 2), 1), b = (new Spot(1, 2), 1); return a == b;")] // true
    public void TwoNullableRecordStructs_CompareByValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- both absent, one absent: answered without the operator ----
    [InlineData("Reach? a = null, b = null; return a == b;")]                                 // true
    [InlineData("Reach? a = null, b = null; return a != b;")]                                 // false
    [InlineData("Reach? a = new Reach(1, 5), b = null; return a == b;")]                      // false
    [InlineData("Reach? a = null, b = new Reach(1, 5); return a != b;")]                      // true
    // ---- two values: the struct's own operator, which reads the start alone ----
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 9); return a == b;")]           // true
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 9); return a != b;")]           // false
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(2, 5); return a == b;")]           // false
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(2, 5); return a != b;")]           // true
    // ---- a nullable beside a value, and the null literal ----
    [InlineData("Reach? a = new Reach(1, 5); Reach b = new Reach(1, 9); return a == b;")]     // true
    [InlineData("Reach a = new Reach(1, 5); Reach? b = null; return a != b;")]                // true
    [InlineData("Reach? a = null; return a == null;")]                                        // true
    [InlineData("Reach? a = new Reach(1, 5); return a != null;")]                             // true
    [InlineData("Reach? a = new Reach(1, 5); return null == a;")]                             // false
    // ---- each operand once, in order ----
    [InlineData("var log = \"\"; Reach? At(int i) { log += i; return new Reach(i, 0); } var same = At(1) != At(2); return log + same;")] // "12True"
    // ---- inside a LINQ predicate, and a tuple's element ----
    [InlineData("var xs = new List<Reach?> { new Reach(1, 5), null, new Reach(1, 9), new Reach(2, 5) }; Reach? target = new Reach(1, 0); return xs.Count(x => x == target);")] // 2
    [InlineData("(Reach?, int) a = (null, 1), b = (null, 1); return a == b;")]                // true
    [InlineData("(Reach?, int) a = (new Reach(1, 5), 1), b = (null, 1); return a != b;")]     // true
    [InlineData("(Reach?, int) a = (new Reach(1, 5), 1), b = (new Reach(1, 9), 1); return a == b;")] // true
    // ---- an operator declared over the nullable values is called as written, a null included ----
    [InlineData("Probe? a = null, b = new Probe(-1); return a == b;")]                        // true
    [InlineData("Probe? a = null, b = null; return a != b;")]                                 // false
    [InlineData("Probe? a = new Probe(3), b = new Probe(3); return a == b;")]                 // true
    public void AStructsOwnEquality_IsLiftedOverNullableValues(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- a relation is false where a value is absent ----
    [InlineData("Reach? a = null, b = new Reach(2, 0); return a < b;")]                       // false
    [InlineData("Reach? a = new Reach(1, 0), b = null; return a > b;")]                       // false
    [InlineData("Reach? a = new Reach(1, 0), b = new Reach(2, 0); return a < b;")]            // true
    // ---- any other operator answers null ----
    [InlineData("Reach? a = new Reach(1, 5), b = null; return a + b;")]                       // null
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(2, 6); return (a + b)?.Length;")]  // 11
    [InlineData("Reach? a = null; return -a;")]                                               // null
    [InlineData("Reach? a = new Reach(1, 5); return (-a)?.Start;")]                           // -1
    [InlineData("Reach? m = null; m += new Reach(2, 0); return m;")]                          // null
    [InlineData("Reach? m = new Reach(1, 1); m += new Reach(2, 2); return m?.Length;")]       // 3
    // ---- a conversion between two nullable types answers null for null ----
    [InlineData("int? n = null; Cents? c = n; return c;")]                                    // null
    [InlineData("int? n = 5; Cents? c = n; return c?.Amount;")]                               // 5
    [InlineData("Cents? c = null; int? n = (int?)c; return n;")]                              // null
    [InlineData("Cents? c = new Cents(3); int? n = (int?)c; return n;")]                      // 3
    [InlineData("Cents? Wrap(int? v) => v; return Wrap(null) is null;")]                      // true
    public void AStructsOwnOperator_IsLiftedOverNullableValues(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- a vocabulary record struct the browser holds as data ----
    [InlineData("Color? a = Color.FromRgb(1, 2, 3), b = Color.FromRgb(1, 2, 3); return a == b;")]    // true
    [InlineData("Color? a = Color.FromRgb(1, 2, 3), b = Color.FromRgb(3, 2, 1); return a != b;")]    // true
    [InlineData("Color? a = Color.FromRgb(1, 2, 3), b = null; return a != b;")]                      // true
    [InlineData("Color? a = null, b = null; return a == b;")]                                        // true
    [InlineData("Color? a = Color.White; return a == Color.FromRgb(255, 255, 255);")]                // true
    public void TwoNullableVocabularyValues_CompareByValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    /// <summary>The spreadsheet's own range, where #948 was found, and an app's own record struct and
    /// struct, built as an app's build builds them: one module per type, each importing what it names.</summary>
    private const string App = """
        using eQuantic.UI.Sheet;

        public readonly record struct Spot(int Row, int Col);

        public readonly struct Reach
        {
            public Reach(int start, int length) { Start = start; Length = length; }
            public int Start { get; }
            public int Length { get; }
            public static bool operator ==(Reach a, Reach b) => a.Start == b.Start;
            public static bool operator !=(Reach a, Reach b) => a.Start != b.Start;
        }

        public sealed class Filler
        {
            public SheetRange? Target { get; private set; }
            public void Update(int row) => Target = new SheetRange(new CellRef(row, 0), new CellRef(row, 3));
            public bool Moves(int row)
            {
                var before = Target;
                Update(row);
                return Target != before;
            }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("the same range built again is no move", "var f = new Filler(); f.Update(2); return f.Moves(2);"),
        ("another range is a move", "var f = new Filler(); f.Update(2); return f.Moves(3);"),
        ("a range where there was none is a move", "var f = new Filler(); return f.Moves(2);"),
        ("two absent ranges are equal", "SheetRange? a = null, b = null; return a == b;"),
        ("a range and the same range built apart", "SheetRange? a = new SheetRange(new CellRef(1, 1)); return a == new SheetRange(new CellRef(1, 1));"),
        ("an app's record struct", "Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a == b;"),
        ("an app's record struct beside an absent one", "Spot? a = new Spot(1, 2), b = null; return a != b;"),
        ("an app's struct, by its own operator", "Reach? a = new Reach(1, 5), b = new Reach(1, 9); return a == b;"),
        ("an app's struct beside an absent one", "Reach? a = null, b = new Reach(1, 5); return a == b;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullableStructsEquality_IsLiftedInAnAppsBuild(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(App, typeAnnotations, Cases);
}
