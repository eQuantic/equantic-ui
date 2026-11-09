using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>a.GetType() == b.GetType()</c>, <c>!=</c>, and either against <c>typeof(X)</c>, answer as .NET answers
/// wherever the browser tells the types apart, and are refused by the build where it cannot (#1202). Every
/// one was refused (EQ2004): <c>GetType</c> was fenced for every value. The classes are an app's, so the
/// cases run as an app's build runs them (<see cref="ModuleGraph"/>).
/// </summary>
public class RuntimeTypeConformanceTests
{
    private const string Types = """
        using System;
        public class Node { }
        public class A : Node { }
        public class B : Node { }
        public class C : A { }
        public record R(int X);
        public record struct S(int X);
        public class BoxBase { }
        public class Box<T> : BoxBase { }
        public record Pair<T>(T Value);
        public static class Make { public static BoxBase Boxed<T>() => new Box<T>(); }
        public class Failed : Exception { }
        public class Self { public bool SameAs(Self other) => GetType() == other.GetType(); }
        public class Derived : Self { }
        public static class Kinds { public static bool Same<T>(T a, T b) where T : Node => a.GetType() == b.GetType(); }
        public static class Order
        {
            public static int Made;
            public static Node Next() { Made++; return new A(); }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("one class and two, through their base",
            "Node x = new A(), y = new A(), z = new B(); return (x.GetType() == y.GetType()) + \"|\" + (x.GetType() == z.GetType()) + \"|\" + (x.GetType() != z.GetType()) + \"|\" + (x.GetType() != y.GetType());"),
        ("a derived class against its base and itself, typeof on either side",
            "A a = new C(); return (a.GetType() == typeof(A)) + \"|\" + (a.GetType() == typeof(C)) + \"|\" + (typeof(C) == a.GetType()) + \"|\" + (a.GetType() != typeof(B));"),
        ("a record", "R r = new R(1), q = new R(2); return (r.GetType() == q.GetType()) + \"|\" + (r.GetType() == typeof(R));"),
        ("a struct, and one through its Nullable, which boxes to it",
            "S s = new S(1); S? n = s; return (s.GetType() == typeof(S)) + \"|\" + (n.GetType() == typeof(S));"),
        ("two closed types of one generic class",
            "BoxBase x = new Box<int>(), y = new Box<string>(), z = new Box<int>(); return (x.GetType() == y.GetType()) + \"|\" + (x.GetType() == z.GetType()) + \"|\" + (x.GetType() == typeof(Box<int>)) + \"|\" + (y.GetType() == typeof(Box<int>));"),
        ("two closed types of one generic record",
            "Pair<int> q = new Pair<int>(2); Pair<string> s = new Pair<string>(\"a\"); return (q.GetType() == typeof(Pair<int>)) + \"|\" + (s.GetType() == typeof(Pair<int>));"),
        ("one built in generic code is the closed type it is",
            "BoxBase x = Make.Boxed<int>(); return (x.GetType() == typeof(Box<int>)).ToString();"),
        ("an exception of the runtime's",
            "Exception e = new ArgumentException(); return (e.GetType() == typeof(ArgumentException)) + \"|\" + (e.GetType() == typeof(Exception)) + \"|\" + (e.GetType() == new ArgumentException(\"x\").GetType());"),
        ("an exception of the app's", "Exception e = new Failed(); return (e.GetType() == typeof(Failed)) + \"|\" + (e.GetType() == typeof(Exception));"),
        ("an exception the runtime throws",
            "try { return int.Parse(\"x\").ToString(); } catch (Exception e) { return (e.GetType() == typeof(FormatException)) + \"|\" + (e.GetType() == typeof(ArgumentException)); }"),
        ("the instance's own GetType", "return new Self().SameAs(new Derived()) + \"|\" + new Derived().SameAs(new Derived());"),
        ("a type parameter through its class constraint", "return Kinds.Same<Node>(new A(), new B()) + \"|\" + Kinds.Same<Node>(new B(), new B());"),
        ("a null receiver throws before the other side is evaluated",
            "Node x = null; Order.Made = 0; try { return (x.GetType() == Order.Next().GetType()).ToString(); } catch (NullReferenceException) { return \"through null|\" + Order.Made; }"),
        ("a null receiver on the right throws after the left",
            "Node x = null; Order.Made = 0; try { return (Order.Next().GetType() == x.GetType()).ToString(); } catch (NullReferenceException) { return \"through null|\" + Order.Made; }"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARuntimeType_ComparesAsDotNetDoes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Types, typeAnnotations, Cases);

    /// <summary>A receiver whose runtime type the browser cannot tell apart, a <c>typeof</c> of such a type,
    /// and any other use of <c>GetType()</c>, are refused by the build.</summary>
    [SkippableTheory]
    [InlineData("object o = 1; return (o.GetType() == typeof(int)).ToString();")]
    [InlineData("int[] a = { 1 }; int[] b = { 2 }; return (a.GetType() == b.GetType()).ToString();")]
    [InlineData("IComparable c = \"a\"; return (c.GetType() == c.GetType()).ToString();")]
    [InlineData("string s = \"a\"; return (s.GetType() == typeof(string)).ToString();")]
    [InlineData("Node x = new A(); return (x.GetType() == typeof(int)).ToString();")]
    [InlineData("return new A().GetType().Name;")]
    [InlineData("bool Same<T>(T a, T b) => a.GetType() == b.GetType(); return Same(1, 2).ToString();")]
    public void ARuntimeTypeTheBrowserCannotTell_IsRefused(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsRefusedByTheBuild(statements, "EQ2004", Types);
    }
}
