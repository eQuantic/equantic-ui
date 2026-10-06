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
}
