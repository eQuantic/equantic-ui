using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An instance indexer reaches its twin (#427): its getter as <c>item(…)</c> and its setter as
/// <c>setItem(…, value)</c>, and every element access bound to it calls them, a read, a write, a
/// compound, a step, a coalescing write, a null-conditional one and an object initializer's entry.
/// A record whose only member was an indexer had no twin, and an indexer of any twin was written into
/// none, so `new Grid()[3]` read a property named "3": undefined, with a green build.
/// </summary>
public class IndexerConformanceTests
{
    private const string Prelude = """
        public record Grid { public int this[int i] => i * 2; }
        public interface IIndexed { int this[int i] => i * 3; }
        public record Tripler : IIndexed;
        public record Board { public int this[int r, int c] => r * 10 + c; }
        public record Cells
        {
            private readonly Dictionary<int, int> _m = new();
            public int Sets;
            public int this[int k]
            {
                get => _m.TryGetValue(k, out var v) ? v : -1;
                set { Sets++; if (value < 0) return; _m[k] = value; }
            }
            public int Count => _m.Count;
        }
        public record Names
        {
            private readonly Dictionary<int, string> _m = new();
            public int Sets;
            public string this[int k] { get => _m.TryGetValue(k, out var v) ? v : null; set { _m[k] = value; Sets++; } }
        }
        """;

    [SkippableTheory]
    [InlineData("return new Grid()[3];")]                                                           // 6
    [InlineData("return ((IIndexed)new Tripler())[3];")]                                            // 9
    [InlineData("return new Board()[2, 3];")]                                                       // 23
    [InlineData("var c = new Cells(); c[2] = 9; return c[2] + \"|\" + c.Count + \"|\" + c[5];")]    // "9|1|-1"
    [InlineData("var c = new Cells(); c[2] = 1; c[2] += 5; c[2]++; ++c[2]; return c[2] + \"|\" + c.Sets;")] // "8|4"
    [InlineData("var c = new Cells(); var x = (c[1] = 4) + 1; return x + \"|\" + c[1];")]          // "5|4"
    [InlineData("var c = new Cells(); var y = (c[1] = -1); return y + \"|\" + c[1] + \"|\" + c.Sets;")] // "-1|-1|1"
    [InlineData("var c = new Cells(); var old = c[3]++; return old + \"|\" + c[3];")]               // "-1|0"
    [InlineData("var n = new Names(); n[1] ??= \"a\"; n[1] ??= \"b\"; return n[1] + n.Sets;")]      // "a1"
    [InlineData("Cells maybe = null; return maybe?[1] == null;")]                                    // true
    [InlineData("var c = new Cells(); Cells same = c; same?[4] = 7; return c[4];")]                  // 7
    [InlineData("return new Cells { [1] = 4, [2] = 5 }.Count;")]                                    // 2
    public void AnInstanceIndexer_ReachesItsTwin(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    private const string Classes = """
        using System.Collections.Generic;

        public interface IShelf { string this[int i] => "shelf " + i; }
        public class Shelf : IShelf { }
        public class Store
        {
            private readonly Dictionary<string, int> _m = new();
            public int this[string k] { get => _m.TryGetValue(k, out var v) ? v : 0; set => _m[k] = value; }
            public int Count => _m.Count;
        }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("a read and a write", "var s = new Store(); s[\"a\"] = 2; return s[\"a\"] + \"|\" + s[\"b\"] + \"|\" + s.Count;"),
        ("a compound", "var s = new Store(); s[\"a\"] += 3; s[\"a\"] *= 2; return s[\"a\"];"),
        ("an object initializer's entries", "return new Store { [\"x\"] = 1, [\"y\"] = 2 }.Count;"),
        ("an interface's default indexer", "return ((IShelf)new Shelf())[4];"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassIndexer_ReachesItsTwin(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);

    private const string Clamps = """
        public record Clamped { private readonly int[] _v = new int[4]; public int this[int i] { get => _v[i]; set { if (value > 100) value = 100; _v[i] = value; } } }
        public record NullableClamped { private readonly int?[] _v = new int?[4]; public int? this[int i] { get => _v[i]; set { if (value > 100) value = 100; _v[i] = value; } } }
        public record Flipped { private readonly bool[] _v = new bool[2]; public bool this[int i] { get => _v[i]; set { value = !value; _v[i] = value; } } }
        public record EarlyClamp { private readonly int[] _v = new int[4]; public int this[int i] { get => _v[i]; set { if (value < 0) { value = 0; _v[i] = value; return; } _v[i] = value; } } }
        """;

    /// <summary>
    /// An assignment to an indexer answers the value it assigned, whatever the setter does with its
    /// copy of <c>value</c>: the right operand, the value a compound or a step computed, the value a
    /// coalescing write wrote. The twin's setter answered its <c>value</c> after its body, which a clamp
    /// had reassigned: <c>var y = (g[0] = 250)</c> was 100, where .NET says 250.
    /// </summary>
    [SkippableTheory]
    [InlineData("var g = new Clamped(); var y = (g[0] = 250); return y + \"|\" + g[0];")]                                        // "250|100"
    [InlineData("var g = new Clamped(); g[0] = 50; var y = (g[0] += 200); return y + \"|\" + g[0];")]                             // "250|100"
    [InlineData("var g = new Clamped(); g[0] = 100; var a = g[0]++; var b = ++g[0]; return a + \"|\" + b + \"|\" + g[0];")]       // "100|101|100"
    [InlineData("var n = new NullableClamped(); var y = (n[0] ??= 250); return y + \"|\" + n[0];")]                              // "250|100"
    [InlineData("var f = new Flipped(); var y = (f[0] |= true); return y + \"|\" + f[0];")]                                       // "True|False"
    [InlineData("Clamped h = new Clamped(); var y = (h?[0] = 250); return y + \"|\" + h[0];")]                                    // "250|100"
    [InlineData("var e = new EarlyClamp(); var y = (e[0] = -5); return y + \"|\" + e[0];")]                                       // "-5|0"
    public void AnIndexerAssignment_AnswersTheValueItAssigned(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Clamps);
    }

    private const string ClassClamp = """
        public class Clamped { private readonly int[] _v = new int[4]; public int this[int i] { get => _v[i]; set { if (value > 100) value = 100; _v[i] = value; } } }
        """;

    private static readonly (string Name, string Statements)[] ClassClampCases =
    [
        // "250|100|250|100"
        ("an assignment and a compound", "var g = new Clamped(); var y = (g[0] = 250); var z = (g[1] += 250); return y + \"|\" + g[0] + \"|\" + z + \"|\" + g[1];"),
        // "100|101|100"
        ("a postfix and a prefix step", "var g = new Clamped(); g[0] = 100; var a = g[0]++; var b = ++g[0]; return a + \"|\" + b + \"|\" + g[0];"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassIndexerAssignment_AnswersTheValueItAssigned(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(ClassClamp, typeAnnotations, ClassClampCases);

    private const string Rings = """
        public class Ring
        {
            private readonly int[] _v = { 1, 2, 3 };
            public int Count => _v.Length;
            public int this[int i] { get => _v[i]; set => _v[i] = value; }
        }
        public class Logged
        {
            public static string Text = "";
            public static int At(string step, int value) { Text += step; return value; }
            private readonly int[] _v = { 1, 2, 3 };
            public int Length { get { Text += "C"; return _v.Length; } }
            public int this[int i] { get { Text += "g" + i; return _v[i]; } set { Text += "s" + i; _v[i] = value; } }
            public static Logged R(Logged l) { Text += "R"; return l; }
        }
        public class Stepped
        {
            public static string Text = "";
            public static int At(string step, int value) { Text += step; return value; }
            private readonly int[] _v = { 1, 2, 3 };
            public int Count => _v.Length;
            public int this[int i] { get { Text += "g" + i; return _v[i]; } set { Text += "s" + i; _v[i] = value; } }
            public static Stepped R(Stepped s) { Text += "R"; return s; }
        }
        """;

    private static readonly (string Name, string Statements)[] RingCases =
    [
        // "3|9"
        ("a read and a write", "var r = new Ring(); var last = r[^1]; r[^1] = 9; return last + \"|\" + r[2];"),
        // "1|2|41"
        ("a compound and a step", "var r = new Ring(); r[^1] += 1; r[^2]--; var old = r[^3]++; return old + \"|\" + r[0] + \"|\" + r[2] + r[1];"),
        // "RFCVs2|4": the receiver, the offset, the count the type names, then the value
        ("a write's order", "Logged.Text = \"\"; var l = new Logged(); var x = Logged.R(l)[^Logged.At(\"F\", 1)] = Logged.At(\"V\", 4); return Logged.Text + \"|\" + x;"),
        // "FCVs2GCg1|4|2": an offset with an effect over a receiver read by its name
        ("a named receiver's order", "Logged.Text = \"\"; var l = new Logged(); var x = l[^Logged.At(\"F\", 1)] = Logged.At(\"V\", 4); var y = l[^Logged.At(\"G\", 2)]; return Logged.Text + \"|\" + x + \"|\" + y;"),
        // "RFg2Vs2|12": the entry read before the value
        ("a compound's order", "Stepped.Text = \"\"; var s = new Stepped(); Stepped.R(s)[^Stepped.At(\"F\", 1)] += Stepped.At(\"V\", 9); return Stepped.Text + \"|\" + s[2];"),
        // "RFg2s2|3|4"
        ("a step's order", "Stepped.Text = \"\"; var s = new Stepped(); var old = Stepped.R(s)[^Stepped.At(\"F\", 1)]++; return Stepped.Text + \"|\" + old + \"|\" + s[2];"),
        // "14|2|1": a named offset, a compound and a step
        ("a named offset", "var r = new Ring(); int n = 1; r[^n] = 7; r[^n] *= 2; var old = r[^(n + 1)]--; return r[2] + \"|\" + old + \"|\" + r[1];"),
    ];

    /// <summary>
    /// A from-the-end key over a type that counts its elements is its <c>this[int]</c> at the count the
    /// bound tree names, as C# binds it: <c>r[^1]</c> is <c>r[r.Count - 1]</c>, its receiver evaluated
    /// once, then the offset, then the count, and only then a value. It went to the array lowering,
    /// which read a <c>length</c> no twin has, and its write handed the bare index along (.NET "3|9",
    /// JavaScript "undefined|3"). A read-modify-write reads the count for its read and for its write,
    /// where C# reads it once, so its cases count without an effect, the one thing that tells them apart.
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFromTheEndKey_IsTheIndexerAtTheCountTheTypeNames(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Rings, typeAnnotations, RingCases);

    private const string Steppable = """
        public enum Mode { A, B, C }
        [System.Flags] public enum Bits { None = 0, One = 1, Two = 2, Four = 4 }
        public struct Counter
        {
            public int V;
            public static Counter operator ++(Counter c) => new Counter { V = c.V + 1 };
            public static Counter operator --(Counter c) => new Counter { V = c.V - 10 };
        }
        public class Modes { private readonly Mode[] _m = new Mode[2]; public Mode this[int i] { get => _m[i]; set => _m[i] = value; } }
        public class Chars { private readonly char[] _c = { 'a', 'y' }; public char this[int i] { get => _c[i]; set => _c[i] = value; } }
        public class Longs { private readonly long[] _l = { 9007199254740993L, 0L }; public long this[int i] { get => _l[i]; set => _l[i] = value; } }
        public class Counters { private readonly Counter[] _c = new Counter[2]; public Counter this[int i] { get => _c[i]; set => _c[i] = value; } }
        public class Flags { private readonly Bits[] _b = new Bits[1]; public Bits this[int i] { get => _b[i]; set => _b[i] = value; } }
        public class Decimals { private readonly decimal[] _d = { 1.5m }; public decimal this[int i] { get => _d[i]; set => _d[i] = value; } }
        """;

    private static readonly (string Name, string Statements)[] StepCases =
    [
        // "2|B|B|A": an enum steps its value, the old one or the new one answered
        ("an enum", "var m = new Modes(); m[0]++; var old = m[0]++; var now = ++m[1]; m[1]--; return (int)m[0] + \"|\" + old + \"|\" + now + \"|\" + m[1];"),
        // "b|y|{|{": a char steps its code unit
        ("a char", "var c = new Chars(); c[0]++; var old = c[1]++; var now = ++c[1]; return c[0] + \"|\" + old + \"|\" + now + \"|\" + c[1];"),
        // "9007199254740995|9007199254740994|-1": a long steps its 64 bits
        ("a long", "var l = new Longs(); l[0]++; var old = l[0]++; var now = --l[1]; return l[0] + \"|\" + old + \"|\" + now;"),
        // "2|1|-10|2": a struct steps through its operator, an indexer's and a local's
        ("a struct's operator", "var k = new Counters(); k[0]++; var old = k[0]++; var now = --k[1]; var c = new Counter(); c++; ++c; return k[0].V + \"|\" + old.V + \"|\" + now.V + \"|\" + c.V;"),
        // "2|C|B": a flags enum steps its number, and a local enum its value
        ("a flags enum and a local enum", "var f = new Flags(); f[0]++; f[0]++; var m = Mode.A; m++; var o = m++; return (int)f[0] + \"|\" + m + \"|\" + o;"),
        // "1.5|2.5": a decimal steps on the type
        ("a decimal", "var d = new Decimals(); d[0]++; var old = d[0]--; return d[0] + \"|\" + old;"),
    ];

    /// <summary>
    /// Every step of an indexer the twin carries is a read and a write, the step being the one a local
    /// of its type gets: an enum its value, a char its code unit, a long its 64 bits, a decimal on the
    /// type, a struct through its <c>operator ++</c>, the old value or the new one answered as C#
    /// answers. A type the step rules did not name went to JavaScript's own step, written on the
    /// getter's call (<c>m.item(0)++</c>), and the module did not parse; a local enum stepped its name
    /// into NaN, and a struct's operator was written into no twin.
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStepThroughAnIndexer_IsTheStepALocalOfItsTypeGets(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Steppable, typeAnnotations, StepCases);

    private const string Explicit = """
        public interface IGrid { int this[int i] { get; } }
        public interface ICells { int this[int i] { get; set; } }
        public class Grid : IGrid { int IGrid.this[int i] => i * 10; public int this[int i] => i; }
        public class Sub : Grid { }
        public class Cells : ICells
        {
            private readonly int[] _v = new int[2];
            int ICells.this[int i] { get => _v[i] * 100; set => _v[i] = value + 1; }
            public int this[int i] { get => _v[i]; set => _v[i] = value; }
        }
        public class OnlyExplicit : IGrid { int IGrid.this[int i] => i + 1000; }
        """;

    private static readonly (string Name, string Statements)[] ExplicitCases =
    [
        // "2|20": the type's own indexer through the type, the explicit one through the interface
        ("an explicit implementation beside the type's own", "var g = new Grid(); IGrid h = g; return g[2] + \"|\" + h[2];"),
        // "3|30|3": a derived type's, through each
        ("through a derived type", "var s = new Sub(); IGrid h = s; Grid g = s; return s[3] + \"|\" + h[3] + \"|\" + g[3];"),
        // "502|8|50200|800": the setters apart, a compound through the interface
        ("a setter of each", "var c = new Cells(); ICells i = c; c[0] = 5; i[1] = 7; i[0] += 1; return c[0] + \"|\" + c[1] + \"|\" + i[0] + \"|\" + i[1];"),
        // 1005
        ("an explicit implementation alone", "IGrid h = new OnlyExplicit(); return h[5];"),
    ];

    /// <summary>
    /// An explicit implementation of an interface's indexer answers an access through the interface,
    /// and the type's own indexer an access through the type, as C# binds them: the explicit one is the
    /// twin's <c>item</c>, which every access through an interface reaches, and the type's own takes
    /// names of its own beside it. EQ1007 counted the explicit one as a second indexer, and the module
    /// was not written.
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExplicitIndexer_AnswersItsInterface(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Explicit, typeAnnotations, ExplicitCases);

    private const string Keyed = """
        public class Grid
        {
            private readonly int[] _cells = new int[16];
            public int this[int row, int col = 0] { get => _cells[row * 4 + col]; set => _cells[row * 4 + col] = value; }
        }
        public class Path
        {
            public int Last;
            public int this[params int[] path] { get => path.Length * 100 + (path.Length > 0 ? path[0] : 0); set { Last = path.Length * 1000 + value; } }
        }
        public static class Log
        {
            public static string Text = "";
            public static int At(string step, int value) { Text += step; return value; }
        }
        """;

    private static readonly (string Name, string Statements)[] KeyedCases =
    [
        // "5|7|7": an omitted key takes its default, and a named one fills its own parameter
        ("an optional key and named keys", "var g = new Grid(); g[1] = 5; g[2, 1] = 7; return g[1] + \"|\" + g[2, 1] + \"|\" + g[col: 1, row: 2];"),
        // "crvCR|7|7": named keys out of order are evaluated where they are written
        ("named keys out of order", "Log.Text = \"\"; var g = new Grid(); g[col: Log.At(\"c\", 1), row: Log.At(\"r\", 2)] = Log.At(\"v\", 7); var x = g[col: Log.At(\"C\", 1), row: Log.At(\"R\", 2)]; return Log.Text + \"|\" + x + \"|\" + g[2, 1];"),
        // "201|305|2010|1110": params keys packed as C# packs them, an array passed whole, one array for a compound's read and write
        ("a params key", "var t = new Path(); var a = t[1, 2]; var b = t[new[] { 5, 6, 7 }]; t[4, 5] = 10; var c = t.Last; t[9] += 1; return a + \"|\" + b + \"|\" + c + \"|\" + t.Last;"),
        // "8|6|9|8": the keys of an initializer's entry, a null-conditional write, a compound and a step
        ("every writer's keys", "var g = new Grid { [1] = 5, [col: 3, row: 2] = 6 }; Grid h = g; h?[3] = 8; g[1] += 2; g[1]++; var old = g[3]++; return g[1] + \"|\" + g[2, 3] + \"|\" + g[3] + \"|\" + old;"),
    ];

    /// <summary>
    /// An indexer's keys are passed as the bound tree binds them, each in its parameter's place: an
    /// omitted optional key as its default, a named one where it is named, and evaluated where it is
    /// written, a params key packed into its array. They were passed as written, so
    /// <c>g[1] = 5</c> over <c>this[int row, int col = 0]</c> put 5 in <c>col</c> and left the value
    /// undefined (.NET "5|7|7", JavaScript "undefined|7|0").
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnIndexersKeys_ArePassedByParameter(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Keyed, typeAnnotations, KeyedCases);
}
