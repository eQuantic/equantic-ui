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
}
