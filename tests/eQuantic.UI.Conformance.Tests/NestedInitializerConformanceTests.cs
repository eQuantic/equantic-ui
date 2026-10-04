using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A nested collection initializer adds to the collection the member already holds, through its
/// <c>Add</c>, and a nested object initializer assigns into the object the member holds (#462). Both
/// were written as a new value handed to the member, so whatever the member was initialized with was
/// gone: <c>new Box { Items = { 1, 2 } }</c> counted 2 where .NET counts 3.
/// </summary>
public class NestedInitializerConformanceTests
{
    private const string Records = """
        public record RBox { public List<int> Items { get; init; } = new() { 0 }; public int Count { get; init; } }
        public record RMap { public Dictionary<string, int> Map { get; init; } = new() { ["x"] = 0 }; }
        public record RSet { public HashSet<int> Set { get; } = new() { 0 }; }
        public record RNull { public List<int> Items { get; init; } }
        public record RInner { public List<int> Items { get; } = new() { 1 }; public int N { get; set; } }
        public record ROuter { public RInner Inner { get; } = new(); }
        public struct SBag { public List<string> Tags; public SBag() { Tags = new() { "a" }; } }
        public record REmpty { public List<int> Items { get; } = new(); public HashSet<int> Set { get; } = new(); public Dictionary<string, int> Map { get; } = new(); }
        """;

    [SkippableTheory]
    [InlineData("return new RBox { Items = { 1, 2 } }.Items.Count;")]                                       // 3
    [InlineData("var b = new RBox { Items = { 1, 2 }, Count = 5 }; return string.Join(\",\", b.Items) + \"|\" + b.Count;")] // "0,1,2|5"
    [InlineData("return new RMap { Map = { [\"a\"] = 1 } }.Map.Count;")]                                     // 2
    [InlineData("return new RMap { Map = { [\"x\"] = 7 } }.Map[\"x\"];")]                                    // 7
    [InlineData("return new RMap { Map = { { \"b\", 2 } } }.Map[\"b\"] + new RMap { Map = { { \"b\", 2 } } }.Map.Count;")] // 4
    [InlineData("return new RSet { Set = { 3, 0 } }.Set.Count;")]                                           // 2
    [InlineData("try { new RNull { Items = { 1 } }; return \"no\"; } catch (NullReferenceException) { return \"threw\"; }")] // "threw"
    [InlineData("var o = new ROuter { Inner = { Items = { 5 }, N = 3 } }; return o.Inner.Items.Count + \"|\" + o.Inner.N;")] // "2|3"
    [InlineData("var s = new SBag { Tags = { \"b\" } }; return string.Join(\",\", s.Tags);")]                 // "a,b"
    [InlineData("var e = new REmpty { Items = { 1 }, Set = { 2, 2 }, Map = { [\"k\"] = 3 } }; return e.Items.Count + \"|\" + e.Set.Count + \"|\" + e.Map[\"k\"];")] // "1|1|3"
    public void ANestedInitializer_AddsToWhatTheMemberHolds(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Records);
    }

    private const string Awaited = """
        public record Holder { public List<int> Items { get; } = new(); }
        public record Keyed { public Dictionary<string, int> Map { get; } = new(); }
        public record Nest { public Holder Inner { get; } = new(); public int N { get; set; } }
        """;

    /// <summary>
    /// Every part of the C# an initializer applies is evaluated in the caller's own function: an
    /// <c>await</c> in an element, a value or a key stays in the async method it was written in. Each
    /// was written inside the function that applies the initializer, which is not async, and the module
    /// did not parse. Each part is evaluated once, in the order C# evaluates it.
    /// </summary>
    [SkippableTheory]
    [InlineData("async Task<int> G() { await Task.Yield(); return 3; } var h = new Holder { Items = { await G(), 4 } }; return h.Items.Count + \":\" + h.Items[0];")] // "2:3"
    [InlineData("async Task<int> G() { await Task.Yield(); return 5; } var k = new Keyed { Map = { { \"a\", await G() }, { \"b\", await G() + 1 } } }; return k.Map[\"a\"] + k.Map[\"b\"];")] // 11
    [InlineData("async Task<int> G() { await Task.Yield(); return 5; } var k = new Keyed { Map = { [\"a\"] = await G() } }; return k.Map[\"a\"];")] // 5
    [InlineData("async Task<string> K() { await Task.Yield(); return \"k\"; } var k = new Keyed { Map = { [await K()] = 1 } }; return k.Map[\"k\"];")] // 1
    [InlineData("async Task<int> G() { await Task.Yield(); return 7; } var n = new Nest { Inner = { Items = { await G() } }, N = await G() }; return n.Inner.Items[0] + n.N;")] // 14
    [InlineData("var log = \"\"; int Next(int v) { log += v; return v; } var h = new Holder { Items = { Next(1), Next(2), Next(3) } }; return string.Join(\",\", h.Items) + \":\" + log;")] // "1,2,3:123"
    public void AnInitializersParts_RunInTheCallersFunction(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Awaited);
    }

    /// <summary>
    /// A pair of a collection initializer applied to a dictionary a member holds is that dictionary's
    /// <c>Add</c>, which refuses a key already there, as every call to it lowers. It was written as the
    /// indexer's <c>set</c>, which replaced the first value without a word.
    /// </summary>
    [SkippableTheory]
    [InlineData("try { var k = new Keyed { Map = { { \"a\", 1 }, { \"a\", 2 } } }; return \"added \" + k.Map[\"a\"]; } catch (Exception) { return \"refused\"; }")] // "refused"
    [InlineData("var k = new Keyed { Map = { [\"a\"] = 1, [\"a\"] = 2 } }; return k.Map[\"a\"] + \"|\" + k.Map.Count;")] // "2|1": the indexer replaces
    public void APairAddedToAMembersDictionary_IsItsAdd(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Awaited);
    }

    private const string Classes = """
        using System.Collections.Generic;

        public class Box { public List<int> Items { get; init; } = new() { 0 }; public int Count { get; set; } }
        public class Holder { public Dictionary<string, int> Map { get; init; } = new() { ["x"] = 0 }; }
        public class Tags { public HashSet<string> Set { get; } = new() { "a" }; }
        public class Inner { public List<int> Items { get; } = new() { 1 }; public int N { get; set; } }
        public class Outer { public Inner Inner { get; } = new(); }
        public class Bag : System.Collections.IEnumerable
        {
            private readonly List<string> _items = new();
            public void Add(string item) => _items.Add(item.ToUpper());
            public int Count => _items.Count;
            public string First => _items[0];
            public System.Collections.IEnumerator GetEnumerator() { foreach (var item in _items) yield return item; }
        }
        public class Pocket { public Bag Bag { get; } = new() { "seed" }; }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("a list member", "return new Box { Items = { 1, 2 } }.Items.Count;"),
        ("a list member beside an assignment", "var b = new Box { Items = { 1, 2 }, Count = 5 }; return string.Join(\",\", b.Items) + \"|\" + b.Count;"),
        ("a dictionary member by key", "return new Holder { Map = { [\"a\"] = 1 } }.Map.Count;"),
        ("a dictionary member by pair", "return new Holder { Map = { { \"b\", 2 } } }.Map[\"b\"];"),
        ("a set member", "return new Tags { Set = { \"a\", \"b\" } }.Set.Count;"),
        ("a nested object initializer", "var o = new Outer { Inner = { Items = { 5 }, N = 3 } }; return o.Inner.Items.Count + \"|\" + o.Inner.N;"),
        ("a member's own Add", "var p = new Pocket { Bag = { \"two\" } }; return p.Bag.Count + \"|\" + p.Bag.First;"),
        ("a target-typed construction", "Box b = new() { Items = { 9 } }; return b.Items.Count;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANestedInitializer_AddsToWhatAClassMemberHolds(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);
}
