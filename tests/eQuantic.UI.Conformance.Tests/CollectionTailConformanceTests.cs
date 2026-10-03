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
    // A collection initializer is one Add per element: it was dropped, and the set began empty (#516).
    [InlineData("var s = new SortedSet<int> { 3, 1, 2, 1 }; var r = \"\"; foreach (var x in s) r += x; return r + \"|\" + s.Count;")] // "123|3"
    [InlineData("var s = new SortedSet<int>(new[] { 9 }) { 4 }; return s.Min * 10 + s.Max;")]                   // 49
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

    /// <summary>
    /// A sorted collection orders as its element type's <c>Comparer&lt;T&gt;.Default</c> does, which
    /// the browser can only learn from the type: every one ordered by <c>&lt;</c>, so strings did not
    /// follow the culture (<c>B</c> came before <c>a</c>), decimals and dates compared their text
    /// (<c>10</c> before <c>9</c>, <c>1.0</c> kept beside <c>1.00</c>), and a NaN equalled every
    /// number. An ordinal comparer asks for the code-unit order, and reached the factory as the
    /// elements to copy, which threw.
    /// </summary>
    [SkippableTheory]
    // Each written out with a loop, which reads the collection as C# enumerates it.
    [InlineData("var s = new SortedSet<string> { \"b\", \"B\", \"a\", \"A\", \"_x\" }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r;")]   // ",_x,a,A,b,B"
    [InlineData("var s = new SortedSet<string>(new[] { \"b\", \"B\", \"a\" }); s.Add(\"A\"); var r = \"\"; foreach (var x in s) r += \",\" + x; return r + \"|\" + s.Min;")] // ",a,A,b,B|a"
    [InlineData("var s = new SortedSet<string>(StringComparer.Ordinal) { \"b\", \"B\", \"a\", \"A\" }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r;")] // ",A,B,a,b"
    [InlineData("var s = new SortedSet<string>(new[] { \"b\", \"a\" }, Comparer<string>.Default) { \"B\" }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r;")] // ",a,b,B"
    [InlineData("SortedSet<string> s = [\"b\", \"B\", \"a\"]; var r = \"\"; foreach (var x in s) r += \",\" + x; return r;")]   // ",a,b,B"
    [InlineData("var s = new SortedSet<decimal> { 10m, 9m, 1.0m, 1.00m }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r + \"|\" + s.Count;")] // ",1.0,9,10|3"
    [InlineData("var s = new SortedSet<double> { 2, double.NaN, 1, double.NaN }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r + \"|\" + s.Count;")] // ",NaN,1,2|3"
    [InlineData("var s = new SortedSet<DateTime> { new DateTime(2026, 1, 2), new DateTime(2025, 12, 31), new DateTime(2026, 1, 2) }; return s.Count + \"|\" + s.Min.Year + \"|\" + s.Max.Day;")] // "2|2025|2"
    [InlineData("var s = new SortedSet<char> { 'b', 'B', 'a' }; var r = \"\"; foreach (var x in s) r += \",\" + x; return r;")]  // ",B,a,b"
    [InlineData("var d = new SortedDictionary<string, int> { [\"b\"] = 1, [\"B\"] = 2, [\"a\"] = 3 }; var r = \"\"; foreach (var k in d.Keys) r += \",\" + k; return r;")] // ",a,b,B"
    [InlineData("var d = new SortedList<decimal, string> { [10m] = \"x\", [9m] = \"y\", [1.0m] = \"z\" }; var r = \"\"; foreach (var v in d.Values) r += v; return r;")] // "zyx"
    public void ASortedCollection_OrdersAsItsElementTypeDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// An enum orders by its value, as .NET's comparer orders it, where its members cross as names:
    /// ordered by <c>&lt;</c>, <c>Alpha</c> came before <c>Zeta</c> whatever their values said, and
    /// <c>Max</c> refused it. Written out by interpolation: <c>"," + x</c> writes undefined (#535).
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = new SortedSet<Rank> { Rank.Mid, Rank.Alpha, Rank.Zeta, Rank.Alpha }; var r = \"\"; foreach (var x in s) r += $\",{x}\"; return r + \"|\" + s.Count;")] // ",Zeta,Alpha,Mid|3"
    [InlineData("var d = new SortedDictionary<Rank, int> { [Rank.Mid] = 1, [Rank.Zeta] = 2 }; var r = \"\"; foreach (var k in d.Keys) r += $\",{k}\"; return r;")] // ",Zeta,Mid"
    [InlineData("return new[] { Rank.Zeta, Rank.Mid, Rank.Alpha }.Max().ToString();")]          // "Mid"
    [InlineData("return new[] { Rank.Alpha, Rank.Zeta }.Min().ToString();")]                    // "Zeta"
    [InlineData("Rank? none = null; return new Rank?[] { Rank.Alpha, none, Rank.Zeta }.Max().Value.ToString();")] // "Alpha"
    [InlineData("var s = new SortedSet<Access> { Access.Write, Access.Read }; var r = 0; foreach (var x in s) r = r * 10 + (int)x; return r;")] // 12
    public void AnEnum_OrdersByItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements,
            "public enum Rank { Zeta, Alpha, Mid = 5 }\n[System.Flags] public enum Access { None = 0, Read = 1, Write = 2 }");
    }

    /// <summary>A queue and a stack find a member as <c>EqualityComparer&lt;T&gt;.Default</c> does: a
    /// decimal and a date by their value, where they compared references.</summary>
    [SkippableTheory]
    [InlineData("var q = new Queue<decimal>(); q.Enqueue(1m); return q.Contains(1.00m);")]                                  // true
    [InlineData("var s = new Stack<DateTime>(); s.Push(new DateTime(2026, 1, 1)); return s.Contains(new DateTime(2026, 1, 1));")] // true
    [InlineData("var q = new Queue<int>(); q.Enqueue(1); return q.Contains(2);")]                                           // false
    public void AQueueOrAStack_FindsAMemberByItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableFact]
    public void ASortedSetOfAComparableTypeOfTheAppsOwn_OrdersByItsCompareTo()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var s = new SortedSet<Release> { new(2, \"b\"), new(10, \"c\"), new(1, \"a\"), new(2, \"d\") }; var r = \"\"; foreach (var x in s) r += x.Name; return r + \"|\" + s.Count;",
            "public sealed record Release(int Number, string Name) : IComparable<Release> { public int CompareTo(Release? other) => Number.CompareTo(other!.Number); }");
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
