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
        ("a vocabulary record with a twin of its own, built with named arguments out of order",
            "Seq.Seen = \"\"; var style = new TypeStyle(LineHeight: Seq.F(24), Size: Seq.F(16), Weight: FontWeight.Bold, Tracking: 0, MaxScale: Seq.F(2)); "
            + "return style.Size + \",\" + style.LineHeight + \",\" + style.MaxScale + \"|\" + Seq.Seen;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACallReadFromItsSyntax_EvaluatesItsArgumentsAsWritten(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
