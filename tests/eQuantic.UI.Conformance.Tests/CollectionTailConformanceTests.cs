using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for the remaining collection types — <c>LinkedList&lt;T&gt;</c>, the sorted family
/// (<c>SortedSet</c>/<c>SortedDictionary</c>/<c>SortedList</c>) and the <c>ILookup[key]</c> indexer —
/// each routed to a faithful <c>$eq.collections.*</c> runtime type.
/// </summary>
public class CollectionTailConformanceTests
{
    [SkippableTheory]
    [InlineData("var l = new LinkedList<int>(); l.AddLast(1); l.AddLast(2); l.AddFirst(0); return l.Count;")] // 3
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); return l.First.Value + l.Last.Value;")]            // 4
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); return l.First.Next.Value;")]                       // 2
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); l.RemoveFirst(); l.RemoveLast(); return l.First.Value;")] // 2
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); l.Remove(2); return l.Count;")]                     // 2
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); var s = 0; foreach (var x in l) s += x; return s;")] // 6
    public void LinkedList_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); return l.Contains(2);")]   // true
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); return l.Contains(9);")]   // false
    [InlineData("var l = new LinkedList<int>(new[]{1,2,3}); return l.Remove(9);")]     // false
    public void LinkedList_Bool_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // SortedSet — enumerates in sorted order, dedups, Min/Max.
    [InlineData("var s = new SortedSet<int>(); s.Add(3); s.Add(1); s.Add(2); return s.Min * 100 + s.Max;")]   // 103
    [InlineData("var s = new SortedSet<int>(new[]{5,5,1}); return s.Count;")]                                  // 2
    [InlineData("var s = new SortedSet<int>(new[]{4,2,8,1}); var r = \"\"; foreach (var x in s) r += x; return r;")] // "1248"
    [InlineData("var s = new SortedSet<int>(new[]{1,2,3}); s.Remove(2); return s.Count;")]                     // 2
    // SortedDictionary / SortedList — Keys/Values/iteration in key order.
    [InlineData("var d = new SortedDictionary<int,string>(); d[3]=\"c\"; d[1]=\"a\"; d[2]=\"b\"; var r = \"\"; foreach (var k in d.Keys) r += k; return r;")] // "123"
    [InlineData("var d = new SortedDictionary<int,string>(); d[2]=\"b\"; d[1]=\"a\"; return d[1];")]            // "a"
    [InlineData("var d = new SortedDictionary<int,int>(); d[1]=10; d[2]=20; return d.Count;")]                  // 2
    [InlineData("var d = new SortedDictionary<string,int>{ {\"b\",2}, {\"a\",1} }; var r = \"\"; foreach (var k in d.Keys) r += k; return r;")] // "ab"
    [InlineData("var d = new SortedList<int,string>(); d[3]=\"c\"; d[1]=\"a\"; var r = \"\"; foreach (var k in d.Keys) r += k; return r;")]      // "13"
    public void SortedCollections_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("var d = new SortedDictionary<int,int>(); d[1]=10; return d.ContainsKey(1);")]   // true
    [InlineData("var d = new SortedDictionary<int,int>(); d[1]=10; return d.ContainsKey(9);")]   // false
    public void SortedDictionary_Bool_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // ILookup[key] — the group for a key, or an empty sequence for an absent key (never throws).
    [InlineData("var lk = new[]{1,2,3,4}.ToLookup(x => x % 2); return lk[0].Count();")]   // evens → 2
    [InlineData("var lk = new[]{1,2,3,4}.ToLookup(x => x % 2); return lk[1].Count();")]   // odds → 2
    [InlineData("var lk = new[]{1,2,3}.ToLookup(x => x % 2); return lk[9].Count();")]     // absent → 0
    [InlineData("var lk = new[]{1,2,3,4}.ToLookup(x => x % 2); var s = 0; foreach (var x in lk[0]) s += x; return s;")] // 2+4 = 6
    public void LookupIndexer_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
