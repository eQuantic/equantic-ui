using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type named by a word JavaScript reserves, or by a global the emitted code reads, is a module of
/// its own under its name and a <c>$</c> (<c>function$</c>, <c>Math$</c>), run through the module graph
/// an app's build writes, both sides executed, at every kind of reference. <c>class function</c> was
/// written as <c>export class function</c>, which no module parses (#934), and a class named
/// <c>Math</c> was imported under that name, so <c>System.Math.Max</c> beside it called the class (#890).
/// </summary>
public class ReservedTypeNameConformanceTests
{
    private const string Reserved = """
        public class function { public int V = 1; public static int Made => 2; }
        public class @class : function { public int W = 3; }
        public class Holder { public function Held = new function(); public function[] Many = [new @class()]; }
        """;

    private static readonly (string Name, string Statements)[] ReservedCases =
    [
        ("built, and a member of it read", "return new function().V;"),
        ("a static member of it", "return function.Made;"),
        ("a class written with the verbatim escape, extending one", "var c = new @class(); return c.V + c.W;"),
        ("its type test and its as", "object o = new @class(); return (o is function) + \"|\" + ((o as @class)?.W ?? 0) + \"|\" + (o is Holder);"),
        ("a member and an array of it", "var h = new Holder(); return h.Held.V + h.Many.Length;"),
        ("its name, as C# prints it", "return new function().ToString() + \"|\" + new @class();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATypeNamedByAReservedWord_IsItsTwin(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Reserved, typeAnnotations, ReservedCases);

    private const string Globals = """
        public static class Math { public static int Twice(int x) => x * 2; }
        public struct Symbol { public int X; }
        public class Number { public int Value = 5; }
        public class Bag : System.Collections.Generic.IEnumerable<int>
        {
            public Symbol Tag;
            public System.Collections.Generic.IEnumerator<int> GetEnumerator() { yield return Tag.X; yield return 2; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Calc
        {
            public static int Run() => Math.Twice(2) + System.Math.Max(1, 2);
            public static string Text(double d) => new Number().Value + "|" + double.IsFinite(d) + "|" + double.IsNaN(d);
        }
        """;

    private static readonly (string Name, string Statements)[] GlobalCases =
    [
        ("a class named Math beside .NET's", "return Math.Twice(2) + System.Math.Max(1, 2);"),
        ("the same, from another module", "return Calc.Run();"),
        ("a struct named Symbol in a sequence's module", "var b = new Bag(); b.Tag.X = 1; var sum = 0; foreach (var i in b) sum += i; return sum;"),
        ("a class named Number beside a test of a double", "return Calc.Text(1.25);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATypeNamedLikeAGlobalTheOutputReads_LeavesTheGlobalAlone(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Globals, typeAnnotations, GlobalCases);

    private const string Component = """
        using System.Linq;
        using eQuantic.UI.Primitives;
        public sealed class Set : StatelessComponent
        {
            public int Pins = 2;
            public static int Distinct(int[] items) => items.Distinct().Count();
            public override VisualNode Build(ComponentContext context) => new Text("set", TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] ComponentCases =
    [
        ("a component named Set, built and tested by its twin, beside a Distinct",
            "object o = new Set(); return (o is Set) + \"|\" + ((Set)o).Pins + \"|\" + Set.Distinct([1, 1, 2]);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AComponentNamedLikeAGlobal_IsItsTwin(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Component, typeAnnotations, ComponentCases);
}
