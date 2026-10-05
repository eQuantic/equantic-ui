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

    /// <summary>
    /// What a twin's constructor binds, each in a prelude of its own so a type that does not load fails
    /// its own case: a constructor's parameter is a variable its body may assign (it was a `const`), the
    /// locals the constructor gives itself are names no member takes (`const $a` met the rest parameter
    /// `$a`), an abstract property holds nothing (its base's constructor wrote over the derived getter),
    /// and a struct's primary constructor parameter read only by an initializer is the constructor's.
    /// </summary>
    [SkippableTheory]
    [InlineData("public record Tag(string Name) { public Tag(string raw, bool trim) : this(raw) { raw = raw.Trim(); Clean = raw; } public string Clean { get; set; } = \"\"; }",
        "return new Tag(\" x \", true).Clean + \"|\" + new Tag(\" x \", true).Name;")]                                       // "x| x "
    [InlineData("public record Shape0(string Kind); public record Rect0 : Shape0 { public int K = 1; public int A { get; init; } = 2; public Rect0(int s) : base(\"sq\") { } public Rect0(int w, int h) : base(\"r\") { } }",
        "return new Rect0(1).K + new Rect0(1, 2).A + \"|\" + new Rect0(1, 2).Kind;")]                                          // "3|r"
    [InlineData("public abstract record Shape2 { public abstract string Name { get; } public string Describe() => \"I am \" + Name; } public record Circle2(double R) : Shape2 { public override string Name => \"circle\"; }",
        "var c = new Circle2(2); return c.Name + \"|\" + c.Describe() + \"|\" + (c == new Circle2(2));")]                      // "circle|I am circle|True"
    [InlineData("public readonly struct Point2(int x, int y) { public int X { get; } = x; public int Y { get; } = y; }",
        "var p = new Point2(3, 4); return p.X + \"|\" + p.Y + \"|\" + p.Equals(new Point2(3, 4)) + \"|\" + default(Point2).X;")] // "3|4|True|0"
    public void ATwinsConstructor_BindsAsCSharpBinds(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }

    /// <summary>
    /// A constructor's arguments land as C# binds them (BoundArguments): each in its parameter's place,
    /// evaluated in the order it is written, an array passed whole to a params parameter spread and an
    /// element packed into one passed as it is, through a `new`, a `: this(…)`, a `: base(…)` and a base
    /// clause alike, and the variables an argument declares (`out var n`) declared where it is evaluated.
    /// </summary>
    [SkippableTheory]
    [InlineData("public record Shape(string Kind, int Sides = 0, string Color = \"black\"); public record Square : Shape { public Square() : base(\"square\", Color: \"red\") { } }",
        "var s = new Square(); return s.Sides + \"|\" + s.Color + \"|\" + s.Kind;")]                                    // "0|red|square"
    [InlineData("public abstract record Shape3(string Kind); public record Circle3(double Radius) : Shape3(DefaultKind) { public const string DefaultKind = \"circle\"; }",
        "return new Circle3(1).Kind;")]                                                                              // "circle"
    [InlineData("public record Animal(string Name, int Age); public record Pet : Animal { public Pet(string n, int a) : base(Age: a, Name: n) { } }",
        "var p = new Pet(\"rex\", 3); return p.Name + p.Age;")]                                                       // "rex3"
    [InlineData("public record Animal2(string Name, int Legs = 4); public record Bird(string Name) : Animal2(Legs: 2, Name: Name);",
        "var b = new Bird(\"tweety\"); return b.Name + b.Legs;")]                                                     // "tweety2"
    [InlineData("public record Bag(int Tag, params int[] Items) { public int Count => Items.Length; }",
        "var arr = new[] { 4, 5, 6 }; return new Bag(1, Items: arr).Count + \"|\" + new Bag(1, 2, 3).Count + \"|\" + new Bag(1).Count + \"|\" + new Bag(1, arr).Count;")] // "3|2|0|3"
    [InlineData("public record Box2(params object[] Items) { public int Count => Items.Length; }",
        "var ints = new[] { 1, 2 }; return new Box2(ints).Count + \"|\" + new Box2(1, \"a\").Count;")]               // "1|2"
    [InlineData("public record Tags2(string Title, params string[] Values) { public int Count => Values.Length; }",
        "return new Tags2(Title: \"x\", \"a\", \"b\").Count;")]                                                     // 2
    [InlineData("public record Log2 { public static string Text = \"\"; public static int Note(string s, int v) { Text += s; return v; } } public record Pair(int A, int B) { public Pair(string s) : this(B: Log2.Note(\"b\", 2), A: Log2.Note(\"a\", 1)) { } }",
        "Log2.Text = \"\"; var p = new Pair(\"x\"); var first = Log2.Text; Log2.Text = \"\"; var q = new Pair(B: Log2.Note(\"d\", 4), A: Log2.Note(\"c\", 3)); return first + \"|\" + p.A + p.B + \"|\" + Log2.Text + \"|\" + q.A + q.B;")] // "ba|12|dc|34"
    [InlineData("public record Size(int W, int H) { public Size(string text) : this(int.TryParse(text, out var n) ? n : 0, n) { } }",
        "return new Size(\"4\").W + \"|\" + new Size(\"4\").H + \"|\" + new Size(\"z\").W;")]                     // "4|4|0"
    // More arguments out of their order than a template's holes: still evaluated where written (found by
    // Copilot's review of #608, where eleven kept the parameter order).
    [InlineData("public record Log3 { public static string Text = \"\"; public static int Note(string s, int v) { Text += s; return v; } } public record Wide(int A, int B, int C, int D, int E, int F, int G, int H, int I, int J, int K);",
        "Log3.Text = \"\"; var w = new Wide(K: Log3.Note(\"k\", 11), A: Log3.Note(\"a\", 1), B: Log3.Note(\"b\", 2), C: Log3.Note(\"c\", 3), D: Log3.Note(\"d\", 4), E: Log3.Note(\"e\", 5), F: Log3.Note(\"f\", 6), G: Log3.Note(\"g\", 7), H: Log3.Note(\"h\", 8), I: Log3.Note(\"i\", 9), J: Log3.Note(\"j\", 10)); return Log3.Text + \"|\" + w.A + \"|\" + w.K;")] // "kabcdefghij|1|11"
    public void AConstructorsArguments_LandAsCSharpBindsThem(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }
}
