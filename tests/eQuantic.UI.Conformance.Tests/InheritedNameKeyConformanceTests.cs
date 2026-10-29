using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A key named like what every JavaScript object inherits — <c>__proto__</c>, <c>constructor</c>,
/// <c>toString</c>, <c>hasOwnProperty</c>, <c>valueOf</c> — is a key like any other, through every form
/// that builds a dictionary, and an empty collection expression builds the collection its target names
/// (#1144).
/// <para>
/// A plain object holds such a key only as its own entry: an assignment of <c>__proto__</c> hands the
/// value to the prototype's setter, and a read of <c>constructor</c> answers the function every object
/// inherits. Every dictionary has been the runtime's <c>Dictionary</c>, held by a <c>Map</c>, since #443,
/// so these keys were already safe wherever a dictionary is constructed; the first theory pins it,
/// against data a diff, a parser or a router hands a dictionary (a JavaScript source is full of them).
/// </para>
/// <para>
/// The second theory is the form that did not build one. <c>Dictionary&lt;string, int&gt; ids = [];</c>,
/// which the IDE suggests for <c>new()</c>, went out as an array, and so did every collection of the
/// runtime's own started with <c>[]</c>: a dictionary's write threw, its count was null, a queue had no
/// <c>enqueue</c>.
/// </para>
/// </summary>
public class InheritedNameKeyConformanceTests
{
    /// <summary>Reads a dictionary back through every lookup, by every inherited name: its count and its
    /// keys, then each name's value through <c>TryGetValue</c> and its presence through <c>ContainsKey</c>.</summary>
    private const string Read =
        "string Read(Dictionary<string, int> d) { var text = d.Count + \":\" + string.Join(\",\", d.Keys); "
        + "foreach (var name in new[] { \"__proto__\", \"constructor\", \"toString\", \"hasOwnProperty\", \"valueOf\" }) "
        + "text += \"|\" + name + \"=\" + (d.TryGetValue(name, out var value) ? value.ToString() : \"-\") + (d.ContainsKey(name) ? \"+\" : \"!\"); "
        + "return text; } ";

    private const string Holder = """
        public record Holder { public Dictionary<string, int> Map { get; } = new(); }
        public record Started { public Dictionary<string, int> Map { get; set; } = []; }
        """;

    [SkippableTheory]
    [InlineData(Read + "var d = new Dictionary<string, int>(); d[\"__proto__\"] = 1; d[\"constructor\"] = 2; d[\"toString\"] = 3; return Read(d);")]
    [InlineData(Read + "Dictionary<string, int> d = new(); d[\"__proto__\"] = 1; d[\"constructor\"] = 2; d[\"toString\"] = 3; return Read(d);")]
    [InlineData(Read + "var d = new Dictionary<string, int> { { \"__proto__\", 1 }, { \"constructor\", 2 }, { \"toString\", 3 } }; return Read(d);")]
    [InlineData(Read + "var d = new Dictionary<string, int> { [\"__proto__\"] = 1, [\"constructor\"] = 2, [\"toString\"] = 3 }; return Read(d);")]
    [InlineData(Read + "var s = new Dictionary<string, int> { [\"__proto__\"] = 1, [\"toString\"] = 3 }; var d = new Dictionary<string, int>(s); d[\"valueOf\"] = 4; return Read(d) + \"/\" + Read(s);")]
    [InlineData(Read + "var d = new Dictionary<string, int>(new List<KeyValuePair<string, int>> { new(\"__proto__\", 1), new(\"constructor\", 2) }); return Read(d);")]
    [InlineData(Read + "var d = new[] { \"__proto__\", \"constructor\", \"toString\" }.ToDictionary(s => s, s => s.Length); return Read(d);")]
    [InlineData(Read + "var d = new[] { \"__proto__\", \"constructor\", \"__proto__\", \"toString\" }.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count()); return Read(d);")]
    [InlineData(Read + "var h = new Holder { Map = { [\"__proto__\"] = 1, [\"constructor\"] = 2 } }; return Read(h.Map);")]
    [InlineData(Read + "var h = new Holder { Map = { { \"__proto__\", 1 }, { \"toString\", 3 } } }; return Read(h.Map);")]
    [InlineData(Read + "var d = new Dictionary<string, int>(); d[\"__proto__\"] = 1; d[\"__proto__\"] += 5; d.TryAdd(\"constructor\", 2); d.TryAdd(\"constructor\", 9); return Read(d) + \"|\" + d.GetValueOrDefault(\"toString\", -1);")]
    [InlineData(Read + "var d = new Dictionary<string, int> { [\"__proto__\"] = 1, [\"a\"] = 2 }; d.Remove(\"__proto__\"); d[\"toString\"] = 3; return Read(d);")]
    [InlineData("var d = new Dictionary<string, int> { [\"__proto__\"] = 1, [\"toString\"] = 2 }; var t = \"\"; foreach (var (k, v) in d) t += k + v; return t;")]
    [InlineData("var d = new Dictionary<string, object>(); d[\"__proto__\"] = new List<int> { 1 }; d[\"constructor\"] = \"x\"; return d.Count + \":\" + string.Join(\",\", d.Keys) + \":\" + d.ContainsKey(\"__proto__\") + d[\"constructor\"];")]
    [InlineData("IReadOnlyDictionary<string, int> r = new Dictionary<string, int> { [\"__proto__\"] = 1 }; return r[\"__proto__\"] + \",\" + r.ContainsKey(\"toString\");")]
    [InlineData("var d = new SortedDictionary<string, int> { [\"toString\"] = 1, [\"__proto__\"] = 2, [\"constructor\"] = 3 }; return string.Join(\",\", d.Keys) + d[\"__proto__\"];")]
    [InlineData("var d = new SortedList<string, int> { [\"toString\"] = 1, [\"__proto__\"] = 2, [\"constructor\"] = 3 }; return string.Join(\",\", d.Keys) + d[\"__proto__\"];")]
    [InlineData("var s = new HashSet<string> { \"__proto__\", \"constructor\" }; return s.Count + \",\" + s.Contains(\"__proto__\") + s.Contains(\"toString\") + s.Add(\"__proto__\");")]
    [InlineData("var l = new[] { \"__proto__\", \"toString\", \"__proto__\" }.ToLookup(s => s); return l[\"__proto__\"].Count() + \",\" + l[\"constructor\"].Count() + \",\" + l.Count;")]
    public void InheritedNames_AreKeysLikeAnyOther(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Holder);
    }

    [SkippableTheory]
    [InlineData(Read + "Dictionary<string, int> d = []; d[\"__proto__\"] = 1; d[\"constructor\"] = 2; d[\"toString\"] = 3; return Read(d);")]
    [InlineData("Dictionary<string, int> d = []; return d.Count;")]
    [InlineData("Dictionary<string, int> d = [with(10)]; d[\"a\"] = 1; return d.EnsureCapacity(0) + \",\" + d.Count;")]
    [InlineData("Dictionary<int, int> d = [with(capacity: 3)]; d[2] = 20; d[1] = 10; return string.Join(\",\", d.Keys) + \",\" + d.EnsureCapacity(0);")]
    [InlineData(Read + "var s = new Started(); s.Map[\"__proto__\"] = 1; return Read(s.Map);")]
    [InlineData(Read + "var s = new Started { Map = [] }; s.Map[\"constructor\"] = 2; return Read(s.Map);")]
    [InlineData("SortedDictionary<string, int> d = []; d[\"b\"] = 1; d[\"a\"] = 2; return string.Join(\",\", d.Keys);")]
    [InlineData("SortedList<string, int> d = []; d[\"b\"] = 1; d[\"a\"] = 2; return string.Join(\",\", d.Keys);")]
    [InlineData("SortedList<string, int> d = [with(8)]; d[\"b\"] = 1; d[\"a\"] = 2; return string.Join(\",\", d.Keys);")]
    [InlineData("Queue<int> q = []; q.Enqueue(1); q.Enqueue(2); return q.Dequeue() + \",\" + q.Count;")]
    [InlineData("Queue<int> q = [with(4)]; q.Enqueue(1); return q.Capacity + \",\" + q.Count;")]
    [InlineData("Stack<int> s = []; s.Push(1); s.Push(2); return s.Pop() + \",\" + s.Count;")]
    [InlineData("Stack<int> s = [with(3)]; return s.Capacity;")]
    [InlineData("LinkedList<int> l = []; l.AddLast(1); l.AddFirst(0); return string.Join(\",\", l) + \",\" + l.Count;")]
    [InlineData("Dictionary<string, int> Make(bool empty) => empty ? [] : new() { [\"a\"] = 1 }; return Make(true).Count + \",\" + Make(false).Count;")]
    public void AnEmptyCollectionExpression_BuildsItsTarget(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Holder);
    }
}
