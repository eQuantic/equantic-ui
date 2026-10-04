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
