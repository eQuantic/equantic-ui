using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A class of the app's dispatches in the browser as it does in .NET, run through the module graph an
/// app's build writes, both sides executed:
/// <list type="bullet">
/// <item>a method that hides an inherited member is reached as the hiding one only through a type that
/// declares it, and a call bound to the hidden member reaches the hidden one, the runtime's own included
/// (#563).</item>
/// </list>
/// </summary>
public class ClassDispatchConformanceTests
{
    private const string Hiding = """
        using System;
        using System.Collections.Generic;
        public class A { public virtual string Name() => "A"; public string Call() => Name(); }
        public class B : A { public new string Name() => "B"; public string Both() => Name() + base.Name(); }
        public class D : B { public new virtual string Name() => "D"; }
        public class E : D { public override string Name() => "E" + base.Name(); }
        public class Up : B { public string Base() => base.Name(); }
        #pragma warning disable CS0108
        public class Quiet : A { public string Name() => "Q"; }
        #pragma warning restore CS0108
        public class Maker { public static string Make() => "S"; }
        public class Remaker : Maker { public new static string Make() => "M"; public static string Both() => Make() + Maker.Make(); }
        public class Sized { public int Size = 1; }
        public class Measured : Sized { public new int Size() => 2; }
        public class Box<T> { public virtual string Get(T x) => "box"; }
        public class IntBox : Box<int> { public new string Get(int x) => "int"; }
        public class Counter { public int Calls; public new int GetHashCode() => ++Calls; }
        public class Labelled { public new string ToString() => "hidden"; }
        """;

    private static readonly (string Name, string Statements)[] HidingCases =
    [
        ("a call through the base reaches the base's", "A a = new B(); return a.Name();"),
        ("a call through the hiding type reaches the hiding one", "B b = new B(); return b.Name();"),
        ("the base's own call reaches the base's", "return new B().Call();"),
        ("the hiding class reaches its own and its base's", "return new B().Both();"),
        ("a derived class's base call reaches the hiding one", "var u = new Up(); return u.Base() + u.Name() + ((A)u).Name();"),
        ("an override of a virtual that hides one fills its slot",
            "var e = new E(); A a = e; B b = e; D d = e; return a.Name() + \"|\" + b.Name() + \"|\" + d.Name();"),
        ("a method group of each", "A a = new B(); Func<string> f = a.Name; Func<string> g = new B().Name; return f() + g();"),
        ("a null-conditional call of each", "B? b = new B(); A? a = b; return a?.Name() + b?.Name();"),
        ("a method that hides one without saying new", "A a = new Quiet(); return a.Name() + new Quiet().Name();"),
        ("a static that hides one", "return Maker.Make() + Remaker.Make() + Remaker.Both();"),
        ("a method that hides a field", "var m = new Measured(); return m.Size() + \"|\" + ((Sized)m).Size;"),
        ("a method that hides one of a generic base", "Box<int> b = new IntBox(); return b.Get(1) + new IntBox().Get(1);"),
        ("an object's hash is object's, not the method that hides it",
            "var c = new Counter(); object o = c; var h1 = o.GetHashCode(); var h2 = o.GetHashCode(); return (h1 == h2) + \"|\" + c.Calls;"),
        ("a combined hash asks object's, not the method that hides it", "var c = new Counter(); HashCode.Combine(c, 1); return c.Calls;"),
        ("the method that hides GetHashCode, called through its type", "var c = new Counter(); c.GetHashCode(); return c.GetHashCode();"),
        ("the method that hides ToString, called through its type", "return new Labelled().ToString();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AMethodThatHidesOne_IsReachedThroughItsBase_AsDotNetReachesIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Hiding, typeAnnotations, HidingCases);
}
