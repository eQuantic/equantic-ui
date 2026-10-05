using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A struct's zero is what C# gives it: every member at its type's zero, and nothing run to build it
/// (C# 16.4.10). It is `default(S)`, an array's slot, an OrDefault, and `new S()` where S declares no
/// parameterless constructor. Every struct twin the compiler writes carries a `$zero()` that builds it
/// without the constructor: written only where the constructor did more than zero, the zero was a bare
/// `new S()` everywhere else, which ran an all-optional constructor (`P(int a = 1) : this(a, a)` made
/// `default(P)` a (1, 1)) and started the type's initialization, a generic struct's was the text
/// `undefined`, and a struct twin from another assembly zeroed through its constructor's defaults.
/// </summary>
public class StructZeroConformanceTests
{
    private const string Prelude = """
        public record Log { public static string Text = ""; public static int Note(string s) { Text += s; return 0; } }

        public struct Pair<T> { public T First; public int Count; }
        public record struct P(int X, int Y) { public P(int a = 1) : this(a, a) { } }
        public struct Meter { public int V; static Meter() { Log.Note("cctor "); } public Meter(int v) { V = v; } }
        public struct Meter0 { public int V; static Meter0() { Log.Note("cctor "); } public static int Read() => 1; }
        public struct Gauge2 { public int Level = 5; public int Count; public Gauge2(int level) { Level = level; } }
        public struct Wrap { public Pair<int> Inner; public P Point; }
        """;

    [SkippableTheory]
    // A generic struct has a zero of its own, through `new S()` and every default.
    [InlineData("var p = new Pair<string>(); p.Count++; return p.Count;")]                                  // 1
    [InlineData("var a = new Pair<int>[2]; a[0].Count = 3; return a[0].Count + \"|\" + a[1].Count;")]       // "3|0"
    [InlineData("var p = new Pair<string> { Count = 2 }; return p.Count + \"|\" + (p.First == null);")]     // "2|True"
    [InlineData("return default(Pair<string>).Count;")]                                                     // 0
    // An all-optional constructor is not the zero: `new P()`, `default(P)` and an array's slot are (0, 0).
    [InlineData("var d = default(P); var arr = new P[2]; return d.X + \"|\" + arr[1].Y + \"|\" + new P(3).X;")] // "0|0|3"
    [InlineData("var n = new P(); return n.X + \"|\" + n.Y;")]                                              // "0|0"
    // A zero starts no type initialization: only an explicit constructor or a static member does.
    [InlineData("Log.Text = \"\"; var m = new Meter(); var a = new Meter[2]; var d = default(Meter); return Log.Text + \"|\" + m.V + a[1].V + d.V;")] // "|000"
    [InlineData("Log.Text = \"\"; var m = new Meter(4); return Log.Text + \"|\" + m.V;")]                   // "cctor |4"
    [InlineData("Log.Text = \"\"; var m = new Meter0(); var a = new Meter0[2]; var d = default(Meter0); var before = Log.Text; Meter0.Read(); return before + \"|\" + Log.Text + \"|\" + m.V + a[1].V + d.V;")] // "|cctor |000"
    // An initializer runs only in the constructor that runs it, never in a zero.
    [InlineData("var g = new Gauge2(); var d = default(Gauge2); return g.Level + \"|\" + d.Level + \"|\" + new Gauge2(7).Level;")] // "0|0|7"
    // A struct member's zero is a zero of its own, a fresh one for every zero of its holder.
    [InlineData("var a = new Wrap[2]; a[0].Inner.Count = 5; a[0].Point = new P(2); return a[1].Inner.Count + \"|\" + a[1].Point.X + \"|\" + a[0].Point.Y;")] // "0|0|2"
    public void AStructsZero_RunsNothing(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>A struct twin from another assembly the compiler transpiled (the code engine's), seen as
    /// metadata, so nothing says which constructors it has: its zero is the `$zero()` the twin
    /// carries. `CodeCollapse`'s Placeholder defaults to true in its constructor, and false in C#'s zero.</summary>
    private const string Metadata = """
        using System.Collections.Generic;
        using System.Linq;
        using eQuantic.UI.Code;
        """;

    private static readonly (string Name, string Statements)[] MetadataCases =
    [
        ("default", "return default(CodeCollapse).Placeholder;"),                                          // false
        ("new with no argument", "return new CodeCollapse().Placeholder;"),                                // false
        ("an array's slot", "return (new CodeCollapse[2])[1].Placeholder;"),                                 // false
        ("an OrDefault", "return new List<CodeCollapse>().FirstOrDefault().Placeholder;"),                 // false
        ("the constructor's own default", "return new CodeCollapse(1, 2).Placeholder;"),                   // true
        ("a zero equals the zero", "return default(CodeCollapse).Equals(new CodeCollapse(0, 0, false));"), // true
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStructOfAnotherAssembly_ZeroesAsItsTwinDoes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Metadata, typeAnnotations, MetadataCases);
}
