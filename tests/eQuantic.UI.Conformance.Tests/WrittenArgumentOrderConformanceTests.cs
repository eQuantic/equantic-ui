using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A call read from its syntax evaluates its arguments in the order they are written, and an <c>out</c>
/// argument shifts none of the ones after it (#1071), both sides executed. Named arguments were placed in
/// parameter order and evaluated so, so <c>M(b: Log("b"), a: Log("a"))</c> logged <c>a</c> first in the
/// browser, and the argument after an <c>out</c> one took the value of the next. The twins eqc writes
/// already kept the written order (<c>BoundArguments.Of</c>); an app's method call and a vocabulary
/// type's constructor did not.
/// </summary>
public class WrittenArgumentOrderConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;

        public static class Seq
        {
            public static string Seen = "";
            public static int Int(int value) { Seen += value; return value; }
            public static string Str(string value) { Seen += value; return value; }
            public static float F(float value) { Seen += value; return value; }
        }

        public static class Calls
        {
            public static string Pair(int a, int b) => a + "," + b;
            public static string Three(int a, int b = 20, int c = 30) => a + "," + b + "," + c;
            public static bool Try(out int found, int a = 1, int b = 2) { found = a * 10 + b; return true; }
            public static string Listed(int head, params int[] rest) => head + ":" + string.Join(",", rest);
            public static string RefPair(int a, ref int b) { var read = a + "," + b; b = 100; return read; }
            public static string InPair(int a, in int b) => a + "," + b;
        }

        // Initializers, where no statement is open to declare a temporary (#1201).
        public sealed class Holder
        {
            public string Value = Calls.Pair(b: Seq.Int(2), a: Seq.Int(1));
            public string Prop { get; } = Calls.Pair(b: Seq.Int(4), a: Seq.Int(3));
        }

        public sealed record Rec(int X)
        {
            public string Value { get; } = Calls.Pair(b: Seq.Int(6), a: Seq.Int(5));
        }

        public class Pairing
        {
            public string Value;
            public Pairing(int a, int b) { Value = a + "," + b; }
        }

        public sealed class Chained : Pairing
        {
            public Chained() : base(b: Seq.Int(2), a: Seq.Int(1)) { }
        }

        // A params parameter that is the array it is on the twin, its elements packed after the arguments are
        // evaluated as written (#1192 beside #1071): a method's and a class's constructor.
        public sealed class Packed
        {
            public string Value;
            public Packed(string tag, params int[] items) { Value = tag + ":" + string.Join(",", items) + ":" + items.Length; }
        }

        public static class Shared
        {
            // A static constructor, so .NET runs the initializer at the first access and not before it.
            static Shared() { }
            public static string Value = Calls.Pair(b: Seq.Int(8), a: Seq.Int(7));
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("named arguments out of order, each with an effect",
            "Seq.Seen = \"\"; var r = Calls.Pair(b: Seq.Int(2), a: Seq.Int(1)); return r + \"|\" + Seq.Seen;"),
        ("a skipped optional between two named arguments out of order",
            "Seq.Seen = \"\"; var r = Calls.Three(c: Seq.Int(3), a: Seq.Int(1)); return r + \"|\" + Seq.Seen;"),
        ("named arguments in order still pass as they stand",
            "Seq.Seen = \"\"; var r = Calls.Pair(a: Seq.Int(1), b: Seq.Int(2)); return r + \"|\" + Seq.Seen;"),
        ("an out argument, then a named one",
            "Calls.Try(out var found, b: 7); return found;"),
        ("an out argument, then two named ones out of order",
            "Seq.Seen = \"\"; Calls.Try(out var found, b: Seq.Int(7), a: Seq.Int(4)); return found + \"|\" + Seq.Seen;"),
        ("a name an argument written after it changes",
            "var i = 1; return Calls.Pair(b: i, a: ++i);"),
        ("a name an argument written after it changes, past an out argument",
            "var i = 1; Calls.Try(out var found, b: i, a: ++i); return found;"),
        // A reference is read by the callee, after every argument ran: never held where it is written.
        ("a ref argument an argument written after it changes",
            "var i = 1; var r = Calls.RefPair(b: ref i, a: ++i); return r + \"|\" + i;"),
        ("an in argument an argument written after it changes",
            "var i = 1; return Calls.InPair(b: in i, a: ++i);"),
        ("a ref element taken before the call, which an argument written after it changes",
            "var arr = new[] { 1, 2 }; var r = Calls.RefPair(b: ref arr[^1], a: arr[1] = 5); return r + \"|\" + string.Join(\",\", arr);"),
        ("a vocabulary record with a twin of its own, built with named arguments out of order",
            "Seq.Seen = \"\"; var style = new TypeStyle(LineHeight: Seq.F(24), Size: Seq.F(16), Weight: FontWeight.Bold, Tracking: 0, MaxScale: Seq.F(2)); "
            + "return style.Size + \",\" + style.LineHeight + \",\" + style.MaxScale + \"|\" + Seq.Seen;"),
        ("a field's and a property's initializers, named arguments out of order",
            "Seq.Seen = \"\"; var h = new Holder(); return h.Value + \"|\" + h.Prop + \"|\" + Seq.Seen;"),
        ("a record's property initializer, named arguments out of order",
            "Seq.Seen = \"\"; var r = new Rec(1); return r.Value + \"|\" + Seq.Seen;"),
        ("a constructor's base call, named arguments out of order",
            "Seq.Seen = \"\"; var c = new Chained(); return c.Value + \"|\" + Seq.Seen;"),
        ("a static's initializer, named arguments out of order",
            "Seq.Seen = \"\"; var v = Shared.Value; return v + \"|\" + Seq.Seen;"),
        ("a params element named before the parameter it follows",
            "Seq.Seen = \"\"; var r = Calls.Listed(rest: Seq.Int(2), head: Seq.Int(1)); return r + \"|\" + Seq.Seen;"),
        ("a constructor's params element named before the parameter it follows",
            "Seq.Seen = \"\"; var p = new Packed(items: Seq.Int(2), tag: Seq.Str(\"t\")); return p.Value + \"|\" + Seq.Seen;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACallReadFromItsSyntax_EvaluatesItsArgumentsAsWritten(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
