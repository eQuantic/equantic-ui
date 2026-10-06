using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>List&lt;T&gt;.Sort</c>, <c>Array.Sort</c> and <c>BinarySearch</c> answer as .NET's do, on both sides
/// (#488). <c>Sort()</c> was the array's <c>sort()</c>, which compares the elements' TEXT and is stable:
/// <c>10, 9, 1</c> sorted to <c>1, 10, 9</c>, and a sort by a key left equal elements in their order where
/// .NET's introspective sort, which is not stable, moves them. A comparer had no form
/// (<c>StringComparer is not defined</c>), and <c>BinarySearch</c> was a <c>findIndex</c> that answered -1
/// for every miss, where .NET answers the complement of the insertion point.
/// <para>
/// The runtime's sort is .NET's, step for step, both of its helpers: a case that TRACES every comparison
/// the sort makes, on an input built to exhaust the quicksort's depth so the heap sort runs, answers the
/// same trace on both sides.
/// </para>
/// </summary>
public class ListSortConformanceTests
{
    private const string Prelude = """
        public record P(int X, int Y);
        public enum E { Low = 1, Mid = 5, High = 10 }
        public record K(int V) : IComparable<K>
        {
            public static int Calls;
            public static int Trace;
            public int CompareTo(K o) { Calls++; Trace = (Trace * 31 + V * 64 + o.V) % 1000003; return V.CompareTo(o.V); }
        }
        """;

    /// <summary>An input on which the quicksort runs out of depth at 64 elements (McIlroy's adversary,
    /// played against the runtime's sort), so a sort of it goes through the heap sort.</summary>
    private const string Killer = "0,59,5,58,7,57,9,56,11,55,13,54,15,53,17,52,19,51,21,50,23,49,25,48,27,47,28,29,63,61,62,3,2,4,6,8,10,12,14,16,18,20,22,24,26,46,45,44,43,42,41,40,39,38,37,36,35,34,33,32,31,30,60,1";

    [SkippableTheory]
    // By value, not by text (#488): "1,9,10".
    [InlineData("var l = new List<int> { 10, 9, 1 }; l.Sort(); return string.Join(\",\", l);")]
    // A comparer crosses: StringComparer's six, Comparer<T>.Default and Comparer<T>.Create.
    [InlineData("var l = new List<string> { \"b\", \"a\", \"B\", \"A\" }; l.Sort(StringComparer.Ordinal); return string.Join(\",\", l);")]            // A,B,a,b
    [InlineData("var l = new List<string> { \"b\", \"a\", \"B\", \"A\" }; l.Sort(StringComparer.OrdinalIgnoreCase); return string.Join(\",\", l);")]  // a,A,b,B
    [InlineData("var l = new List<string> { \"b\", \"a\", \"B\", \"A\" }; l.Sort(StringComparer.CurrentCulture); return string.Join(\",\", l);")]     // a,A,b,B
    [InlineData("var l = new List<int> { 5, 4 }; l.Sort(Comparer<int>.Default); return string.Join(\",\", l);")]                                   // 4,5
    [InlineData("var l = new List<int> { 3, 1, 2 }; l.Sort(Comparer<int>.Create((a, b) => b.CompareTo(a))); return string.Join(\",\", l);")]       // 3,2,1
    // The default comparer of each type: an enum by its value, a bool, a char by its code unit, a
    // decimal (equal values keep where the sort put them), a string in the current culture, a null first.
    [InlineData("var l = new List<E> { E.High, E.Low, E.Mid }; l.Sort(); return string.Join(\",\", l);")]                                           // Low,Mid,High
    [InlineData("var l = new List<bool> { true, false, true }; l.Sort(); return string.Join(\",\", l);")]                                            // False,True,True
    [InlineData("var l = new List<char> { 'b', 'A', 'a' }; l.Sort(); return string.Join(\",\", l);")]                                                // A,a,b
    [InlineData("var l = new List<decimal> { 1.00m, 1.0m, 0.5m, 1m }; l.Sort(); return string.Join(\",\", l);")]                                     // 0.5,1.00,1.0,1
    [InlineData("var l = new List<string> { \"b\", \"a\", \"B\", \"A\", null }; l.Sort(); return string.Join(\",\", l.Select(x => x ?? \"null\"));")] // null,a,A,b,B
    [InlineData("var l = new List<string> { \"b\", \"a\", \"B\", \"A\", \"\\u00e1\", \"a\\u00ad\" }; l.Sort(); return string.Join(\"|\", l);")]
    [InlineData("var l = Enumerable.Range(0, 16).Reverse().ToList(); l.Sort(); return string.Join(\",\", l);")]
    // A double's NaNs go first, before the sort, which decides where a -0 lands beside a 0; a float's
    // too. A nullable double sorts by a comparer, with no such pass: null, then NaN.
    [InlineData("var l = new List<double> { 0.0, -0.0, double.NaN, 1, -0.0, 0.0 }; l.Sort(); return string.Join(\" \", l.Select(d => d.ToString() + (d == 0 ? (1 / d > 0 ? \"+\" : \"-\") : \"\")));")] // NaN -0- 0+ -0- 0+ 1
    [InlineData("var l = new List<double> { 3, double.NaN, 1, double.NaN, 2 }; l.Sort(); return string.Join(\",\", l);")]                            // NaN,NaN,1,2,3
    [InlineData("var l = new List<float> { 3, float.NaN, 1 }; l.Sort(); return string.Join(\",\", l);")]                                             // NaN,1,3
    [InlineData("var l = new List<double?> { 3, null, double.NaN, 1 }; l.Sort(); return string.Join(\",\", l.Select(x => x?.ToString() ?? \"null\"));")] // null,NaN,1,3
    [InlineData("var seed = 99; var l = new List<double>(); for (var i = 0; i < 300; i++) { seed = (seed * 75 + 74) % 65537; l.Add(seed % 5 == 0 ? double.NaN : seed % 5 == 1 ? -0.0 : seed % 5 == 2 ? 0.0 : seed % 11); } l.Sort(); var s = \"\"; foreach (var d in l) s += double.IsNaN(d) ? \"n\" : d == 0 ? (1 / d > 0 ? \"+\" : \"-\") : d.ToString(); return s;")]
    // Unstable as .NET's is: equal elements land where its introspective sort puts them.
    [InlineData("var l = Enumerable.Range(0, 20).Select(i => new P(i % 3, i)).ToList(); l.Sort((a, b) => a.X.CompareTo(b.X)); return string.Join(\" \", l.Select(p => p.Y));")] // 0 15 12 18 6 9 3 4 7 10 13 1 16 19 8 11 2 14 17 5
    [InlineData("var l = Enumerable.Range(0, 5).Select(i => new P(i % 2, i)).ToList(); l.Sort((a, b) => a.X.CompareTo(b.X)); return string.Join(\" \", l.Select(p => p.Y));")]  // 0 2 4 1 3
    [InlineData("var l = Enumerable.Range(0, 40).Select(i => new P((i * 7) % 5, i)).ToList(); l.Sort((a, b) => a.X - b.X); return string.Join(\" \", l.Select(p => p.Y));")]
    [InlineData("var seed = 12345; var l = new List<P>(); for (var i = 0; i < 500; i++) { seed = (seed * 75 + 74) % 65537; l.Add(new P(seed % 7, i)); } l.Sort((a, b) => a.X.CompareTo(b.X)); var h = 0; foreach (var p in l) h = (h * 31 + p.Y) % 1000003; return h;")]
    [InlineData("var seed = 7; var l = new List<P>(); for (var i = 0; i < 2000; i++) { seed = (seed * 75 + 74) % 65537; l.Add(new P(seed % 3, i)); } l.Sort((a, b) => a.X - b.X); var h = 0; foreach (var p in l) h = (h * 31 + p.Y) % 1000003; return h;")]
    [InlineData("var seed = 3; var l = new List<P>(); for (var i = 0; i < 3000; i++) { seed = (seed * 75 + 74) % 65537; l.Add(new P(seed % 2, i)); } l.Sort(Comparer<P>.Create((a, b) => a.X.CompareTo(b.X))); var h = 0; foreach (var p in l) h = (h * 31 + p.Y) % 1000003; return h;")]
    [InlineData("var l = new List<P>(); for (var i = 0; i < 30; i++) l.Add(new P(i % 4, i)); var calls = 0; l.Sort(Comparer<P>.Create((a, b) => { calls++; return a.X.CompareTo(b.X); })); return calls + \":\" + string.Join(\" \", l.Select(p => p.Y));")]
    [InlineData("var l = new List<int>(); for (var i = 0; i < 64; i++) l.Add(i % 2 == 0 ? i : 64 - i); var calls = 0; l.Sort((x, y) => { calls++; return x.CompareTo(y); }); return calls + \":\" + string.Join(\",\", l.Take(5));")]
    // A range of the list, and what an inconsistent comparison leaves where it reads nothing past the
    // span (three elements, and every answer "greater").
    [InlineData("var l = new List<int> { 5, 4, 3, 2, 1 }; l.Sort(1, 3, null); return string.Join(\",\", l);")]                                       // 5,2,3,4,1
    [InlineData("var l = Enumerable.Range(0, 3).ToList(); l.Sort((a, b) => -1); return string.Join(\",\", l);")]                                    // 0,1,2
    [InlineData("var l = Enumerable.Range(0, 17).ToList(); l.Sort((a, b) => 1); return string.Join(\",\", l);")]                                    // 16,14,13,…
    // A sort of nothing to compare never asks the comparer, nor the default comparer of a type that
    // cannot compare (a record that is not IComparable): one element is sorted already.
    [InlineData("var l = new List<int>(); l.Sort((a, b) => throw new FormatException()); return l.Count;")]
    [InlineData("var l = new List<P> { new P(1, 1) }; l.Sort(); return l.Count;")]
    [InlineData("var l = new List<P>(); return l.BinarySearch(new P(1, 1));")]
    // Array.Sort: the same sort, by the same comparers.
    [InlineData("var a = new[] { 3, 1, 2 }; Array.Sort(a); return string.Join(\",\", a);")]
    [InlineData("var a = new[] { 3, 1, 2 }; Array.Sort(a, (x, y) => y - x); return string.Join(\",\", a);")]
    [InlineData("var a = new[] { \"b\", \"a\", \"C\" }; Array.Sort(a, StringComparer.Ordinal); return string.Join(\",\", a);")]
    [InlineData("var a = new[] { 5, 4, 3, 2, 1 }; Array.Sort(a, 1, 3); return string.Join(\",\", a);")]
    [InlineData("var seed = 5; var a = new int[1000]; for (var i = 0; i < a.Length; i++) { seed = (seed * 75 + 74) % 65537; a[i] = seed % 100; } Array.Sort(a); var ok = true; for (var i = 1; i < a.Length; i++) ok &= a[i - 1] <= a[i]; return ok + \":\" + a[0] + \":\" + a[999];")]
    // Step for step through the heap sort, by both helpers: every comparison a Comparison makes, and
    // every CompareTo the default comparer of an IComparable<T> makes, the same on both sides, ties included.
    [InlineData("var a = new[] { " + Killer + " }; var h = 0; var calls = 0; Array.Sort(a, (x, y) => { calls++; h = (h * 31 + x * 64 + y) % 1000003; return x.CompareTo(y); }); return calls + \":\" + h + \":\" + a[0] + \":\" + a[63];")] // 984:470213:0:63
    [InlineData("var a = new[] { " + Killer + " }.Select(v => new K(v)).ToList(); K.Calls = 0; K.Trace = 0; a.Sort(); return K.Calls + \":\" + K.Trace + \":\" + a[0].V + \":\" + a[63].V;")]                                          // 984:272133:0:63
    [InlineData("var a = new[] { " + Killer + " }.Select(v => new K(v / 2)).ToList(); K.Calls = 0; K.Trace = 0; a.Sort(); return K.Calls + \":\" + K.Trace;")]                                                                        // 987:3269
    public void Sort_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // What a comparison throws is wrapped as .NET wraps it; an inconsistent one that reads past the
    // span is .NET's ArgumentException naming the comparer.
    [InlineData("try { var l = new List<int> { 3, 1, 2 }; l.Sort((a, b) => throw new FormatException(\"boom\")); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = Enumerable.Range(0, 20).ToList(); l.Sort((a, b) => -1); return string.Join(\",\", l); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<K> { new K(2), new K(1) }; l.Sort(Comparer<K>.Create((a, b) => throw new FormatException())); return \"no\"; } catch (Exception e) { return e.Message; }")]
    // Two elements of a type .NET's default comparer cannot order: it throws once it compares them.
    [InlineData("try { var l = new List<P> { new P(1, 1), new P(0, 0) }; l.Sort(); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<P> { new P(1, 1), new P(2, 2) }; return l.BinarySearch(new P(1, 1)); } catch (Exception e) { return e.Message; }")]
    // The range and the comparison are checked as .NET checks them.
    [InlineData("try { var l = new List<int> { 5, 4, 3, 2, 1 }; l.Sort(3, 3, null); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 5, 4, 3, 2, 1 }; l.Sort(-1, 3, null); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 5, 4, 3, 2, 1 }; l.Sort(0, -1, null); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 5, 4 }; l.Sort((Comparison<int>)null); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new[] { 5, 4, 3 }; Array.Sort(a, 2, 2); return \"no\"; } catch (Exception e) { return e.Message; }")]
    public void Sort_FailsAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // The complement of the insertion point for a miss (#488): "1,-3".
    [InlineData("var l = new List<int> { 1, 3, 5 }; return l.BinarySearch(3) + \",\" + l.BinarySearch(4);")]
    [InlineData("var l = new List<int> { 1, 3, 5 }; return l.BinarySearch(0) + \",\" + l.BinarySearch(9);")]                                       // -1,-4
    // Among equal elements, the one .NET's midpoint meets first.
    [InlineData("var l = new List<int> { 1, 2, 2, 2, 2, 2, 3 }; return l.BinarySearch(2);")]                                                       // 3
    [InlineData("var l = new List<int> { 1, 3, 5, 7, 9 }; return l.BinarySearch(1, 3, 5, null) + \",\" + l.BinarySearch(1, 3, 1, null) + \",\" + l.BinarySearch(1, 3, 9, null);")] // 2,-2,-5
    // By the type's order (a string in the current culture, a NaN first, a null first) or the comparer's.
    [InlineData("var l = new List<string> { \"a\", \"B\", \"c\" }; return l.BinarySearch(\"b\") + \",\" + l.BinarySearch(\"B\", StringComparer.Ordinal);")] // -2,1
    [InlineData("var l = new List<double> { double.NaN, 1, 2 }; return l.BinarySearch(double.NaN) + \",\" + l.BinarySearch(2);")]                // 0,2
    [InlineData("var l = new List<string> { null, \"a\", \"b\" }; return l.BinarySearch(null) + \",\" + l.BinarySearch(\"b\");")]                    // 0,2
    [InlineData("var l = new List<int> { 3, 1, 2 }; return l.BinarySearch(2, Comparer<int>.Create((a, b) => b.CompareTo(a)));")]
    [InlineData("try { var l = new List<int> { 1, 3 }; return l.BinarySearch(1, 3, 5, null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 1, 3 }; return l.BinarySearch(-1, 1, 5, null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 1, 3 }; return l.BinarySearch(0, -1, 5, null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 1, 3 }; return l.BinarySearch(5, Comparer<int>.Create((a, b) => throw new FormatException())); } catch (Exception e) { return e.Message; }")]
    public void BinarySearch_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
