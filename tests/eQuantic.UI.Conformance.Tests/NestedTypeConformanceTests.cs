using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type declared inside another one is a module of its own, named by its owner (<c>Cart$Item</c>,
/// #584), run through the module graph an app's build writes, both sides executed. A nested class had
/// no module, so constructing it named nothing, and where a top-level class had its name it resolved
/// to that one; a nested record wrote the module a top-level record of its name writes, and the build
/// refused the two (EQ2009); a component's nested static class was its module's alone.
/// </summary>
public class NestedTypeConformanceTests
{
    private const string Classes = """
        public class Item { public int Qty = 3; }
        public class Cart { private class Item { public int Qty = 9; } public int Size() => new Item().Qty; }
        public class Roster { private sealed class Row { public string Name = ""; } public static string First() { var r = new Row { Name = "Ada" }; return r.Name; } }
        public class Calc { static class Ops { public static int Twice(int x) => x * 2; } public int Run(int x) => Ops.Twice(x); }
        public class Outer { public class Inner { public int N = 4; } }
        public class A { public class B { public class C { public int V = 1; } } }
        public class Counter { private static int made; public class Ticket { public int Id = ++made; } public static int Made => made; }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("a nested class beside a top-level class of its name", "return new Cart().Size();"),
        ("a private nested class built by its owner", "return Roster.First();"),
        ("a nested static class of a plain class", "return new Calc().Run(4);"),
        ("a public nested type from outside, and its type test", "object o = new Outer.Inner(); return ((Outer.Inner)o).N + \"|\" + (o is Outer.Inner);"),
        ("two levels of nesting", "return new A.B.C().V;"),
        ("a nested class reads its owner's private static", "var t1 = new Counter.Ticket(); var t2 = new Counter.Ticket(); return t1.Id + \"|\" + t2.Id + \"|\" + Counter.Made;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANestedClass_IsAModuleNamedByItsOwner(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);

    private const string Records = """
        public class Shop { public record Line(int Qty); }
        public record Line(string Text);
        """;

    private static readonly (string Name, string Statements)[] RecordCases =
    [
        ("a nested record beside a top-level record of its name, each printing its C# name",
            "return new Shop.Line(2).Qty + \"|\" + new Line(\"x\").Text + \"|\" + new Shop.Line(2) + \"|\" + new Line(\"x\");"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANestedRecord_NeverMeetsATopLevelOneOfItsName(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Records, typeAnnotations, RecordCases);

    private const string Component = """
        using eQuantic.UI.Primitives;
        public sealed class Panel : StatelessComponent
        {
            private static class Copy { public static string Greet(string n) => "hi " + n; }
            private sealed class Row { public string Name = ""; }
            public static string Hello() => Copy.Greet("x");
            public static string First() { var r = new Row { Name = "Ada" }; return r.Name; }
            public override VisualNode Build(ComponentContext context) => new Text(First(), TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] ComponentCases =
    [
        ("a component's nested static class", "return Panel.Hello();"),
        ("a component's nested class", "return Panel.First();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AComponentsNestedTypes_AreModulesNamedByIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Component, typeAnnotations, ComponentCases);

    private const string NestedComponents = """
        using eQuantic.UI.Primitives;
        public sealed class Item : StatelessComponent
        {
            public static string Kind() => "component";
            public override VisualNode Build(ComponentContext context) => new Text("item", TypeRole.BodyM);
        }
        public class Cart { public class Item { public int Qty = 2; } public static int Size() => new Item().Qty; }
        public static class Host
        {
            public sealed class Page : StatelessComponent
            {
                public int Hits = 7;
                public override VisualNode Build(ComponentContext context) => new Text("page", TypeRole.BodyM);
            }
            public static int Made() { Page p = new(); return p.Hits; }
        }
        """;

    private static readonly (string Name, string Statements)[] NestedComponentCases =
    [
        ("a component beside a nested plain class of its name", "return Item.Kind() + \"|\" + Cart.Size();"),
        ("a nested component, built and tested by its twin",
            "object o = new Host.Page(); return (o is Host.Page) + \"|\" + Host.Made() + \"|\" + ((Host.Page)o).Hits;"),
    ];

    /// <summary>A component is known by its twin's name, so a nested plain class of a component's simple
    /// name is never taken for it, and a nested component is built, tested and imported as
    /// <c>Host$Page</c> (found by Copilot's review of #654).</summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANestedComponent_IsItsTwin(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(NestedComponents, typeAnnotations, NestedComponentCases);

    private const string Inferred = """
        using Alias = Outer.Pair;
        public class Outer
        {
            public record Amount(int V)
            {
                public static Amount operator +(Amount a, Amount b) => new(a.V + b.V);
                public static Amount operator -(Amount a) => new(-a.V);
                public static implicit operator int(Amount a) => a.V;
            }
            public struct Pair { public int A; public int B; }
        }
        public static class Factory { public static Outer.Amount Make() => new(2); public static Alias Zero() => default; }
        """;

    private static readonly (string Name, string Statements)[] InferredCases =
    [
        ("an operator of a nested type no expression names", "var s = Factory.Make() + Factory.Make(); return s.V;"),
        ("a unary operator, then a conversion, of it", "int n = -Factory.Make(); return n;"),
        ("a compound assignment through it", "var t = Factory.Make(); t += Factory.Make(); return t.V;"),
        ("the zero of a nested struct through an alias", "Alias p = Factory.Zero(); Alias q = default; return p.A + q.B;"),
    ];

    /// <summary>A twin the syntax never names, reached by an operator on what a call returned or by the
    /// zero of a type an alias names, is imported as a named one is (found by Copilot's review of
    /// #654): the module called <c>Outer$Amount.opAdd</c> with no import.</summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANestedTwinReachedByInference_IsImported(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Inferred, typeAnnotations, InferredCases);
}
