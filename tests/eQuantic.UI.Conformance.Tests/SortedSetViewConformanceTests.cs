using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>SortedSet&lt;T&gt;.GetViewBetween</c> answers the view .NET 10 answers (#865): a sorted set over the
/// range of the set it was taken from, live both ways, which refuses an element outside its range. It
/// compiled and was a TypeError in the browser, and since #876 it failed the build with EQ2004.
/// <para>
/// A view takes .NET's paths through the set operations, which differ from a set's: it never merges, so a
/// <c>UnionWith</c> adds element by element and refuses the first one outside the range. And its
/// <c>ExceptWith</c> asks the count the view last counted, which only reading <c>Count</c> takes again:
/// a view whose count was never read, or was read while it was empty, removes nothing (measured).
/// </para>
/// </summary>
public class SortedSetViewConformanceTests
{
    private const string Set = "var t = new SortedSet<int> { 1, 3, 5, 7 }; ";
    private const string View = Set + "var v = t.GetViewBetween(2, 6); ";

    [SkippableTheory]
    // The view, its count and its ends; an empty one's ends are the element type's default.
    [InlineData(View + "return string.Join(\",\", v) + \"|\" + v.Count + v.Min + v.Max;")]                                     // "3,5|235"
    [InlineData(Set + "var v = t.GetViewBetween(8, 9); return v.Count + \"|\" + v.Min + \"|\" + v.Max;")]                     // "0|0|0"
    [InlineData(Set + "return string.Join(\",\", t.GetViewBetween(3, 3));")]                                                   // "3"
    [InlineData("var t = new SortedSet<string> { \"a\", \"b\", \"c\", \"d\" }; return string.Join(\",\", t.GetViewBetween(\"b\", \"c\"));")] // "b,c"
    // Live both ways: the set's changes show in the view, the view's in the set.
    [InlineData(View + "t.Add(4); t.Add(9); return string.Join(\",\", v) + \"|\" + v.Count + v.Min + v.Max;")]                 // "3,4,5|335"
    [InlineData(View + "var m = v.Max; t.Add(6); t.Remove(3); return m + \"|\" + v.Min + v.Max + v.Count;")]                   // "5|562"
    [InlineData("var t = new SortedSet<int>(); var v = t.GetViewBetween(1, 5); t.Add(3); return string.Join(\",\", v) + \"|\" + v.Count;")] // "3|1"
    [InlineData(View + "var a = v.Add(4); var b = v.Add(3); return a + \",\" + b + \"|\" + string.Join(\",\", t) + \"|\" + v.Count + t.Count;")] // "True,False|1,3,4,5,7|35"
    [InlineData(View + "var a = v.Remove(3); var b = v.Remove(7); var c = v.Remove(4); return a + \",\" + b + \",\" + c + \"|\" + string.Join(\",\", t);")] // "True,False,False|1,5,7"
    [InlineData(View + "return v.Contains(3) + \",\" + v.Contains(7) + \",\" + v.Contains(4);")]                                // "True,False,False"
    [InlineData(View + "var a = v.TryGetValue(3, out var x); var b = v.TryGetValue(7, out var y); return a + \",\" + x + \",\" + b + \",\" + y;")] // "True,3,False,0"
    [InlineData(View + "v.Clear(); return string.Join(\",\", t) + \"|\" + v.Count;")]                                           // "1,7|0"
    [InlineData(View + "t.Clear(); return string.Join(\",\", v) + \"|\" + v.Count;")]                                           // "|0"
    // An element outside the range is refused when added, and is simply not there otherwise.
    [InlineData(View + "try { v.Add(9); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message + \"|\" + string.Join(\",\", t); }")] // "Specified argument was out of the range of valid values. (Parameter 'item')|1,3,5,7"
    [InlineData(View + "try { v.Add(0); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]          // the same
    // Bounds out of order, and a view of a view outside its range.
    [InlineData(Set + "try { t.GetViewBetween(6, 2); return \"no\"; } catch (ArgumentOutOfRangeException) { return \"range\"; } catch (ArgumentException e) { return e.Message; }")] // "Must be less than or equal to upperValue. (Parameter 'lowerValue')"
    [InlineData(View + "return string.Join(\",\", v.GetViewBetween(3, 5)) + \"|\" + string.Join(\",\", v.GetViewBetween(2, 6));")] // "3,5|3,5"
    [InlineData(View + "try { v.GetViewBetween(1, 5); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "… (Parameter 'lowerValue')"
    [InlineData(View + "try { v.GetViewBetween(3, 9); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "… (Parameter 'upperValue')"
    [InlineData(View + "try { v.GetViewBetween(5, 3); return \"no\"; } catch (ArgumentOutOfRangeException) { return \"range\"; } catch (ArgumentException e) { return e.Message; }")] // the same, within the range
    [InlineData(View + "var w = v.GetViewBetween(3, 5); t.Add(4); return string.Join(\",\", w);")]                              // "3,4,5"
    // The set's order decides which bound is the lower: the code units', where "B" comes before "a".
    [InlineData("var t = new SortedSet<string>(StringComparer.Ordinal) { \"a\", \"B\", \"c\" }; return string.Join(\",\", t.GetViewBetween(\"B\", \"a\"));")] // "B,a"
    // Read as a sequence: backwards, copied, through LINQ, into a set, and written as JSON.
    [InlineData(View + "return string.Join(\",\", v.Reverse()) + \"|\" + string.Join(\",\", v.ToArray());")]                      // "5,3|3,5"
    [InlineData(View + "var a = new int[4]; v.CopyTo(a); return string.Join(\",\", a);")]                                        // "3,5,0,0"
    [InlineData(View + "return v.Count() + \"|\" + v.First() + \"|\" + v.Sum() + \"|\" + v.Where(x => x > 3).Count();")]       // "2|3|8|1"
    [InlineData(View + "return string.Join(\",\", new SortedSet<int>(v));")]                                                     // "3,5"
    [InlineData(View + "return v;")]                                                                                               // [3,5]
    public void AView_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>The statements, which add what they walk to <c>s</c>, and then what the walk was refused
    /// with, if anything.</summary>
    private static string Walked(string statements) =>
        "var s = \"\"; try { " + statements + " return s; } "
        + "catch (InvalidOperationException e) { return s + \"|\" + e.Message; }";

    [SkippableTheory]
    // A walk over the view ends at any change to the set, inside the range or not.
    [InlineData(View + "foreach (var x in v) { s += x; if (t.Count > 9) break; t.Add(x + 1); }")]          // 3|Collection was modified after the enumerator was instantiated.
    [InlineData(View + "foreach (var x in v) { s += x; t.Add(100 + x); }")]                                 // 3|the same: outside the range
    [InlineData(View + "foreach (var x in v) { s += x; t.Remove(42); }")]                                   // 3|the same: a set's Remove that finds nothing
    // A walk over the set ends at a change made through the view, and only at one.
    [InlineData(View + "foreach (var x in t) { s += x; if (x == 1) v.Add(4); }")]                          // 1|the same
    [InlineData(View + "foreach (var x in t) { s += x; if (x == 1) try { v.Add(9); } catch (ArgumentOutOfRangeException) { } }")] // 1357: refused before it changed anything
    [InlineData(View + "foreach (var x in t) { s += x; v.Remove(9); }")]                                    // 1357: outside the range
    [InlineData(Set + "var v = t.GetViewBetween(8, 9); foreach (var x in t) { s += x; v.Clear(); }")]       // 1357: nothing to clear
    public void AWalkOverAViewOrItsSet_EndsAsDotNetEndsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Walked(statements));
    }

    [SkippableTheory]
    // A view adds element by element, and refuses the first outside its range.
    [InlineData(View + "v.UnionWith(new[] { 4, 3 }); return string.Join(\",\", t);")]                                            // "1,3,4,5,7"
    [InlineData(View + "try { v.UnionWith(new[] { 4, 9 }); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message + \"|\" + string.Join(\",\", t); }")] // "…(Parameter 'item')|1,3,4,5,7"
    [InlineData(View + "try { v.SymmetricExceptWith(new[] { 3, 9 }); return \"no\"; } catch (ArgumentOutOfRangeException) { return string.Join(\",\", t); }")] // "1,5,7"
    [InlineData(View + "v.SymmetricExceptWith(new[] { 3, 4 }); return string.Join(\",\", t) + \"|\" + string.Join(\",\", v);")] // "1,4,5,7|4,5"
    [InlineData(View + "v.IntersectWith(new[] { 5, 7, 1 }); return string.Join(\",\", t) + \"|\" + string.Join(\",\", v);")]   // "1,5,7|5"
    [InlineData(View + "v.IntersectWith(new SortedSet<int> { 5, 7 }); return string.Join(\",\", t) + \"|\" + string.Join(\",\", v);")] // "1,5,7|5"
    [InlineData(View + "var n = v.RemoveWhere(x => x > 2); return n + \"|\" + string.Join(\",\", t);")]                           // "2|1,7"
    // A view's ExceptWith reads the count it last counted.
    [InlineData(View + "v.ExceptWith(new[] { 3, 7 }); return string.Join(\",\", t);")]                                          // "1,3,5,7": never counted
    [InlineData(View + "var c = v.Count; v.ExceptWith(new[] { 3, 7 }); return c + \"|\" + string.Join(\",\", t);")]              // "2|1,5,7"
    [InlineData(View + "var c = v.Count; v.ExceptWith(new SortedSet<int> { 3, 7 }); return string.Join(\",\", t);")]             // "1,5,7"
    [InlineData("var t = new SortedSet<int> { 1, 9 }; var v = t.GetViewBetween(2, 6); var c = v.Count; t.Add(4); v.ExceptWith(new[] { 4 }); return string.Join(\",\", t);")] // "1,4,9": counted empty
    [InlineData("var t = new SortedSet<int> { 1, 9 }; var v = t.GetViewBetween(2, 6); var c = v.Count; t.Add(4); c = v.Count; v.ExceptWith(new[] { 4 }); return string.Join(\",\", t);")] // "1,9": counted again
    [InlineData(View + "var c = v.Count; v.ExceptWith(v); return string.Join(\",\", t);")]                                        // "1,7"
    // LINQ, a copy and another set's operations read the count as ICollection<T>.Count, which counts
    // again; a walk does not.
    [InlineData(View + "var l = v.ToList(); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                            // "1,5,7"
    [InlineData(View + "var a = v.ToArray(); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                           // "1,5,7"
    [InlineData(View + "var n = v.Count(); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                             // "1,5,7"
    [InlineData(View + "var a = v.Any(); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                               // "1,5,7"
    [InlineData(View + "var l = new List<int>(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                      // "1,5,7"
    [InlineData(View + "foreach (var x in v) { } v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                       // "1,3,5,7"
    [InlineData(View + "var u = new SortedSet<int>(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]                // "1,5,7"
    [InlineData(View + "new SortedSet<int>().UnionWith(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]             // "1,5,7": copied as a new set copies it
    [InlineData(View + "new SortedSet<int> { 9 }.UnionWith(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]         // "1,5,7": merged, by its count
    [InlineData(View + "new SortedSet<int> { 1, 9, 11, 13, 15 }.UnionWith(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")]  // "1,5,7": added one by one, its count read first
    [InlineData(View + "var o = new SortedSet<int> { 3 }.Overlaps(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "True|1,5,7"
    [InlineData(View + "var o = new SortedSet<int> { 3 }.IsSupersetOf(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "False|1,5,7"
    [InlineData(View + "var o = new SortedSet<int> { 1, 3, 5, 7 }.IsProperSupersetOf(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "True|1,5,7"
    [InlineData(View + "var o = new SortedSet<int>().IsProperSubsetOf(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "True|1,5,7": an empty set asks
    [InlineData(View + "var o = new SortedSet<int> { 3 }.IsProperSubsetOf(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "True|1,5,7": by its count
    [InlineData(View + "var o = new SortedSet<int> { 3, 5 }.SetEquals(v); v.ExceptWith(new[] { 3 }); return o + \"|\" + string.Join(\",\", t);")] // "True|1,3,5,7": walked
    [InlineData("var t = new SortedSet<string> { \"a\", \"c\", \"e\" }; var v = t.GetViewBetween(\"b\", \"d\"); new SortedSet<string>(StringComparer.Ordinal) { \"z\" }.SymmetricExceptWith(v); v.ExceptWith(new[] { \"c\" }); return string.Join(\",\", t);")] // "a,e": another comparer's copied
    // A set that walks a view of its own elements while it removes them refuses the walk.
    [InlineData(View + "try { t.ExceptWith(v); return \"no\"; } catch (InvalidOperationException e) { return e.Message + \"|\" + string.Join(\",\", t); }")] // "Collection was modified after the enumerator was instantiated.|1,5,7"
    [InlineData(View + "try { t.SymmetricExceptWith(v); return \"no\"; } catch (InvalidOperationException e) { return e.Message + \"|\" + string.Join(\",\", t); }")] // the same
    // A set takes a view as it takes any sorted set of its comparer.
    [InlineData(Set + "var u = new SortedSet<int> { 10 }; u.UnionWith(t.GetViewBetween(2, 6)); return string.Join(\",\", u);")]  // "3,5,10"
    [InlineData(Set + "var u = new SortedSet<int>(); u.UnionWith(t.GetViewBetween(2, 6)); return string.Join(\",\", u);")]       // "3,5"
    [InlineData(Set + "var u = new SortedSet<int> { 1, 3, 5 }; u.IntersectWith(t.GetViewBetween(2, 6)); return string.Join(\",\", u);")] // "3,5"
    [InlineData(Set + "var u = new SortedSet<int> { 1, 3, 5 }; u.ExceptWith(t.GetViewBetween(2, 6)); return string.Join(\",\", u);")] // "1"
    // The questions, a view against its set and against another view.
    [InlineData(View + "return v.IsSubsetOf(t) + \",\" + v.IsSubsetOf(new[] { 3, 5 }) + \",\" + v.IsProperSubsetOf(t) + \",\" + t.IsSupersetOf(v) + \",\" + v.SetEquals(new[] { 5, 3 }) + \",\" + v.Overlaps(new[] { 7 }) + \",\" + t.IsProperSupersetOf(v);")] // "True,True,True,True,True,False,True"
    [InlineData(View + "var w = t.GetViewBetween(4, 4); return w.IsSubsetOf(v) + \",\" + v.IsSupersetOf(w) + \",\" + v.SetEquals(w) + \",\" + v.IsSubsetOf(t.GetViewBetween(3, 4));")] // "True,True,False,False"
    // A view asks a set it is compared with for a view of that set's range, which a narrower view refuses.
    [InlineData(View + "try { return v.IsSupersetOf(new SortedSet<int> { 1, 9 }).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "… (Parameter 'lowerValue')"
    public void AViewsSetOperations_TakeDotNetsPaths(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
