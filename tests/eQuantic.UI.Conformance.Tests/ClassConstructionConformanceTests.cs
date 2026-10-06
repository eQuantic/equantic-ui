using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A plain class built as C# builds it, run through the module graph an app's build writes, both sides
/// executed. A class's twin kept its widest constructor, took an object initializer as a trailing config
/// object and called its base's constructor with no arguments, and its initializers ran in an order of
/// their own:
/// <list type="bullet">
/// <item>every constructor is reached by the count of arguments it takes, a chain through its root, a
/// base's with its own arguments and a primary constructor's parameters bound (#583);</item>
/// <item>the instance members start in declaration order, a field's and a property's alike, and a
/// derived class's before its base's constructor (#571);</item>
/// <item>an object initializer is applied once the constructor returns (#582);</item>
/// <item>and an exception's is applied once it is built (#587).</item>
/// </list>
/// </summary>
public class ClassConstructionConformanceTests
{
    private const string Constructors = """
        public class Money { public int Cents; public Money() : this(100) { } public Money(int c) { Cents = c; } }
        public class B0 { public int X; public B0(int x) { X = x; } }
        public class D0 : B0 { public D0(int x) : base(x * 2) { } }
        public class Greeter(string name) { public string Hello() => "hi " + name; }
        public class Sized(string label) : B0(label.Length) { public string Text => label; }
        public class Box<T> { public T Value; public Box(T v) { Value = v; } public Box() : this(default!) { } }
        public class Sum { public int Total; public Sum(params int[] xs) { foreach (var x in xs) Total += x; } }
        public class Chain { public string Log = ""; public Chain(int a) { Log += "root" + a + " "; } public Chain() : this(1) { Log += "alt"; } }
        public class Multi { public int A; public string B = "b"; public Multi(int a) { A = a; } public Multi(int a, string b) { A = a; B = b; } }
        public class DerivedMulti : Multi { public int C = 9; public DerivedMulti(int a) : base(a, "d") { } public DerivedMulti() : this(4) { C++; } }
        public class Named { public int A; public int B; public Named(int a = 1, int b = 2) { A = a; B = b; } }
        public class Shape0 { public string Kind; public int Sides; public Shape0(string kind, int sides = 0, string color = "black") { Kind = kind + ":" + color; Sides = sides; } }
        public class Square0 : Shape0 { public Square0() : base("square", color: "red") { } }
        """;

    private static readonly (string Name, string Statements)[] ConstructorCases =
    [
        ("a constructor that chains to another", "return new Money().Cents + \"|\" + new Money(5).Cents;"),
        ("a base's constructor with its arguments", "return new D0(3).X;"),
        ("a primary constructor", "return new Greeter(\"ada\").Hello();"),
        ("a primary constructor with a base clause", "var s = new Sized(\"abc\"); return s.X + \"|\" + s.Text;"),
        ("a generic class's constructors", "return new Box<int>(3).Value + \"|\" + new Box<string>().Value;"),
        ("a params constructor", "return new Sum(1, 2, 3).Total + \"|\" + new Sum().Total;"),
        ("a chained constructor's body after its root's", "return new Chain().Log + \"|\" + new Chain(7).Log;"),
        ("two roots told apart by their counts", "var m = new Multi(1); var m2 = new Multi(2, \"z\"); return m.A + m.B + \"|\" + m2.A + m2.B;"),
        ("a derived class's chain and its base's arguments", "var d = new DerivedMulti(); return d.A + d.B + d.C;"),
        ("named arguments over optional parameters", "var n1 = new Named(b: 5); var n2 = new Named(); return n1.A + \"|\" + n1.B + \"|\" + n2.A + n2.B;"),
        ("a base's argument by name", "var q = new Square0(); return q.Kind + \"|\" + q.Sides;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryConstructorOfAClass_IsReachedAsCSharpReachesIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Constructors, typeAnnotations, ConstructorCases);

    private const string Initializers = """
        public static class Log { public static string Text = ""; public static int Note(string s) { Text += s + " "; return 0; } }
        public class Base0 { int b = Log.Note("base-init"); public Base0() { Log.Note("base-ctor"); } }
        public class Derived : Base0 { int d = Log.Note("derived-init"); public int P { get; } = Log.Note("derived-prop"); }
        public class Plain { public static int N; public int A = ++N; public int B { get; set; } = ++N; }
        public class Ordered { public int X = Log.Note("x-init"); public int Y { get; set; } = Log.Note("y-init"); public Ordered() { Log.Note("ctor"); } }
        public class Stored { public int Level { get; set => field = System.Math.Max(0, value); } = 5; }
        public class Ticker { public event System.Action? Ticked; public int Count; public void Tick() { Ticked?.Invoke(); Count++; } }
        public abstract class Shape { public string Kind; protected Shape(string kind) { Kind = kind; Log.Note("shape " + kind); } public abstract double Area(); }
        public class Square : Shape { public double Side = 2; public Square() : base("square") { Log.Note("square " + Side); } public override double Area() => Side * Side; }
        public class Counted { public static int Made; static Counted() { Made = 100; } public int Id = ++Made; }
        """;

    private static readonly (string Name, string Statements)[] InitializerCases =
    [
        ("a derived class's initializers before its base's", "Log.Text = \"\"; new Derived(); return Log.Text;"),
        ("a field's and a property's initializers in declaration order", "Plain.N = 0; var p = new Plain(); return p.A + \"|\" + p.B;"),
        ("a store a property guards starts as its initializer", "var st = new Stored(); var a = st.Level; st.Level = -4; return a + \"|\" + st.Level;"),
        ("an instance event starts with no handler", "var t = new Ticker(); var n = 0; t.Ticked += () => n++; t.Tick(); t.Tick(); return n + \"|\" + t.Count;"),
        ("an abstract base's constructor before its derived class's body", "Log.Text = \"\"; var q = new Square(); return Log.Text + \"|\" + q.Area() + \"|\" + q.Kind;"),
        ("a static constructor before the first instance", "var c1 = new Counted(); var c2 = new Counted(); return c1.Id + \"|\" + c2.Id + \"|\" + Counted.Made;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassesMembers_StartInDeclarationOrder_BeforeItsBasesConstructor(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Initializers, typeAnnotations, InitializerCases);

    private const string Initialized = """
        using System.Collections;
        using System.Collections.Generic;
        public static class Log { public static string Text = ""; public static int Note(string s) { Text += s + " "; return 0; } }
        public class Plain { public static int N; public int A = ++N; public int B { get; set; } = ++N; }
        public class Ordered { public int X = Log.Note("x-init"); public int Y { get; set; } = Log.Note("y-init"); public Ordered() { Log.Note("ctor"); } }
        public class Inner { public int X; }
        public class Outer { public Inner In { get; } = new Inner { X = 1 }; public List<int> Ids { get; } = new() { 7 }; }
        public class Bag : IEnumerable<int>
        {
            private readonly List<int> items = new();
            public int Count => items.Count;
            public void Add(int x) => items.Add(x * 10);
            public IEnumerator<int> GetEnumerator() { foreach (var item in items) yield return item; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Multi { public int A; public string B = "b"; public Multi(int a) { A = a; } public Multi(int a, string b) { A = a; B = b; } }
        """;

    private static readonly (string Name, string Statements)[] InitializedCases =
    [
        ("an initializer reads what the constructor changed", "Plain.N = 0; var c = new Plain { B = Plain.N }; return c.A + \"|\" + c.B + \"|\" + Plain.N;"),
        ("an initializer's value is evaluated after the constructor", "Log.Text = \"\"; new Ordered { Y = Log.Note(\"value\") }; return Log.Text;"),
        ("nested initializers reach what the members hold", "var o = new Outer { In = { X = 5 }, Ids = { 8 } }; return o.In.X + \"|\" + string.Join(\",\", o.Ids);"),
        ("a collection initializer adds through the class's Add", "return new Bag { 1, 2 }.Count;"),
        ("a target-typed construction with an initializer", "Multi m = new(3) { B = \"init\" }; return m.A + m.B;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClass_IsBuiltThenInitialized(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Initialized, typeAnnotations, InitializedCases);

    private const string Exceptions = """
        public class Failure : System.Exception { }
        public class Retry : Failure { public int Attempts; }
        public class Coded : System.Exception { public int Code; public string? Hint { get; set; } }
        """;

    private static readonly (string Name, string Statements)[] ExceptionCases =
    [
        ("an exception's field set by its initializer", "var r = new Retry { Attempts = 3 }; return r.Attempts + \"|\" + (r is Failure);"),
        ("two members of an exception, each after it is built", "var c = new Coded { Code = 4, Hint = \"retry\" }; return c.Code + \"|\" + c.Hint;"),
        ("a thrown exception's initializer reaches its catch", "try { throw new Retry { Attempts = 2 }; } catch (Failure f) { return ((Retry)f).Attempts; }"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExceptionsInitializer_IsApplied(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Exceptions, typeAnnotations, ExceptionCases);
}
