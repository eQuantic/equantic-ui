using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>HashSet&lt;T&gt;</c> answers as .NET's does, on both sides. It was a JavaScript <c>Set</c>, which
/// found an element by identity, so a date, a decimal, a tuple and a record equal to one in the set
/// were not in it (#531), and which appended, where .NET's set reuses the slot a removal freed, the last
/// freed first: <c>{ 3, 1 }</c> less 3, plus 5, enumerates <c>5, 1</c> in .NET and <c>1, 5</c> in a
/// <c>Set</c> (#438). It is the runtime's set now, held in the slot table the runtime's dictionary keeps
/// its keys in, its elements found by the element type's equality.
/// <para>
/// .NET's set operations free and take slots in an order of their own, and a copy of a set keeps the
/// set's free slots while the set is not much larger than it holds: each is measured here by the order
/// the set enumerates in after it, which is where the next insertion lands.
/// </para>
/// </summary>
public class HashSetConformanceTests
{
    private const string Prelude = "public record Point(int X, int Y);";

    [SkippableTheory]
    // The row of #438, and two removals taken back in the order they were freed: last freed, first taken.
    [InlineData("var s = new HashSet<int> { 3, 1 }; s.Remove(3); s.Add(5); return string.Join(\",\", s);")]                                         // 5,1
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4 }; s.Remove(2); s.Remove(4); s.Add(5); s.Add(6); s.Add(7); return string.Join(\",\", s);")]  // 1,6,3,5,7
    // A copy keeps the free slots of a set not much larger than it holds, and packs a larger one; a
    // copy of anything that is not a set adds each element in turn.
    [InlineData("var a = new HashSet<int> { 3, 1 }; a.Remove(3); var b = new HashSet<int>(a); b.Add(5); b.Add(6); return string.Join(\",\", b);")]  // 5,1,6
    [InlineData("var a = new HashSet<int> { 3, 1 }; a.Remove(3); var b = a.ToHashSet(); b.Add(5); return string.Join(\",\", b);")]                  // 5,1
    [InlineData("var a = new HashSet<int>(); for (int i = 0; i < 8; i++) a.Add(i); a.Remove(0); var b = new HashSet<int>(a); b.Add(100); return string.Join(\",\", b);")] // 100,1,…,7
    [InlineData("var a = new HashSet<int>(); for (int i = 0; i < 8; i++) a.Add(i); for (int i = 0; i < 5; i++) a.Remove(i); var b = new HashSet<int>(a); b.Add(100); return string.Join(\",\", b);")] // 5,6,7,100
    [InlineData("var a = new HashSet<int>(); for (int i = 0; i < 20; i++) a.Add(i); for (int i = 0; i < 18; i++) a.Remove(i); var b = new HashSet<int>(a); b.Add(100); return string.Join(\",\", b);")] // 18,19,100
    [InlineData("var a = new HashSet<int> { 3, 1 }; a.Remove(3); var b = new HashSet<int>(a.ToList()); b.Add(5); return string.Join(\",\", b);")]  // 1,5
    // The set operations, each freeing and taking slots as .NET's does.
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; var n = s.RemoveWhere(x => x % 2 == 1); s.Add(10); s.Add(11); s.Add(12); s.Add(13); return n + \":\" + string.Join(\",\", s);")] // 3:12,2,11,4,10,13
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; s.ExceptWith(new[] { 4, 2 }); s.Add(10); s.Add(11); s.Add(12); return string.Join(\",\", s);")]            // 1,10,3,11,5,12
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; s.IntersectWith(new List<int> { 5, 3 }); s.Add(10); s.Add(11); s.Add(12); return string.Join(\",\", s);")] // 12,11,3,10,5
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; s.IntersectWith(new HashSet<int> { 5, 3 }); s.Add(10); s.Add(11); s.Add(12); return string.Join(\",\", s);")]
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; s.SymmetricExceptWith(new List<int> { 6, 4, 2, 7, 2 }); s.Add(10); s.Add(11); return string.Join(\",\", s);")] // 1,11,3,10,5,6,7
    [InlineData("var s = new HashSet<int> { 1, 2, 3, 4, 5 }; s.SymmetricExceptWith(new HashSet<int> { 6, 4, 2, 7 }); s.Add(10); s.Add(11); return string.Join(\",\", s);")] // 1,7,3,10,5,6,11
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; s.Remove(2); s.UnionWith(new[] { 9, 1, 8 }); return string.Join(\",\", s);")]                  // 1,9,3,8
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; s.Remove(2); s.Clear(); s.Add(7); s.Add(8); return string.Join(\",\", s);")]                  // 7,8
    // TrimExcess packs the slots only where the capacity the set needs is smaller than the one it has.
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; s.Remove(1); s.TrimExcess(); s.Add(7); return string.Join(\",\", s);")]                      // 7,2,3
    [InlineData("var s = new HashSet<int>(); for (int i = 0; i < 10; i++) s.Add(i); for (int i = 0; i < 8; i++) s.Remove(i); s.TrimExcess(); s.Add(7); return string.Join(\",\", s);")]
    // Built by a collection expression, a target-typed new, ToHashSet and a constructor's collection.
    [InlineData("HashSet<int> s = [3, 1, 3]; s.Remove(3); s.Add(5); return string.Join(\",\", s) + \"|\" + s.Count;")]
    [InlineData("HashSet<int> s = new() { 4, 5 }; s.Remove(4); s.Add(6); return string.Join(\",\", s);")]
    [InlineData("var s = new[] { 3, 1, 3, 2 }.ToHashSet(); s.Remove(1); s.Add(9); return string.Join(\",\", s);")]
    [InlineData("var s = new HashSet<int>(new[] { 1, 2, 1, 3, 2 }); return s.Count + \":\" + string.Join(\",\", s);")]
    // An element added while the set is walked ends the walk; one removed does not.
    [InlineData("try { var s = new HashSet<int> { 1, 2 }; foreach (var x in s) s.Add(x + 10); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; var seen = new List<int>(); foreach (var x in s) { seen.Add(x); s.Remove(2); } return string.Join(\",\", seen) + \"|\" + string.Join(\",\", s);")] // 1,3|1,3
    [InlineData("var s = new HashSet<int> { 1, 2 }; foreach (var x in s) s.Add(1); return string.Join(\",\", s);")]
    public void HashSet_HoldsItsElementsBySlot(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // The rows of #531: a date, a decimal, a tuple and a record found by value.
    [InlineData("return new HashSet<DateTime> { new(2026, 1, 1) }.Contains(new DateTime(2026, 1, 1));")]                 // True
    [InlineData("return new HashSet<decimal> { 1.0m }.Contains(1.00m);")]                                                // True
    [InlineData("return new HashSet<decimal> { 1.0m, 1.00m }.Count;")]                                                   // 1
    [InlineData("return new HashSet<(int, int)> { (1, 2) }.Contains((1, 2));")]                                         // True
    [InlineData("return new HashSet<Point> { new(1, 2) }.Contains(new Point(1, 2));")]                                   // True
    // The first of two equal values is the one kept, as .NET keeps it.
    [InlineData("return string.Join(\",\", new HashSet<decimal> { 1.0m, 1.00m });")]                                      // 1.0
    [InlineData("var s = new HashSet<(int, int)> { (1, 2), (1, 2), (2, 1) }; return s.Count + \":\" + s.Contains((2, 1));")]
    // A double's equality: a NaN equals a NaN, and -0 equals 0, the first one in staying.
    [InlineData("return new HashSet<double> { double.NaN, double.NaN }.Count;")]                                         // 1
    [InlineData("var s = new HashSet<double> { 0.0, -0.0 }; return s.Count + \":\" + (1 / s.First());")]                  // 1:Infinity
    [InlineData("var s = new HashSet<double> { -0.0, 0.0 }; return s.Count + \":\" + (1 / s.First());")]                  // 1:-Infinity
    // A null is an element like any other.
    [InlineData("var s = new HashSet<string> { null, \"a\", null }; return s.Count + \":\" + s.Contains(null) + \":\" + s.Remove(null) + \":\" + s.Count;")] // 2:True:True:1
    // A tuple's array element by reference; an element typed object by what it holds.
    [InlineData("var a = new[] { 1 }; var s = new HashSet<(int[], int)> { (a, 1) }; return s.Contains((a, 1)) + \":\" + s.Contains((new[] { 1 }, 1));")] // True:False
    // A dictionary's keys take the same rule, one equality for both.
    [InlineData("var a = new[] { 1 }; var d = new Dictionary<(int[], int), int> { [(a, 1)] = 5 }; return d.ContainsKey((a, 1)) + \":\" + d.ContainsKey((new[] { 1 }, 1));")] // True:False
    [InlineData("var a = new[] { 1 }; var d = new Dictionary<(int[], int), int>(); d[(a, 1)] = 5; d[(new[] { 1 }, 1)] = 6; return d.Count;")] // 2
    [InlineData("return new HashSet<object> { 1.0m }.Contains(1.00m);")]                                                // True
    [InlineData("return new HashSet<object> { new Point(1, 2) }.Contains(new Point(1, 2));")]                            // True
    [InlineData("ISet<Point> s = new HashSet<Point>(); s.Add(new Point(1, 2)); return s.Contains(new Point(1, 2)) + \",\" + s.Add(new Point(1, 2));")] // True,False
    [InlineData("var s = new HashSet<int>(); return s.Add(1) + \",\" + s.Add(1) + \",\" + s.Count;")]                    // True,False,1
    // Through a face a list answers to as well, the set answers by its own equality.
    [InlineData("IReadOnlyCollection<int> c = new HashSet<int> { 1, 2 }; return c.Contains(2) + \",\" + c.Count;")]
    [InlineData("ICollection<int> c = new HashSet<int> { 1, 2 }; var r = c.Remove(1); return r + \",\" + c.Count + \",\" + string.Join(\",\", c);")]
    [InlineData("ICollection<DateTime> c = new HashSet<DateTime> { new(2026, 1, 1) }; return c.Contains(new DateTime(2026, 1, 1));")]
    public void HashSet_FindsItsElementsAsTheDefaultComparer(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; return s.IsSubsetOf(new[] { 1, 2, 3, 4 }) + \",\" + s.IsProperSubsetOf(new[] { 1, 2, 3 }) + \",\" + s.IsSupersetOf(new List<int> { 1 }) + \",\" + s.IsProperSupersetOf(new HashSet<int> { 1, 2, 3 }) + \",\" + s.Overlaps(new[] { 9, 3 }) + \",\" + s.SetEquals(new[] { 3, 2, 1, 1 });")]
    [InlineData("var s = new HashSet<Point> { new(1, 2) }; return s.SetEquals(new[] { new Point(1, 2) }) + \",\" + s.Overlaps(new List<Point> { new(1, 2) }) + \",\" + s.IsSubsetOf(new HashSet<Point> { new(1, 2), new(3, 4) });")]
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; var a = new int[5]; s.CopyTo(a, 1); s.CopyTo(a, 3, 1); return string.Join(\",\", a);")]
    [InlineData("try { var s = new HashSet<int> { 1, 2, 3 }; s.CopyTo(new int[2]); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("var s = new HashSet<int>(); return s.EnsureCapacity(5) + \",\" + s.EnsureCapacity(2) + \",\" + new HashSet<int>(10).EnsureCapacity(0);")] // 7,7,11
    // A set behind ICollection<T> copies itself, and so does any collection behind it.
    [InlineData("ICollection<int> c = new HashSet<int> { 1, 2 }; var a = new int[3]; c.CopyTo(a, 1); return string.Join(\",\", a);")]                  // 0,1,2
    [InlineData("ICollection<int> c = new LinkedList<int>(new[] { 3, 4 }); var a = new int[3]; c.CopyTo(a, 0); return string.Join(\",\", a);")]          // 3,4,0
    public void HashSet_AnswersItsOwnMembers(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
