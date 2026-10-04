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
    public void ARecordsStatics_InitializeAsCSharpRunsThem(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Records);
    }

    private const string Classes = """
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

        public sealed class Panel : StatelessComponent
        {
            public static int A = B + 1;
            public static int B = 2;
            public static int C;
            static Panel() { C = A * 10; }
            public override VisualNode Build(ComponentContext context) => new Text("panel", TypeRole.BodyM);
        }
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
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassesStatics_InitializeAsCSharpRunsThem(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);
}
