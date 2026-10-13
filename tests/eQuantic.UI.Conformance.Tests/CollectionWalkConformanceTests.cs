using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>foreach</c> over a <c>Queue&lt;T&gt;</c>, a <c>Stack&lt;T&gt;</c>, a <c>LinkedList&lt;T&gt;</c> or a
/// <c>SortedSet&lt;T&gt;</c> that the collection changes under (#765). .NET's enumerator compares the
/// collection's version at every step, the one after the last element included, and refuses a change with
/// an InvalidOperationException in its assembly's words. The twins walked on: a queue and a stack walked a
/// copy, and a linked list and a sorted set walked themselves, so <c>AddLast</c> or <c>Add</c> inside the
/// walk never let it end. Each case guards its own walk so that both sides end and answer what they saw.
/// <para>
/// What counts as a change is .NET's: every <c>Add</c> and <c>Remove</c> of a sorted set that holds
/// anything, one that finds nothing to do included, and a set operation by the path .NET takes through it,
/// which asks <c>Contains</c> before adding or removing an element of a sequence and does not ask it of a
/// sorted set's elements. A read is no change, and neither is a node's value written in place.
/// </para>
/// <para>
/// A sorted set's <c>Reverse()</c>, the same walk from its largest element, was a TypeError: the set had
/// no such member in the browser.
/// </para>
/// </summary>
public class CollectionWalkConformanceTests
{
    /// <summary>The statements, which add what they walk to <c>s</c>, and then what the walk was refused
    /// with, if anything.</summary>
    private static string Walked(string statements) =>
        "var s = \"\"; try { " + statements + " return s; } "
        + "catch (InvalidOperationException e) { return s + \"|\" + e.Message; }";

    [SkippableTheory]
    // Queue: a change ends the walk at the next step, and after the last element too.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; if (q.Count > 9) break; q.Enqueue(x); }")]  // 1|Collection was modified; enumeration operation may not execute.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; q.Dequeue(); }")]                           // 1|the same
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) { s += x; if (x == 3) q.Clear(); }")]                 // 123|the same
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); foreach (var x in q) s += x + \":\" + q.Peek() + q.Contains(x) + q.Count + q.ToArray().Length + \",\";")] // read: no change
    // Stack: from the top, the same.
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; if (t.Count > 9) break; t.Push(x); }")]     // 3|Collection was modified after the enumerator was instantiated.
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; t.Pop(); }")]                               // 3|the same
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) { s += x; if (x == 1) t.Clear(); }")]                 // 321|the same
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); foreach (var x in t) s += x + \":\" + t.Peek() + t.Contains(x) + t.Count + \",\";")] // read: no change
    // LinkedList: every node added or removed; a value not found removes nothing, and a value written in
    // place is read as it is.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (l.Count > 9) break; l.AddLast(x); }")] // 1|Collection was modified after the enumerator was instantiated.
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (l.Count > 9) break; l.AddFirst(x); }")] // 1|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 1) l.RemoveLast(); }")]       // 1|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 2) l.RemoveFirst(); }")]      // 12|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 1) l.Remove(3); }")]          // 1|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; l.Remove(9); }")]                     // 123: nothing removed
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 3) l.Clear(); }")]            // 123|the same
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); foreach (var x in l) { s += x; if (x == 1) l.Last!.Value = 9; }")]   // 129
    // SortedSet: an Add or a Remove, one that changes nothing included, and the walk backwards too.
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (t.Count > 9) break; t.Add(x + 10); }")]  // 1|Collection was modified after the enumerator was instantiated.
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.Add(x); }")]                                // 1|the same: already there
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.Remove(5); }")]                 // 1|the same
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.Remove(9); }")]                             // 1|the same: not there
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 5) t.Clear(); }")]                   // 135|the same
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) s += x + \":\" + t.Min + t.Max + t.Contains(x) + t.Count + \",\";")] // read: no change
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t.Reverse()) { s += x; if (t.Count > 9) break; t.Add(x + 10); }")] // 5|the same
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; var r = t.Reverse(); t.Add(7); s += string.Join(\",\", r) + \";\" + string.Join(\",\", r) + \";\" + r.Count();")] // 7,5,3,1;7,5,3,1;4: read when walked, each time
    // SortedSet's set operations: a sequence's elements asked with Contains first, a sorted set's not.
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.UnionWith(new[] { x }); }")]               // 135: nothing to add
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.UnionWith(new[] { 9 }); }")]  // 1|the same
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.UnionWith(t); }")]             // 1|the same: merged with itself
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.ExceptWith(new[] { 2 }); }")]              // 135: nothing to remove
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.ExceptWith(new SortedSet<int> { 2 }); }")] // 1|the same: removed without asking
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.ExceptWith(new SortedSet<int> { 9 }); }")] // 135: past this set's range
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.IntersectWith(new SortedSet<int> { 1, 3, 5 }); }")] // 1|the same: rebuilt
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.IntersectWith(new[] { 1, 3, 5 }); }")] // 1|the same: cleared and refilled
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.IntersectWith(t); }")]                     // 135: itself
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.SymmetricExceptWith(new int[0]); }")]      // 135: nothing to toggle
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; t.RemoveWhere(v => v > 9); }")]              // 135: nothing matched
    [InlineData("var t = new SortedSet<int> { 1, 3, 5 }; foreach (var x in t) { s += x; if (x == 1) t.RemoveWhere(v => v == 5); }")] // 1|the same
    public void AWalkOverACollectionThatChanges_EndsAsDotNetEndsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Walked(statements));
    }
}
