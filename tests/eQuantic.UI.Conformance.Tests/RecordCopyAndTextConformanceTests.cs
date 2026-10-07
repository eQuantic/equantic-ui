using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A record's or a struct's copy and text, as .NET's. Each case brings its own prelude, so a type that
/// does not load fails its own case.
/// <list type="bullet">
/// <item>The text prints what PrintMembers prints: every public instance field and every public
/// instance property with a getter, whatever the getter's own accessibility, and never an override of
/// a property its base prints. The twin dropped `{ private get; set; }` and printed an override twice
/// (`Derived { V = 2, V = 2, W = 3 }`).</item>
/// <item>A record that declares its own copy constructor is built as any other: it took that
/// constructor for a second one of one argument and refused the type (EQ1009).</item>
/// <item>An assignment to `this` in a struct copies the value's state onto the instance: written as
/// itself, the module did not parse.</item>
/// <item>A `with` on a struct, or on a record the compiler writes in a namespace of the vocabulary, is
/// the twin's own copy: a plain struct's was spread into an object with no methods, and the code
/// engine's records were rebuilt through a constructor that takes no config, so the copy and the patch
/// were both lost.</item>
/// </list>
/// </summary>
public class RecordCopyAndTextConformanceTests
{
    [SkippableTheory]
    [InlineData("public record R { public int Hidden { private get; set; } = 3; public int Shown { get; set; } = 4; public int ProtGet { protected get; set; } = 5; public int InternalGet { internal get; set; } = 6; public int PrivSet { get; private set; } = 7; public int WriteOnly { set { } } }",
        "return new R().ToString();")]                                                    // "R { Hidden = 3, Shown = 4, ProtGet = 5, InternalGet = 6, PrivSet = 7 }"
    [InlineData("public record Base { public virtual int V { get; init; } = 1; } public record Derived : Base { public override int V { get; init; } = 2; public int W { get; init; } = 3; }",
        "return new Derived().ToString();")]                                              // "Derived { V = 2, W = 3 }"
    [InlineData("public abstract record Named { public abstract string Name { get; init; } } public record Person(string Name, int Age) : Named;",
        "return new Person(\"ada\", 36).ToString();")]                                     // "Person { Name = ada, Age = 36 }"
    [InlineData("public record Shape4 { public virtual string Name => \"shape\"; } public record Circle4(double R) : Shape4 { public override string Name => \"circle\"; }",
        "return new Circle4(2).ToString();")]                                             // "Circle4 { Name = circle, R = 2 }"
    [InlineData("public record Doc { public int Size; public Doc(int capacity) { Size = capacity; } protected Doc(Doc original) { Size = original.Size + 1; } }",
        "var d = new Doc(2); return d.Size + \"|\" + d;")]                                // "2|Doc { Size = 2 }"
    [InlineData("public struct P5 { public int X, Y; public P5(int x) { this = default; X = x; } public void Reset() { this = new P5(9); } }",
        "var p = new P5(3); var q = new P5(1); q.Reset(); return p.X + \"|\" + p.Y + \"|\" + q.X + \"|\" + q.Y;")] // "3|0|9|0"
    [InlineData("public struct Pt { public int X; public int Y; public int Sum() => X + Y; }",
        "var a = new Pt { X = 1, Y = 2 }; var b = a with { Y = 5 }; return b.Sum() + \"|\" + a.Y + \"|\" + b.X;")] // "6|2|1"
    public void ARecordsCopyAndText_AreDotNets(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }

    /// <summary>
    /// `with` copies through a record's own copy constructor, as C#'s does (#589): a declared one runs no
    /// initializer and copies nothing it does not assign, chains to its base's with `: base(original)`,
    /// and a deep copy it writes is the copy's own. A derived record whose copy constructor is
    /// synthesized runs its base's declared one, then copies its own members. The twin's `with` copied
    /// every member onto the prototype and never ran a declared one.
    /// </summary>
    [SkippableTheory]
    [InlineData("public record Base1 { public int B = 7; public int Copied; public Base1() { } protected Base1(Base1 o) { Copied = o.B + 100; } } public record Derived1 : Base1 { public int D = 3; public Derived1() { } protected Derived1(Derived1 o) : base(o) { D = o.D * 10; } }",
        "var x = new Derived1(); x.B = 1; var y = x with { }; return y.B + \"|\" + y.Copied + \"|\" + y.D;")]   // "0|101|30"
    [InlineData("public record Lines { public System.Collections.Generic.List<int> Items = new(); public Lines() { } protected Lines(Lines o) { Items = new(); foreach (var item in o.Items) Items.Add(item); } }",
        "var a = new Lines(); a.Items.Add(1); var b = a with { }; b.Items.Add(2); return a.Items.Count + \"|\" + b.Items.Count;")] // "1|2"
    [InlineData("public record Base2 { public int B = 7; public int Copied; public Base2() { } protected Base2(Base2 o) { Copied = o.B + 100; } } public record Derived2 : Base2 { public int D = 3; }",
        "var x = new Derived2(); x.D = 5; var y = x with { }; return y.B + \"|\" + y.Copied + \"|\" + y.D;")]   // "0|107|5"
    [InlineData("public record Counter { public int N; public int Copies; public Counter() { } protected Counter(Counter o) { N = o.N; Copies = o.Copies + 1; } }",
        "var c = new Counter { N = 4 }; var d = c with { N = 9 }; var e = d with { }; return d.N + \"|\" + d.Copies + \"|\" + e.N + \"|\" + e.Copies + \"|\" + c.Copies;")] // "9|1|9|2|0"
    [InlineData("public record P6(int X) { public int Extra = 5; protected P6(P6 o) { X = o.X * 2; } }",
        "var p = new P6(3) with { }; return p.X + \"|\" + p.Extra;")]                // "6|0"
    public void AWith_CopiesThroughTheRecordsOwnCopyConstructor(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }

    /// <summary>A record of the code engine, which the compiler transpiles whole although its namespace
    /// is the vocabulary's, reached as metadata: its `with` is its twin's own copy.</summary>
    private const string Engine = """
        using eQuantic.UI.Code;
        """;

    private static readonly (string Name, string Statements)[] EngineCases =
    [
        ("the patch", "var r = CodeLanguageRules.Default with { LineComment = \"--\", IndentWidth = 2 }; return r.LineComment + \"|\" + r.IndentWidth + \"|\" + (r.InsertSpaces ? \"yes\" : \"no\");"), // "--|2|yes"
        ("the copy", "var b = new CodeLanguageRules { LineComment = \"#\", IndentWidth = 8 }; var c = b with { InsertSpaces = false }; return c.LineComment + \"|\" + c.IndentWidth + \"|\" + (c.InsertSpaces ? \"yes\" : \"no\");"), // "#|8|no"
        ("the original", "var r = CodeLanguageRules.Default with { IndentWidth = 2 }; return CodeLanguageRules.Default.IndentWidth;"), // 4
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWithOnTheCodeEnginesRecord_IsItsTwinsCopy(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Engine, typeAnnotations, EngineCases);
}
