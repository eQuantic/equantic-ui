using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type's statics start at their zero, then initialize in declaration order, then run the static
/// constructor, the first time anything touches them, as C# runs them (#417), in a record, a struct,
/// a class, a static class and a component. The twins defined each static where it was declared, so
/// an initializer reading a static declared after it read undefined, and a class's statics each
/// initialized on their own first read.
/// </summary>
public class StaticInitializationConformanceTests
{
    private const string Records = """
        public record Chain { public static int A = B + 1; public static int B = 2; public int N; }
        public record Early { public static Early First = new(); public static int Seed = 3; public int Value = Seed * 2; }
        public record Self { public static int X = X + 1; }
        public record Lookup { public static readonly int Code = Compute(); static int Compute() => Factor * 2; public static int Factor = 4; }
        public record Stored
        {
            public static int First = Second + 1;
            public static int Second { get; set => field = value * 2; } = 5;
        }
        public record Counter { public static int Count; public static readonly string Label = "c" + Count; static Counter() { Count = 10; } }
        public struct Gauge { public static int Z = W + 1; public static int W = 3; public int V; }
        public record Registry { public static List<string> Names; static Registry() { Names = new List<string> { "a" }; Names.Add("b"); } }
        public record Trail { public static string Text = ""; }
        public record Pinged { static Pinged() { Trail.Text += "cctor "; } public static int Ping() { Trail.Text += "ping "; return 1; } }
        public record Built { static Built() { Trail.Text += "cctor "; } public Built() { Trail.Text += "ctor "; } }
        public record Rated { static Rated() { Trail.Text += "cctor "; } public static int Rate => 3; }
        public struct Tick { public int V; static Tick() { Trail.Text += "cctor "; } public Tick(int v) { V = v; Trail.Text += "ctor "; } public static Tick operator +(Tick a, Tick b) { Trail.Text += "plus "; return new Tick(a.V + b.V); } }
        public struct Rgb { public byte R, G, B; public static Rgb Black; static Rgb() { Black = new Rgb { R = 1 }; } }
        public record Gate { public static int X = 5; public static string Seen = ""; static Gate() { Seen = "cctor"; if (X > 0) return; Seen = "late"; } }
        public record Shaky { public static int A = Fail(); public static int B = 3; static int Fail() => throw new InvalidOperationException("x"); }
        public record Bounds { public static readonly int Max = Default * 2; public const int Default = 50; public static readonly long Wide = Default * 3L; }
        """;

    [SkippableTheory]
    [InlineData("return Chain.A + \"|\" + Chain.B;")]                                   // "1|2"
    [InlineData("return Early.First.Value + \"|\" + new Early().Value;")]               // "0|6"
    [InlineData("return Self.X;")]                                                      // 1
    [InlineData("return Lookup.Code + \"|\" + Lookup.Factor;")]                         // "0|4"
    [InlineData("return Stored.First + \"|\" + Stored.Second;")]                        // "1|5"
    [InlineData("Stored.Second = 4; return Stored.First + \"|\" + Stored.Second;")]     // "1|8"
    [InlineData("return Counter.Label + \"|\" + Counter.Count;")]                       // "c0|10"
    [InlineData("return Gauge.Z + \"|\" + Gauge.W;")]                                   // "1|3"
    [InlineData("Chain.B = 7; return Chain.A + \"|\" + Chain.B;")]                      // "1|7"
    [InlineData("return Counter.Count + \"|\" + Counter.Label;")]                       // "10|c0"
    [InlineData("return Registry.Names.Count + \"|\" + Registry.Names[1];")]            // "2|b"
    // A static constructor runs before the first use of any static member, a method or a computed
    // property included, and before the first instance: not only before the first read of a static.
    [InlineData("Trail.Text = \"\"; Pinged.Ping(); Pinged.Ping(); return Trail.Text;")]              // "cctor ping ping "
    [InlineData("Trail.Text = \"\"; new Built(); new Built(); return Trail.Text;")]                // "cctor ctor ctor "
    [InlineData("Trail.Text = \"\"; var r = Rated.Rate; return Trail.Text + r;")]                  // "cctor 3"
    [InlineData("Trail.Text = \"\"; var t = new Tick(1) + new Tick(2); return Trail.Text + t.V;")] // "cctor ctor ctor plus ctor 3"
    // The type is used while it initializes: a static's zero is an instance whose constructor starts
    // the type again, which finds it started and goes on, where it started over without end.
    [InlineData("return Rgb.Black.R + \"|\" + default(Rgb).R;")]                                      // "1|0"
    // A static constructor's `return` ends the constructor, and the type is initialized.
    [InlineData("return Gate.X + \"|\" + Gate.Seen;")]                                                 // "5|cctor"
    // An initializer that throws fails every use of the type, the first included, with the
    // TypeInitializationException that carries what it threw.
    [InlineData("string Read() { try { return Shaky.B.ToString(); } catch (Exception e) { return e is TypeInitializationException { InnerException: InvalidOperationException { Message: \"x\" } } ? \"tie\" : \"other\"; } } return Read() + \"|\" + Read();")] // "tie|tie"
    // A static that C# folds to a constant is its value, read before the later constant it names is
    // defined: NaN where .NET answers 100.
    [InlineData("return Bounds.Max + \"|\" + Bounds.Wide;")]                                          // "100|150"
    public void ARecordsStatics_InitializeAsCSharpRunsThem(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Records);
    }

    private const string Classes = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public static class Log { public static string Text = ""; public static int Note(string s) { Text += s; return 0; } }

        public class Pair { public static int A = B + 1; public static int B = 2; }

        public static class Squares
        {
            public static readonly int[] Table = Build();
            public static int Size = 3;
            private static int[] Build()
            {
                var table = new int[Size];
                for (var i = 0; i < table.Length; i++) table[i] = i * i;
                return table;
            }
        }

        public class Near { public static int X = Far.Y + 1; }
        public class Far { public static int Y = 5; }

        public class Ping { public static int A = Pong.B + 1; }
        public class Pong { public static int B = Ping.A + 10; }

        public class Timed { public static int Value = Log.Note("init "); static Timed() { Log.Note("cctor "); } }

        public class Tally { public static int Count; public static string Name; }

        public class Catalog
        {
            public static List<string> Names;
            public static int Size { get; private set; }
            static Catalog() { Names = new List<string> { "a", "b" }; Size = Names.Count; }
        }

        public class Pinger { static Pinger() { Log.Note("cctor "); } public static int Ping() { Log.Note("ping "); return 1; } }
        public class Maker { static Maker() { Log.Note("cctor "); } public Maker() { Log.Note("ctor "); } }
        public class Rater { static Rater() { Log.Note("cctor "); } public static int Rate => 3; }

        public sealed class Dial : StatelessComponent
        {
            static Dial() { Log.Note("dial "); }
            public static int Size() => 1;
            public override VisualNode Build(ComponentContext context) => new Text("dial", TypeRole.BodyM);
        }

        public sealed class Panel : StatelessComponent
        {
            public static int A = B + 1;
            public static int B = 2;
            public static int C;
            static Panel() { C = A * 10; }
            public override VisualNode Build(ComponentContext context) => new Text("panel", TypeRole.BodyM);
        }

        public static class Config { public static int X = 5; public static string Seen = ""; static Config() { Seen = "cctor"; if (X > 0) return; Seen = "late"; } }
        public static class Pool { public static int Size; static Pool() { var slots = 4; Size = slots; } }
        public static class Boom { public static int A = Fail(); public static int B = 3; static int Fail() => throw new InvalidOperationException("x"); }
        public class Fuse { public static int Lit = 1; static Fuse() { throw new InvalidOperationException("cctor"); } public static int Light() => Lit; }
        public class Bus { public static event Action Changed; static Bus() { Changed += () => Log.Text += "default "; } public static void Raise() => Changed?.Invoke(); }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("a class", "return Pair.A + \"|\" + Pair.B;"),
        ("a static class whose initializer calls a method reading a later static", "return Squares.Table.Length + \"|\" + Squares.Size;"),
        ("a static read across two types", "return Near.X + \"|\" + Far.Y;"),
        ("two types whose statics read each other", "return Ping.A + \"|\" + Pong.B;"),
        ("the static constructor after the initializers, on first use", "Log.Text = \"\"; var v = Timed.Value; return Log.Text + v;"),
        ("a component", "return Panel.A + \"|\" + Panel.B;"),
        ("a component's static its static constructor sets", "return Panel.C + \"|\" + Panel.A;"),
        ("a static with no initializer, at its zero", "Tally.Count++; return Tally.Count + \"|\" + (Tally.Name == null);"),
        ("statics a static constructor sets, read first", "return Catalog.Size + \"|\" + Catalog.Names[1];"),
        ("a static method starts the static constructor", "Log.Text = \"\"; Pinger.Ping(); Pinger.Ping(); return Log.Text;"),
        ("the first instance starts the static constructor", "Log.Text = \"\"; new Maker(); new Maker(); return Log.Text;"),
        ("a computed static property starts the static constructor", "Log.Text = \"\"; var r = Rater.Rate; return Log.Text + r;"),
        ("a component's static method starts its static constructor", "Log.Text = \"\"; var s = Dial.Size(); return Log.Text + s;"),
        // The static constructor runs in a function of its own: its `return` ends it, and its locals
        // meet none of the initializer's.
        ("a static constructor that returns early leaves its type initialized", "return Config.X + \"|\" + Config.Seen;"), // "5|cctor"
        ("a static constructor's local named like the holder of the statics", "return Pool.Size;"),                      // 4
        // A failure is kept: every use of the type throws the TypeInitializationException that
        // carries what the initializer threw, the first use included.
        ("an initializer that throws fails every use of its type",
            "string Read() { try { return Boom.B.ToString(); } catch (Exception e) { return e is TypeInitializationException { InnerException: InvalidOperationException { Message: \"x\" } } ? \"tie\" : \"other\"; } } return Read() + \"|\" + Read();"), // "tie|tie"
        ("a static constructor that throws fails every use of its type, a static method included",
            "string Read(Func<int> use) { try { return use().ToString(); } catch (Exception e) { return e is TypeInitializationException { InnerException.Message: \"cctor\" } ? \"tie\" : \"other\"; } } return Read(() => Fuse.Lit) + \"|\" + Read(() => Fuse.Light()) + \"|\" + Read(() => Fuse.Lit);"), // "tie|tie|tie"
        // A static event is a static: subscribing to it uses the type, which runs the static
        // constructor first, and its handler comes before the subscriber's.
        ("a subscription to a static event runs the static constructor first",
            "Log.Text = \"\"; Bus.Changed += () => Log.Text += \"mine \"; Bus.Raise(); return Log.Text;"),              // "default mine "
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassesStatics_InitializeAsCSharpRunsThem(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);

    /// <summary>
    /// A static whose initializer C# folds to a constant is that VALUE, in its own type, so a type of
    /// constants keeps its fields and reads nothing while it is defined. Written as the expression, it
    /// named the constants it folds, and an in-source constant read by its bare name is the twin's
    /// static, defined where it is declared: `Max = Default * 2` above `const int Default = 50` read it
    /// before it was defined, NaN for .NET's 100. Through `using static`, the initializer named another
    /// type no import brought, and the module did not load.
    /// </summary>
    private const string Constants = """
        using System;
        using eQuantic.UI.Primitives;
        using static Limits;

        public enum Shade { None, Dark, Light }

        public static class Limits { public static readonly int Max = Default * 2; public const int Default = 50; }

        public static class Names
        {
            public const string Full = Prefix + "x";
            public const string Prefix = "eq.";
            public static readonly string Tagged = Full + "!";
        }

        public static class Widths
        {
            public static readonly long Wide = Base * 3;
            public static readonly decimal Price = Base / 4m;
            public static readonly float Ratio = Base / 3f;
            public static readonly char Mark = First;
            public static readonly Shade Tone = Initial;
            public static readonly int? Maybe = Base;
            public static readonly object Boxed = Base;
            public static double Half { get; } = Base / 2.0;
            public const int Base = 10;
            public const char First = 'q';
            public const Shade Initial = Shade.Light;
        }

        public static class Derived { public static readonly int Next = Default + 1; }

        public class Grid { public static readonly int Cells = Side * Side; public const int Side = 3; public int Count() => Cells; }

        public sealed class Meter : StatelessComponent
        {
            public static readonly int Max = Default * 2;
            public static int Least { get; } = Default / 5;
            public const int Default = 50;
            public override VisualNode Build(ComponentContext context) => new Text("meter", TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] ConstantCases =
    [
        ("a static over a later constant", "return Limits.Max;"),                                                          // 100
        ("a constant over a later constant, and a static over it", "return Names.Full + \"|\" + Names.Tagged;"),            // "eq.x|eq.x!"
        ("a long, a decimal, a float and a char", "return Widths.Wide + \"|\" + Widths.Price + \"|\" + Widths.Ratio + \"|\" + Widths.Mark;"), // "30|2.5|3.3333333|q"
        ("an enum, a nullable, a boxed value and a property", "return Widths.Tone + \"|\" + Widths.Maybe + \"|\" + Widths.Boxed + \"|\" + Widths.Half;"), // "Light|10|10|5"
        ("a constant of another type, read through using static", "return Derived.Next;"),                                  // 51
        ("a plain class's static over a later constant", "return Grid.Cells + \"|\" + new Grid().Count();"),              // "9|9"
        ("a component's static field and property over a later constant", "return Meter.Max + \"|\" + Meter.Least;"),     // "100|10"
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStaticThatIsAConstant_IsItsValue(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Constants, typeAnnotations, ConstantCases);

    /// <summary>
    /// A static with no initializer whose zero CONSTRUCTS (an in-source struct's `new Alpha()`) is built
    /// on first use, its type initializing in order, and never while its module is evaluated: there, an
    /// import cycle (Alpha's method reads Zeta, so Alpha's module imports Zeta's) met Alpha's class
    /// before it was defined, and the module graph did not load.
    /// </summary>
    private const string ConstructedZero = """
        public struct Alpha { public int X; public int Read() => Zeta.Size; }
        public class Zeta { public static Alpha Origin; public static int Size = 3; }
        """;

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStaticWhoseZeroConstructs_IsBuiltOnFirstUse(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(ConstructedZero, typeAnnotations,
            ("a struct static's zero, its struct's module importing the class's", "return new Alpha().Read() + \"|\" + Zeta.Origin.X;")); // "3|0"
}
