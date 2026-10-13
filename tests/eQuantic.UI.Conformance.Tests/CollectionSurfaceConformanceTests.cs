using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The members of .NET's <c>Queue&lt;T&gt;</c>, <c>Stack&lt;T&gt;</c>, <c>LinkedList&lt;T&gt;</c>,
/// <c>LinkedListNode&lt;T&gt;</c> and <c>SortedSet&lt;T&gt;</c> that compiled and were a TypeError in the
/// browser, because eqc lowered any member of the five to its camel-cased name whether or not the
/// runtime's twin carried it (#866): <c>TryDequeue</c>, <c>TryPop</c>, <c>TryPeek</c> and
/// <c>TryGetValue</c>, a queue's and a stack's <c>Capacity</c>, <c>EnsureCapacity</c> and
/// <c>TrimExcess</c>, a linked list's <c>Find</c>, <c>FindLast</c>, <c>AddAfter</c>, <c>AddBefore</c> and
/// <c>CopyTo</c>, and the overloads that take a node. <c>Remove(node)</c> was no TypeError, and removed
/// nothing; <c>new LinkedListNode&lt;T&gt;(value)</c> built a linked list of the value. Each runs on both
/// sides, refusals included.
/// <para>
/// A capacity is .NET's: the array a queue or a stack holds its elements in, grown by each one's own
/// rule, and sized by a construction from a sequence as <c>EnumerableHelpers.ToArray</c> sizes it, to the
/// count of an <c>ICollection&lt;T&gt;</c> and by doubling from four for anything else, a queue, a stack
/// and a string among them. A node belongs to one list at a time, and one removed from its list,
/// cleared with it included, belongs to none and links to nothing.
/// </para>
/// </summary>
public class CollectionSurfaceConformanceTests
{
    /// <summary>The statements, run in a <c>try</c> whose answer names the refusal: its type, its
    /// parameter where it has one, and its message.</summary>
    private static string Refused(string statements) =>
        "try { " + statements + " } "
        + "catch (ArgumentNullException e) { return \"null:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentOutOfRangeException e) { return \"range:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentException e) { return \"argument:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (InvalidOperationException e) { return \"operation:\" + e.Message; }";

    /// <summary>The statements, which add what they walk to <c>s</c>, and then what the walk was refused
    /// with, if anything.</summary>
    private static string Walked(string statements) =>
        "var s = \"\"; try { " + statements + " return s; } "
        + "catch (InvalidOperationException e) { return s + \"|\" + e.Message; }";

    [SkippableTheory]
    // Each take answers whether there was an element, and writes it or the type's default.
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); var a = q.TryDequeue(out var x); var b = q.TryPeek(out var y); var c = q.TryDequeue(out var z); var d = q.TryDequeue(out var w); var e = q.TryPeek(out var v); return a + \":\" + x + \",\" + b + \":\" + y + \",\" + c + \":\" + z + \",\" + d + \":\" + w + \",\" + e + \":\" + v + \"|\" + q.Count;")] // True:1,True:2,True:2,False:0,False:0|0
    [InlineData("var t = new Stack<int>(new[] { 1, 2 }); var a = t.TryPop(out var x); var b = t.TryPeek(out var y); var c = t.TryPop(out var z); var d = t.TryPop(out var w); var e = t.TryPeek(out var v); return a + \":\" + x + \",\" + b + \":\" + y + \",\" + c + \":\" + z + \",\" + d + \":\" + w + \",\" + e + \":\" + v + \"|\" + t.Count;")] // True:2,True:1,True:1,False:0,False:0|0
    [InlineData("var q = new Queue<string>(); var t = new Stack<string>(); return q.TryDequeue(out var a) + \":\" + (a == null) + \",\" + q.TryPeek(out var b) + \":\" + (b == null) + \",\" + t.TryPop(out var c) + \":\" + (c == null) + \",\" + t.TryPeek(out var d) + \":\" + (d == null);")] // False:True x4
    [InlineData("var q = new Queue<int?>(new int?[] { null }); var a = q.TryPeek(out var x); var b = q.TryDequeue(out var y); return a + \":\" + (x == null) + \",\" + b + \":\" + (y == null) + \",\" + q.Count;")] // True:True,True:True,0
    // A variable that held a value is written the default when there is nothing to take.
    [InlineData("var q = new Queue<int>(); var t = new Stack<int>(); int x = 7; int y = 8; var a = q.TryDequeue(out x); var b = t.TryPop(out y); return a + \":\" + x + \",\" + b + \":\" + y;")] // False:0,False:0
    // A discard, an element and a field each take what a variable takes, each part read once and in place.
    [InlineData("var q = new Queue<int>(new[] { 1 }); var a = q.TryDequeue(out _); var b = q.TryDequeue(out _); return a + \",\" + b + \",\" + q.Count;")] // True,False,0
    [InlineData("var q = new Queue<int>(new[] { 5 }); var arr = new int[2]; var i = 0; var a = q.TryDequeue(out arr[i++]); var b = q.TryDequeue(out arr[i++]); return a + \",\" + b + \":\" + string.Join(\",\", arr) + \":\" + i;")] // True,False:5,0:2
    [InlineData("var log = \"\"; var q = new Queue<int>(new[] { 1, 2 }); Queue<int> Q() { log += \"q\"; return q; } var a = Q().TryDequeue(out var x); var b = Q().TryPeek(out var y); return log + a + x + b + y;")] // qqTrue1True2
    // The idioms that drain them.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); var t = new Stack<int>(new[] { 1, 2, 3 }); var s = \"\"; while (q.TryDequeue(out var x)) s += x; while (t.TryPop(out var y)) s += y; return s;")] // 123321
    // TryGetValue answers the element the set holds, which a decimal's scale tells apart.
    [InlineData("var s = new SortedSet<int> { 1, 3 }; var a = s.TryGetValue(3, out var x); var b = s.TryGetValue(2, out var y); return a + \":\" + x + \",\" + b + \":\" + y;")] // True:3,False:0
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m }; var a = s.TryGetValue(1.00m, out var x); return a + \":\" + x;")] // True:1.0
    [InlineData("var s = new SortedSet<string> { \"a\" }; var a = s.TryGetValue(\"b\", out var x); return a + \":\" + (x == null);")] // False:True
    // The receiver is read before every argument, an out named before the value included.
    [InlineData("var log = \"\"; var s = new SortedSet<int> { 1, 2 }; var slots = new int[2]; var i = 0; SortedSet<int> Get() { log += \"g\" + i; return s; } int Key() { log += \"k\" + i; return 2; } var a = Get().TryGetValue(actualValue: out slots[i++], equalValue: Key()); return log + \"|\" + a + \"|\" + string.Join(\",\", slots) + \"|\" + i;")] // g0k1|True|2,0|1
    public void ATake_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A queue grows to twice its array, four more at least, and a stack to twice, four from empty.
    [InlineData("var q = new Queue<int>(); var t = new Stack<int>(); var s = q.Capacity + \",\" + t.Capacity + \":\"; for (var i = 0; i < 10; i++) { q.Enqueue(i); t.Push(i); s += q.Capacity + \"/\" + t.Capacity + \",\"; } return s;")] // 0,0:4/4,…,16/16,
    [InlineData("var q = new Queue<int>(1); var t = new Stack<int>(1); var s = q.Capacity + \"/\" + t.Capacity + \",\"; for (var i = 0; i < 6; i++) { q.Enqueue(i); t.Push(i); s += q.Capacity + \"/\" + t.Capacity + \",\"; } return s;")] // 1/1,1/1,5/2,5/4,5/4,5/8,10/8,
    // A capacity asked for, or a sequence's: its count for an ICollection<T>, doubling from four otherwise.
    [InlineData("return new Queue<int>(5).Capacity + \",\" + new Queue<int>(new[] { 1, 2, 3 }).Capacity + \",\" + new Queue<char>(\"abcde\").Capacity + \",\" + new Queue<int>(new List<int> { 1, 2 }).Capacity + \",\" + new Queue<int>(new HashSet<int> { 1, 2, 3 }).Capacity + \",\" + new Queue<int>(new Queue<int>(new[] { 1, 2, 3, 4, 5 })).Capacity + \",\" + new Queue<int>(new Stack<int>(new[] { 1, 2, 3 })).Capacity + \",\" + new Queue<int>(new SortedSet<int> { 1, 2, 3 }).Capacity + \",\" + new Queue<int>(new LinkedList<int>(new[] { 1, 2, 3 })).Capacity + \",\" + new Queue<int>(new int[0]).Capacity + \",\" + new Queue<char>(\"\").Capacity;")] // 5,3,8,2,3,8,4,3,3,0,0
    [InlineData("return new Stack<int>(5).Capacity + \",\" + new Stack<int>(new[] { 1, 2, 3 }).Capacity + \",\" + new Stack<char>(\"abcde\").Capacity + \",\" + new Stack<int>(new List<int> { 1, 2 }).Capacity + \",\" + new Stack<int>(new Queue<int>(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })).Capacity + \",\" + new Stack<int>(new Stack<int>(new[] { 1 })).Capacity;")] // 5,3,8,2,16,4
    // EnsureCapacity grows by the same rule, to the capacity asked for at least, and answers the capacity.
    [InlineData("var q = new Queue<int>(); var t = new Stack<int>(); return q.EnsureCapacity(3) + \",\" + q.EnsureCapacity(2) + \",\" + q.EnsureCapacity(10) + \",\" + q.EnsureCapacity(11) + \",\" + q.EnsureCapacity(0) + \"|\" + t.EnsureCapacity(3) + \",\" + t.EnsureCapacity(2) + \",\" + t.EnsureCapacity(10) + \",\" + t.EnsureCapacity(11) + \",\" + t.Capacity;")] // 4,4,10,20,20|4,4,10,20,20
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); q.Dequeue(); var a = q.EnsureCapacity(5); q.Enqueue(3); q.Enqueue(4); q.Enqueue(5); return a + \":\" + string.Join(\",\", q) + \":\" + q.Peek();")] // 6:2,3,4,5:2
    // TrimExcess() trims below nine tenths of the capacity, and TrimExcess(n) to n.
    [InlineData("var q = new Queue<int>(); var t = new Stack<int>(); for (var i = 0; i < 5; i++) { q.Enqueue(i); t.Push(i); } var a = q.Capacity + \"/\" + t.Capacity; q.TrimExcess(); t.TrimExcess(); var b = q.Capacity + \"/\" + t.Capacity; q.Enqueue(9); t.Push(9); return a + \",\" + b + \",\" + q.Capacity + \"/\" + t.Capacity + \":\" + string.Join(\",\", q) + \":\" + string.Join(\",\", t);")] // 8/8,5/5,10/10:0,1,2,3,4,9:9,4,3,2,1,0
    [InlineData("var q = new Queue<int>(); var t = new Stack<int>(); for (var i = 0; i < 7; i++) { q.Enqueue(i); t.Push(i); } q.TrimExcess(); t.TrimExcess(); return q.Capacity + \"/\" + t.Capacity;")] // 8/8
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); var t = new Stack<int>(new[] { 1, 2 }); q.TrimExcess(6); t.TrimExcess(6); var a = q.Capacity + \"/\" + t.Capacity; q.TrimExcess(2); t.TrimExcess(2); q.Clear(); t.Clear(); return a + \",\" + q.Capacity + \"/\" + t.Capacity;")] // 6/6,2/2
    // Dequeue, Pop and Clear keep the capacity.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); var t = new Stack<int>(new[] { 1, 2, 3 }); q.Dequeue(); t.Pop(); var a = q.Capacity + \"/\" + t.Capacity; q.Clear(); t.Clear(); return a + \",\" + q.Capacity + \"/\" + t.Capacity;")] // 3/3,3/3
    public void ACapacity_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A capacity refused with its actual value, which <c>ArgumentOutOfRangeException</c> writes
    /// after <c>Environment.NewLine</c>.</summary>
    [SkippableTheory]
    [InlineData("var q = new Queue<int>(); q.EnsureCapacity(-1); return \"no\";")]
    [InlineData("var t = new Stack<int>(); t.EnsureCapacity(-1); return \"no\";")]
    [InlineData("var q = new Queue<int>(); q.TrimExcess(-1); return \"no\";")]
    [InlineData("var t = new Stack<int>(); t.TrimExcess(-1); return \"no\";")]
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); q.TrimExcess(1); return \"no\";")]
    [InlineData("var t = new Stack<int>(new[] { 1, 2 }); t.TrimExcess(1); return \"no\";")]
    public void ACapacity_IsRefusedAsDotNetRefusesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(
            Refused(statements), "an ArgumentOutOfRangeException writes its actual value after Environment.NewLine");
    }

    [SkippableTheory]
    // Find and FindLast answer the node, from the front and from the back, a null value included.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 1, 3 }); var f = l.Find(1); var g = l.FindLast(1); return (f == l.First) + \",\" + (g == l.Last!.Previous) + \",\" + (l.Find(9) == null) + \",\" + (l.FindLast(9) == null) + \",\" + f!.Next!.Value + \",\" + g!.Next!.Value;")] // True,True,True,True,2,3
    [InlineData("var l = new LinkedList<string?>(new[] { \"a\", null, \"b\", null }); return l.Find(null)!.Next!.Value + \",\" + (l.FindLast(null) == l.Last) + \",\" + (new LinkedList<int>().Find(1) == null);")] // b,True,True
    // AddAfter and AddBefore a node, a value or a node of their own, at either end too.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 3 }); var n = l.AddAfter(l.First!, 2); var m = l.AddBefore(l.First!, 0); var k = l.AddAfter(l.Last!, 4); return string.Join(\",\", l) + \"|\" + n.Value + m.Value + k.Value + \"|\" + l.Count + \"|\" + (l.First == m) + (l.Last == k) + (n.List == l) + (n.Previous!.Value) + (n.Next!.Value);")] // 0,1,2,3,4|204|5|TrueTrueTrue13
    [InlineData("var l = new LinkedList<int>(new[] { 2 }); var a = new LinkedListNode<int>(1); var b = new LinkedListNode<int>(3); var c = new LinkedListNode<int>(0); var d = new LinkedListNode<int>(4); var before = (a.List == null) + \",\" + (a.Next == null) + \",\" + a.Value; l.AddBefore(l.First!, a); l.AddAfter(l.Last!, b); l.AddFirst(c); l.AddLast(d); return before + \"|\" + string.Join(\",\", l) + \"|\" + (a.List == l) + (c.Next == a) + (d.Previous == b) + (l.First!.Previous == null) + (l.Last!.Next == null) + l.Count;")] // True,True,1|0,1,2,3,4|TrueTrueTrueTrueTrue5
    [InlineData("var l = new LinkedList<int>(); var a = new LinkedListNode<int>(1); var b = new LinkedListNode<int>(2); l.AddLast(a); l.AddFirst(b); return string.Join(\",\", l) + \"|\" + (l.First == b) + (l.Last == a) + (b.Next == a) + (a.Previous == b);")] // 2,1|TrueTrueTrueTrue
    // A node's value is settable, so a node takes an object initializer, applied after its construction.
    [InlineData("var log = \"\"; int V(int v) { log += v; return v; } var n = new LinkedListNode<int>(V(1)) { Value = V(2) }; var l = new LinkedList<int>(); l.AddLast(n); return log + \"|\" + n.Value + \"|\" + string.Join(\",\", l);")] // 12|2|2
    // A node removed, by itself, from either end, by its value or with the rest, belongs to no list.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); var n = l.First!.Next!; l.Remove(n); var s = string.Join(\",\", l) + \"|\" + (n.List == null) + (n.Next == null) + (n.Previous == null) + n.Value + \"|\" + l.Count; l.AddLast(n); return s + \"|\" + string.Join(\",\", l) + (n.List == l);")] // 1,3|TrueTrueTrue2|2|1,3,2True
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3, 4 }); var a = l.First!; var b = l.Last!; var c = l.Find(2)!; l.RemoveFirst(); l.RemoveLast(); l.Remove(2); var s = \"\" + (a.List == null) + (a.Next == null) + (b.List == null) + (b.Previous == null) + (c.List == null) + (c.Next == null); var d = l.First!; l.Clear(); return s + (d.List == null) + (d.Next == null) + (d.Previous == null) + d.Value + l.Count;")] // TrueTrueTrueTrueTrueTrueTrueTrueTrue30
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); var n = l.First!; l.Remove(n); return (l.First == null) + \",\" + (l.Last == null) + \",\" + l.Count + \",\" + string.Join(\",\", l.AddFirst(5).List!);")] // True,True,0,5
    // A node's list is the list itself.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); var n = l.Last!; n.List!.AddLast(3); n.Value = 9; return string.Join(\",\", l) + \",\" + n.List.Count;")] // 1,9,3,3
    // CopyTo writes front to back from the index it is handed.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); var a = new int[4]; l.CopyTo(a, 1); var b = new int[0]; new LinkedList<int>().CopyTo(b, 0); return string.Join(\",\", a) + \"|\" + b.Length;")] // 0,1,2,0|0
    public void ALinkedList_LinksAsDotNetLinks(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A node that is null, of another list or of none, or a new node that already belongs to a list.
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddAfter(null!, 1); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddBefore(null!, 1); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddAfter(l.First!, (LinkedListNode<int>)null!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddBefore(l.First!, (LinkedListNode<int>)null!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddFirst((LinkedListNode<int>)null!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddLast((LinkedListNode<int>)null!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.Remove((LinkedListNode<int>)null!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddAfter(new LinkedListNode<int>(2), 3); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); var o = new LinkedList<int>(new[] { 2 }); l.AddBefore(o.First!, 3); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.Remove(new LinkedListNode<int>(1)); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); var n = l.First!; l.Remove(n); l.Remove(n); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddLast(l.First!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); var o = new LinkedList<int>(new[] { 2 }); l.AddFirst(o.First!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddAfter(l.First!, l.First!); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); l.AddAfter(new LinkedListNode<int>(2), l.First!); return \"no\";")]
    // CopyTo's array and room.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); l.CopyTo(null!, 0); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); l.CopyTo(new int[3], 2); return \"no\";")]
    public void ALinkedList_RefusesAsDotNetRefuses(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    /// <summary>A linked list's index refused with its actual value, after <c>Environment.NewLine</c>.</summary>
    [SkippableTheory]
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); l.CopyTo(new int[3], -1); return \"no\";")]
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2 }); l.CopyTo(new int[3], 4); return \"no\";")]
    public void ALinkedListIndex_IsRefusedAsDotNetRefusesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(
            Refused(statements), "an ArgumentOutOfRangeException writes its actual value after Environment.NewLine");
    }

    [SkippableTheory]
    // A take is a change, and a look is not; a capacity changed is one, and one left as it is is not.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.TryPeek(out _); }")]                            // 123
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.TryDequeue(out _); }")]                         // 1|Collection was modified; enumeration operation may not execute.
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.TryPeek(out _); }")]                            // 321
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.TryPop(out _); }")]                             // 3|Collection was modified after the enumerator was instantiated.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.EnsureCapacity(1); q.TrimExcess(); q.TrimExcess(q.Capacity); }")] // 123
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.EnsureCapacity(100); }")]                       // 1|the same
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.TrimExcess(9); }")]                             // 1|the same
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.EnsureCapacity(1); t.TrimExcess(); t.TrimExcess(t.Capacity); }")] // 321
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.EnsureCapacity(100); }")]                       // 3|the same
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.TrimExcess(9); }")]                             // 3|the same
    [InlineData("var q = new Queue<int>(); for (var i = 0; i < 5; i++) q.Enqueue(i); foreach (var x in q) { s += x; if (x == 4) q.TrimExcess(); }")] // 01234|the same
    [InlineData("var t = new Stack<int>(); for (var i = 0; i < 5; i++) t.Push(i); foreach (var x in t) { s += x; if (x == 4) t.TrimExcess(); }")]   // 43210: a stack reallocated is no change
    // A node added or removed is a change, and a node found is not.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; l.Find(x); l.FindLast(x); }")]               // 123
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 1) l.AddAfter(l.Last!, 9); }")]     // 1|Collection was modified after the enumerator was instantiated.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 2) l.Remove(l.First!); }")]         // 12|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 3) l.AddLast(new LinkedListNode<int>(4)); }")] // 123|the same
    // A look into a sorted set is no change.
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.TryGetValue(x, out _); }")]                        // 135
    public void AWalk_EndsWhereDotNetsEnds(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Walked(statements));
    }
}
