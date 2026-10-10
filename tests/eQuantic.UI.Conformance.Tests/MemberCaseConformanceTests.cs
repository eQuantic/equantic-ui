using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A field and a property or a method a case apart stay two members in the browser, both sides executed
/// through the module graph an app's build writes, one case at a time. They lowered to one name: the
/// own field hid the accessors, so a setter never ran, an auto-property and its field shared one slot,
/// and a call reached the number the field held (#396). The field moves now, to a slot with a `$` after
/// its name, and a pattern reads it there too: a property subpattern named the member by its text, and
/// read the property, and a positional one read the members its Deconstruct's outs are named after,
/// the property again once the field had moved (Copilot's first review of #696). So does a call of a
/// delegate field, which named the method a case apart from it, and a method that forwards to its
/// delegate field called itself and never returned, and a field called Count, which the collections'
/// table read as the method `count()` (found in the same review's sweep). A slot an ancestor holds is
/// taken whatever the spellings, so a derived field beside an inherited one moves, and a pattern-matching
/// operation calls a Deconstruct the app wrote once for each value, in an initializer, across the arms of
/// a switch and the alternatives of an `or` (Copilot's second review of #696). An explicit implementation
/// counts under the name of the member it implements, as the emitter writes it (Copilot's third review).
/// </summary>
public class MemberCaseConformanceTests
{
    private const string Declarations = """
        public class Doubler { int value; public int Value { get => value; set => this.value = value * 2; } }
        public class Named { string name = "ana"; public string Name { get; set; } = "bia"; public string Both() => name + Name; }
        public class Sized { int size = 3; public int Size() => size * 2; }
        public class Counted { int count; public int Count => count; public void Set() { count = 2; } }
        public class Pair { int value = 1; public int Value => value * 10; public int Sum(Pair other) => other.value + value; }
        public class Base { public int Total { get; set; } = 5; }
        public class Derived : Base { int total = 7; public int Own() => total + Total; }
        public class Made { int value; public int Value { get => value; set => this.value = value * 2; } public static Made Make() => new Made { value = 5 }; }
        public class Matched
        {
            int value = 1; public int Value => value * 10;
            Matched next; public Matched Next => null;
            public Matched(Matched next) { this.next = next; }
            public bool Field() => this is { value: 1 };
            public int Bound() => this is { value: var v } ? v : -1;
            public string Arm() => this switch { { value: 1 } => "field", { Value: 10 } => "property", _ => "none" };
            public string Case() { switch (this) { case { value: 1 }: return "field"; default: return "none"; } }
            public bool Through() => this is { next.value: 1 };
        }
        public class Tally { public int Count = 2; public int count() => Count * 3; public int Twice() => this.Count * 2; }
        public class Validator { System.Func<int, bool> validate = n => n > 2; public bool Validate(int n) => validate(n); }
        public class Checked
        {
            System.Func<int, bool> check = n => n > 2;
            public bool Check(int n) => n > 100;
            public bool Bare(int n) => check(n);
            public bool Through(int n) => this.check(n);
            public bool Other(Checked other, int n) => other.check(n);
            public bool Guarded(Checked other, int n) => other?.check(n) == true;
        }
        public class Point { int x; int y; public int X => x * 10; public int Y => y * 10; public Point(int x, int y) { this.x = x; this.y = y; } public void Deconstruct(out int x, out int y) { x = this.x; y = this.y; } }
        public class Gauge { int low = 1; int high = 2; public int Low => low * 10; public int High => high * 10; public int RawLow() => low; public int RawHigh() => high; }
        public static class GaugeParts { public static void Deconstruct(this Gauge gauge, out int low, out int high) { low = gauge.RawLow(); high = gauge.RawHigh(); } }
        public class Lower { public int Value = 2; public int Read() => Value; }
        public class Upper : Lower { int value = 3; public int Own() => value; }
        public class Elder { int count = 1; public int Mine() => count; }
        public class Heir : Elder { int count = 5; public int Theirs() => count; }
        public class Store { int value = 1; public int Value => value * 10; }
        public class Shop : Store { int value = 4; public int Mine() => value; }
        public class Tracked
        {
            public static int Calls;
            int x; int y;
            public Tracked(int x, int y) { this.x = x; this.y = y; }
            public void Deconstruct(out int x, out int y) { Calls++; x = this.x; y = this.y; }
        }
        public interface IReads { int Value(); }
        public class ReadsExplicitly : IReads { int value = 3; int IReads.Value() => value * 10; public int Own() => value; }
        public interface ISized { int Size { get; } }
        public class SizedExplicitly : ISized { int size = 4; int ISized.Size => size * 10; public int Own() => size; }
        public interface INotifies { event System.Action Changed; }
        public class NotifiesExplicitly : INotifies
        {
            int changed = 5;
            System.Action handlers;
            event System.Action INotifies.Changed { add { handlers += value; } remove { handlers -= value; } }
            public int Own() => changed;
        }
        public class Initialized
        {
            public bool Matched = new Tracked(1, 2) is (1, 2);
            public bool Also { get; } = new Tracked(1, 2) is (1, _);
            public int Bound = new Tracked(1, 2) is (var a, var b) ? a * 10 + b : -1;
            public string Arm = new Tracked(3, 4) switch { (1, _) => "one", (var a, var b) => $"{a}{b}" };
        }
        """;

    public static TheoryData<string, string, bool> Cases()
    {
        var cases = new (string Name, string Statements)[]
        {
            ("a setter beside its field", "var d = new Doubler(); d.Value = 3; return d.Value;"),
            ("an auto-property beside a field", "return new Named().Both();"),
            ("a method beside a field", "return new Sized().Size();"),
            ("a getter beside its field", "var c = new Counted(); c.Set(); return c.Count;"),
            ("another instance's field", "var a = new Pair(); return a.Sum(new Pair()) + a.Value;"),
            ("a field beside a base's property", "return new Derived().Own();"),
            ("an initializer naming the field", "return Made.Make().Value;"),
            ("a property pattern naming the field", "return new Matched(null).Field();"),
            ("a pattern binding the field", "return new Matched(null).Bound();"),
            ("a switch arm naming the field", "return new Matched(null).Arm();"),
            ("a case label naming the field", "return new Matched(null).Case();"),
            ("an extended pattern through a field", "return new Matched(new Matched(null)).Through();"),
            ("a pattern naming a field called Count", "return new Tally() is { Count: 2 };"),
            ("a positional pattern through the app's Deconstruct", "return new Point(1, 2) is (1, 2);"),
            ("a positional pattern binding through the app's Deconstruct", "return new Point(1, 2) is (var a, var b) ? a * 100 + b : -1;"),
            ("a positional pattern through an extension Deconstruct", "return new Gauge() is (1, 2);"),
            ("a method forwarding to its delegate field", "return new Validator().Validate(3);"),
            ("a delegate field called by its name", "return new Checked().Bare(3);"),
            ("a delegate field called through this", "return new Checked().Through(3);"),
            ("another instance's delegate field, called", "return new Checked().Other(new Checked(), 3);"),
            ("a delegate field called through ?.", "return new Checked().Guarded(new Checked(), 3);"),
            ("a field called Count, read", "var t = new Tally(); return t.Count + t.count() + t.Twice();"),
            ("a derived field a case apart from an inherited field", "var u = new Upper(); return u.Read() * 10 + u.Own();"),
            ("a derived field of an inherited field's own name", "var h = new Heir(); return h.Mine() * 10 + h.Theirs();"),
            ("a derived field past an inherited moved field", "var s = new Shop(); return s.Value + s.Mine();"),
            ("a Deconstruct in initializers, called once each", "Tracked.Calls = 0; var i = new Initialized(); return $\"{i.Matched}|{i.Also}|{i.Bound}|{i.Arm}|{Tracked.Calls}\";"),
            ("a Deconstruct across the arms of a switch expression, called once", "Tracked.Calls = 0; var t = new Tracked(3, 4); var r = t switch { (1, _) => \"one\", (var a, var b) => $\"{a}{b}\" }; return r + \":\" + Tracked.Calls;"),
            ("a Deconstruct across the labels of a switch statement, called once", "Tracked.Calls = 0; var t = new Tracked(3, 4); string r; switch (t) { case (1, _): r = \"one\"; break; case (var a, var b): r = $\"{a}{b}\"; break; default: r = \"none\"; break; } return r + \":\" + Tracked.Calls;"),
            ("a Deconstruct across the alternatives of an or, called once", "Tracked.Calls = 0; var t = new Tracked(3, 4); var r = t is (1, _) or (_, 4); return r + \":\" + Tracked.Calls;"),
            ("a Deconstruct in a loop's test, called once a test", "Tracked.Calls = 0; var n = 0; for (var i = 0; i < 3; i++) { if (new Tracked(i, 0) is (1, _) or (2, _)) n++; } return n * 10 + Tracked.Calls;"),
            ("a field beside an explicit method a case apart", "IReads r = new ReadsExplicitly(); return r.Value() + ((ReadsExplicitly)r).Own();"),
            ("a field beside an explicit property a case apart", "ISized s = new SizedExplicitly(); return s.Size + ((SizedExplicitly)s).Own();"),
            ("a field beside an explicit event a case apart", "var n = new NotifiesExplicitly(); INotifies i = n; i.Changed += () => { }; return n.Own();"),
        };
        var data = new TheoryData<string, string, bool>();
        foreach (var (name, statements) in cases)
        {
            data.Add(name, statements, true);
            data.Add(name, statements, false);
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public void AFieldAndAMemberACaseApart_StayTwoMembers(string name, string statements, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, (name, statements));
}
