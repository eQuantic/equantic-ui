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

    private const string Published = """
        public record RLast
        {
            public static RLast Last;
            public int A { get; set; }
            public int B { get; set; }
            public List<int> Items { get; } = new();
            public Dictionary<string, int> Map { get; } = new();
            public RLast() { Last = this; }
        }
        public record GLog { public static string Text = ""; public static int Note(string s, int v) { Text += s; return v; } }
        public record GInner { public int N { get; set; } }
        public record GBox
        {
            private readonly List<int> _items = new();
            private readonly GInner _inner = new();
            public List<int> Items { get { GLog.Text += "get "; return _items; } }
            public GInner Inner { get { GLog.Text += "inner "; return _inner; } }
        }
        """;

    /// <summary>
    /// C# applies each element of an initializer before it evaluates the next, so a part that reads
    /// the object being built (here through the static its constructor publishes it in) sees every
    /// element before it applied. Every part was evaluated first and the elements applied after, so
    /// `new RLast { A = 1, B = RLast.Last.A }` set B to 0 where .NET sets 1 (found by Copilot's review
    /// of #608).
    /// </summary>
    [SkippableTheory]
    [InlineData("var p = new RLast { A = 1, B = RLast.Last.A }; return p.B;")]                                                         // 1
    [InlineData("var p = new RLast { Items = { 1, RLast.Last.Items.Count, RLast.Last.Items.Count } }; return string.Join(\",\", p.Items);")] // "1,1,2"
    [InlineData("var p = new RLast { Map = { [\"a\"] = 1, [\"b\"] = RLast.Last.Map.Count } }; return p.Map[\"b\"];")]                   // 1
    [InlineData("var p = new RLast { A = 2, Items = { RLast.Last.A }, B = RLast.Last.Items[0] + 1 }; return p.Items[0] + \"|\" + p.B;")] // "2|3"
    // The member an element adds to is read before the element's parts, again for each element.
    [InlineData("GLog.Text = \"\"; var b = new GBox { Items = { GLog.Note(\"a \", 1), GLog.Note(\"b \", 2) }, Inner = { N = GLog.Note(\"n \", 3) } }; return GLog.Text + \"|\" + b.Items.Count + b.Inner.N;")] // "get a get b inner n |23"
    public void AnInitializersElement_IsAppliedBeforeTheNextIsEvaluated(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Published);
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

    private const string Rebuilt = """
        using System.Collections.Generic;
        using System.Linq;

        public class Node { public List<int> Children { get; } = new(); public int Count; public void Attach(Node n) { Count += n.Children.Count; } }
        public class Card { public List<string> Lines { get; } = new() { "head" }; }
        public class Person { public string Name = ""; }
        public class Box { public ICollection<int> Items { get; } = new List<int> { 0 }; public IList<int> List { get; } = new List<int>(); }
        """;

    private static readonly (string Name, string Statements)[] RebuiltCases =
    [
        // 2: an initializer inside a null-conditional call, whose nodes the strategy rebuilt
        ("in a null-conditional call", "var p = new Node(); Node q = p; q?.Attach(new Node { Children = { 1, 2 } }); return p.Count;"),
        // "head,a;head,b": inside a query's clause, which the query lowering rebuilds
        ("in a query", "var people = new List<Person> { new Person { Name = \"a\" }, new Person { Name = \"b\" } }; var cards = (from p in people select new Card { Lines = { p.Name } }).ToList(); return string.Join(\";\", cards.Select(c => string.Join(\",\", c.Lines)));"),
        // "3|3": a collection interface's Add, as a call to it lowers
        ("a collection interface's members", "var b = new Box { Items = { 1, 2 }, List = { 3 } }; return b.Items.Count + \"|\" + b.List[0];"),
    ];

    /// <summary>
    /// A nested initializer's element is added through the Add the bound tree binds, asked of it through
    /// the guard every node a strategy rebuilt takes: inside a null-conditional call or a query, the model
    /// was asked of a node not in its tree, and the module was never written. A collection interface's
    /// Add (<c>ICollection&lt;int&gt;</c>, <c>IList&lt;int&gt;</c>) lowers as a call to it lowers, where it
    /// was refused (EQ1004).
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnElementsAdd_IsTheOneTheBoundTreeBinds(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Rebuilt, typeAnnotations, RebuiltCases);

    private const string Extended = """
        using System.Collections.Generic;
        using System.Linq;

        public static class TagExtensions { public static void Add(this List<string> tags, int n) => tags.Add("#" + n); }
        public static class NameExtensions { extension(HashSet<string> names) { public void Add(int n) => names.Add("s" + n); } }
        public static class MapExtensions { public static void Add(this Dictionary<string, int> map, int k, int v) => map.Add("k" + k, v); }
        public static class BagExtensions { public static void Add(this Bag bag, int n) => bag.Items.Add("ext:" + n); }

        public class Bag : System.Collections.IEnumerable
        {
            public System.Collections.IEnumerator GetEnumerator() { foreach (var item in Items) yield return item; }
            public List<string> Items { get; } = new();
            public void Add(string s) => Items.Add("bag:" + s);
        }

        public class Post
        {
            public List<string> Tags { get; } = new() { "seed" };
            public HashSet<string> Names { get; } = new();
            public Dictionary<string, int> Map { get; } = new() { ["seed"] = 0 };
            public Bag Bag { get; } = new();
        }
        """;

    private static readonly (string Name, string Statements)[] ExtendedCases =
    [
        // "seed,#1,x,#2": a list's own Add beside an extension's
        ("a list member", "var p = new Post { Tags = { 1, \"x\", 2 } }; return string.Join(\",\", p.Tags);"),
        // "s1,y": a C# 14 extension block's Add on a set
        ("a set member", "var p = new Post { Names = { 1, \"y\" } }; return string.Join(\",\", p.Names.OrderBy(n => n));"),
        // "k1,seed,z": a dictionary's pair through an extension
        ("a dictionary member", "var p = new Post { Map = { { 1, 2 }, { \"z\", 3 } } }; return string.Join(\",\", p.Map.Keys.OrderBy(k => k));"),
        // "ext:7,bag:w": a type's own Add(string) beside an extension Add(int)
        ("a type with an Add of its own", "var p = new Post { Bag = { 7, \"w\" } }; return string.Join(\",\", p.Bag.Items);"),
        // "#1,a,#2|#4,b|x,#5": a list's own initializer, explicit, target-typed and over a source
        ("a list's own initializer", "var l = new List<string> { 1, \"a\", 2 }; List<string> t = new() { 4, \"b\" }; var m = new List<string>(new[] { \"x\" }) { 5 }; return string.Join(\",\", l) + \"|\" + string.Join(\",\", t) + \"|\" + string.Join(\",\", m);"),
        // "#3": the call, the control
        ("a call", "var l = new List<string>(); l.Add(3); return string.Join(\",\", l);"),
    ];

    /// <summary>
    /// An element the bound tree adds through an EXTENSION method, or a C# 14 extension block's member,
    /// goes to its home's static with the collection first, as every call to it lowers; a collection's
    /// own lowering applies only to its own Add. It was written as the collection's own: a list has no
    /// <c>add</c> and threw, and a set's, a dictionary's and a type's own Add ran in the extension's
    /// place, in silence (.NET "seed,#1,x,#2", JavaScript a TypeError).
    /// </summary>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnElementAnExtensionAdds_GoesToItsHome(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Extended, typeAnnotations, ExtendedCases);
}
