using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A record or a struct is built as C# builds it: its constructor runs every initializer, in
/// declaration order, a derived record's before its base's constructor, and then its own body; an
/// object initializer runs once the constructor has returned; a <c>with</c> copies and runs nothing
/// (#413). A positional parameter whose property the record declares is one member (#546).
/// </summary>
public class RecordConstructionConformanceTests
{
    private const string Prelude = """
        public record Log { public static string Text = ""; public static int Note(string s) { Text += s; return 0; } }

        public record Counted { public static int N; public int A = ++N; public int B { get; init; } = ++N; }

        public record Base0 { public int P = Log.Note("base-init "); public Base0() { Log.Note("base-ctor "); } }
        public record Derived(int X) : Base0 { public int Q = Log.Note("derived-init "); }

        public record Ordered
        {
            public int X = Log.Note("x-init ");
            public int Y { get; init; } = Log.Note("y-init ");
            public Ordered() { Log.Note("ctor "); }
        }

        public record ExplicitCtor { public int A { get; init; } public int B = 7; public ExplicitCtor(int a) { A = a * 2; } }
        public record Span(int Start, int End) { public Span(int at) : this(at, at + 1) { Log.Note("one "); } }
        public record Box3(int A, int B, int C) { public Box3(int a, int b = 5) : this(a, b, 0) { } }
        public record Opt(int A, int B = 4, int C = 6) { public Opt(string s, string t, string u, string v) : this(s.Length, C: t.Length) { } }
        public record Bag(params int[] Items) { public int Count => Items.Length; }
        public struct Money { public decimal A; public string C; public Money(decimal a) { A = a; C = "EUR"; } public Money(decimal a, string c) { A = a; C = c; } }
        public record Shape(string Kind) { public int Trace = Log.Note("shape "); }
        public record Rect : Shape
        {
            public int W; public int H;
            public int Seen = Log.Note("rect ");
            public Rect(int side) : base("square") { W = side; H = side; }
            public Rect(int w, int h) : base("rect" + (w * h)) { W = w; H = h; Log.Note("body "); }
            public Rect() : this(1) { Log.Note("unit "); }
        }
        public record Early
        {
            public int A; public string Trail = "";
            public Early(int a) { A = a; if (a < 0) return; Trail += "body "; }
            public Early(int a, int b) { A = a + b; Trail += "sum "; }
            public Early() : this(-1) { Trail += "alt"; }
        }
        public record Tags(string Name, params string[] Values) { public Tags() : this("none", "a", "b") { } }
        public record Listed(string Name, params string[] Values) { public Listed() : this("w", new[] { "x", "y", "z" }) { } }

        public struct Tally { public int N; public Tally() { N = 3; } }
        public struct Pinned { public int V; public Pinned(int v) { V = v * 10; } }
        public record struct Reading(int Id) { public string Unit { get; init; } = "kg"; }

        public sealed record Box(int X, int Y) { public int X { get; set; } = X; }
        public sealed record Scaled(int X, int Y) { public int X { get; } = X * 10; }

        public record Animal(string Name);
        public record Dog(string Name, string Breed) : Animal(Name);
        public record Quiet : Animal { public Quiet() : base("q") { } }
        public record Unit;
        public record Secret(int Shown) { private int _hidden = Shown * 2; public int Hidden => _hidden; }
        """;

    [SkippableTheory]
    // Every initializer runs, whatever the object initializer then sets (#413).
    [InlineData("var r = new Counted { B = 10 }; return Counted.N + \"|\" + r.A + \"|\" + r.B;")]          // "2|1|10"
    // The object initializer's value is evaluated after the initializers ran.
    [InlineData("var c = new Counted { B = Counted.N }; return c.A + \"|\" + c.B;")]                       // "1|2"
    // A `with` copies and runs none of them.
    [InlineData("var s = new Counted(); var t = s with { A = 5 }; return Counted.N + \"|\" + t.A + \"|\" + t.B;")] // "2|5|2"
    // A derived record's initializers run before its base's constructor.
    [InlineData("Log.Text = \"\"; new Derived(1); return Log.Text;")]                                       // "derived-init base-init base-ctor "
    [InlineData("var d = new Derived(1) { Q = 9 }; Log.Text = \"\"; var e = d with { X = 4 }; return Log.Text + e.Q + e.X + e.P;")] // "940"
    // Initializers, then the constructor's body, then the object initializer.
    [InlineData("Log.Text = \"\"; new Ordered { Y = Log.Note(\"value \") }; return Log.Text;")]            // "x-init y-init ctor value "
    // An explicit constructor's body runs, and an alternate reaches the main one, then runs its own.
    [InlineData("return new ExplicitCtor(3).A + \"|\" + new ExplicitCtor(3).B;")]                          // "6|7"
    [InlineData("Log.Text = \"\"; var s = new Span(4); return s.Start + \"|\" + s.End + \"|\" + Log.Text;")] // "4|5|one "
    [InlineData("return new Quiet().Name;")]                                                              // "q"
    // An alternate is reached by the counts of arguments it takes, its own defaults included, and a
    // parameter of the main one its chain leaves out takes its default.
    [InlineData("return new Box3(1) + \"|\" + new Box3(1, 2) + \"|\" + new Box3(a: 7) + \"|\" + new Box3(1, 2, 3);")] // "Box3 { A = 1, B = 5, C = 0 }|…"
    [InlineData("return new Opt(\"ab\", \"c\", \"d\", \"e\") + \"|\" + new Opt(1, C: 2);")]              // "Opt { A = 2, B = 4, C = 1 }|Opt { A = 1, B = 4, C = 2 }"
    // Constructors that each do their own work are each a branch, by how many arguments arrive, with
    // its own base constructor's arguments, and an alternate runs its body after its root's, even when
    // the root's returns early.
    [InlineData("return new Money(1.5m).A + new Money(1.5m).C + \"|\" + new Money(2m, \"USD\").A + new Money(2m, \"USD\").C + \"|\" + default(Money).C;")] // "1.5EUR|2USD|"
    [InlineData("Log.Text = \"\"; var r = new Rect(2); var q = new Rect(2, 3); return r.Kind + r.W + r.H + \"|\" + q.Kind + q.W + q.H + \"|\" + Log.Text;")]
    [InlineData("Log.Text = \"\"; var u = new Rect(); return u.Kind + u.W + \"|\" + Log.Text + \"|\" + (new Rect(2, 3) == new Rect(2, 3));")]
    [InlineData("return new Early().Trail + \"|\" + new Early().A + \"|\" + new Early(4).Trail + \"|\" + new Early(1, 2).A + new Early(1, 2).Trail;")] // "alt|-1|body |3sum "
    // A `params` parameter takes the elements listed, or the array passed whole.
    [InlineData("return new Bag(1, 2, 3).Count + \"|\" + new Bag(new[] { 4, 5 }).Count + \"|\" + new Bag().Count;")] // "3|2|0"
    [InlineData("return string.Join(\",\", new Tags().Values) + \"|\" + string.Join(\",\", new Listed().Values) + \"|\" + new Tags(\"t\").Values.Length;")] // "a,b|x,y,z|0"
    // A struct's parameterless constructor runs; its default and its implicit one run nothing.
    [InlineData("return new Tally().N + \"|\" + default(Tally).N;")]                                       // "3|0"
    [InlineData("return new Pinned(2).V + \"|\" + new Pinned().V + \"|\" + default(Pinned).V;")]           // "20|0|0"
    [InlineData("return (new Reading().Unit == null) + \"|\" + new Reading(3).Unit;")]                     // "True|kg"
    [InlineData("var a = new Reading[1]; return a[0].Unit == null;")]                                      // true
    public void ARecordOrAStruct_IsBuiltAsCSharpBuildsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // A positional parameter whose property the record declares is ONE member, the declared one (#546).
    [InlineData("var b = new Box(1, 2); b.X = 5; return b.X + \"|\" + b.Y;")]                              // "5|2"
    [InlineData("return new Box(1, 2).ToString();")]                                                      // "Box { Y = 2, X = 1 }"
    [InlineData("return (new Box(1, 2) == new Box(1, 2)) + \"|\" + (new Box(1, 2) == new Box(1, 3));")]     // "True|False"
    [InlineData("return (new Box(1, 2) with { X = 7 }).ToString();")]                                     // "Box { Y = 2, X = 7 }"
    [InlineData("var (x, y) = new Box(3, 4); return x * 10 + y;")]                                         // 34
    [InlineData("return new Scaled(2, 3).X + \"|\" + new Scaled(2, 3);")]                                  // "20|Scaled { Y = 3, X = 20 }"
    // A record compares its runtime type, prints its base's members first, and `Name { }` for none.
    [InlineData("return (new Animal(\"a\") == new Dog(\"a\", \"b\")) + \"|\" + (new Dog(\"a\", \"b\") == new Dog(\"a\", \"b\"));")] // "False|True"
    [InlineData("return new Dog(\"Rex\", \"Lab\").ToString() + \"|\" + new Unit();")]                      // "Dog { Name = Rex, Breed = Lab }|Unit { }"
    [InlineData("return (new Quiet() == new Quiet()) + \"|\" + new Quiet();")]                             // "True|Quiet { Name = q }"
    // A private field is part of the value and not of its text, and a public computed property is
    // part of its text and not of its value.
    [InlineData("return new Secret(2).Hidden + \"|\" + new Secret(2) + \"|\" + (new Secret(2) == new Secret(2));")] // "4|Secret { Shown = 2, Hidden = 4 }|True"
    public void ARecordsMembers_AreEachOnce_AndItsTextIsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
