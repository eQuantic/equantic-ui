using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>List&lt;T&gt;</c> refuses a walk over it once it changed, as .NET's enumerator compares the list's
/// version at every step, the step after the last element included (#863). The twin of a list is the
/// array itself, whose own iterator reads the length at every step, so a walk that added as it went
/// never ended. Which changes move the version is .NET's, measured: an empty range moves nothing, a
/// <c>Clear</c>, a <c>Sort</c> and a <c>Reverse</c> move it whatever they hold, and so does an element
/// written through the indexer, the same value included.
/// </summary>
public class ListVersionConformanceTests
{
    private const string Walk = "var l = new List<int> { 3, 1, 2 }; var steps = 0; try { foreach (var x in l) { steps++; if (steps == 1) { CHANGE; } } return steps + \"|done\"; } catch (InvalidOperationException e) { return steps + \"|\" + e.Message; }";

    [SkippableTheory]
    [InlineData("l.Add(9)")]
    [InlineData("l.AddRange(new int[0])")]
    [InlineData("l.AddRange(new[] { 9 })")]
    [InlineData("l.InsertRange(0, new int[0])")]
    [InlineData("l.Insert(0, 9)")]
    [InlineData("l.Remove(42)")]
    [InlineData("l.Remove(1)")]
    [InlineData("l.RemoveAt(0)")]
    [InlineData("l.RemoveRange(0, 0)")]
    [InlineData("l.RemoveRange(0, 1)")]
    [InlineData("l.RemoveAll(v => false)")]
    [InlineData("l.RemoveAll(v => v == 2)")]
    [InlineData("l.Clear()")]
    [InlineData("l.Sort()")]
    [InlineData("l.Sort(0, 0, null)")]
    [InlineData("l.Sort((a, b) => a.CompareTo(b))")]
    [InlineData("l.Reverse()")]
    [InlineData("l.Reverse(0, 0)")]
    [InlineData("l[0] = 5")]
    [InlineData("l[0] = l[0]")]
    [InlineData("l[0] += 1")]
    [InlineData("l[^1] = 7")]
    [InlineData("l.ForEach(v => { })")]
    [InlineData("((IList<int>)l)[0] = 5")]
    [InlineData("((ICollection<int>)l).Add(5)")]
    [InlineData("((ICollection<int>)l).Clear()")]
    [InlineData("((IList<int>)l).Insert(0, 5)")]
    public void AChangeDuringAWalk_IsRefusedAsDotNetRefusesIt(string change)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Walk.Replace("CHANGE", change));
    }

    [SkippableTheory]
    // The row of #863: a walk that adds as it goes.
    [InlineData("var l = new List<int> { 1, 2 }; var s = \"\"; try { foreach (var x in l) { s += x; if (l.Count > 9) break; l.Add(x); } return s; } catch (InvalidOperationException e) { return s + \"|\" + e.Message; }")]
    // A change in the last step's body is refused at the step after it.
    [InlineData("var l = new List<int> { 1, 2 }; var s = \"\"; try { foreach (var x in l) { s += x; if (x == 2) l.Add(3); } return s; } catch (InvalidOperationException e) { return s + \"|\" + e.Message; }")]
    // A walk that changes nothing, and one that breaks before the change is read, end as .NET's do.
    [InlineData("var l = new List<int> { 1, 2, 3 }; var s = 0; foreach (var x in l) s += x; return s.ToString();")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; foreach (var x in l) { l.Add(x); break; } return l.Count.ToString();")]
    // A deconstructing walk, and two walks one inside the other.
    [InlineData("var l = new List<(int A, int B)> { (1, 2), (3, 4) }; var s = 0; try { foreach (var (a, b) in l) { s += a + b; l.Add((0, 0)); } return s.ToString(); } catch (InvalidOperationException e) { return s + \"|\" + e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2 }; var s = \"\"; try { foreach (var x in l) foreach (var y in l) { s += x * y; if (y == 2) l.Remove(1); } return s; } catch (InvalidOperationException e) { return s + \"|\" + e.Message; }")]
    // ForEach refuses a change its action makes, after the action returns.
    [InlineData("var l = new List<int> { 1, 2 }; var s = \"\"; try { l.ForEach(x => { s += x; if (x == 1) l.Add(5); }); return s; } catch (InvalidOperationException e) { return s + \"|\" + e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2 }; var s = \"\"; l.ForEach(x => s += x); return s;")]
    [InlineData("var l = new List<int> { 1 }; try { l.ForEach(null); return \"done\"; } catch (ArgumentNullException e) { return e.Message; }")]
    // A list made from another starts with a version of its own.
    [InlineData("var l = new List<int> { 1, 2 }; l.Add(3); var copy = new List<int>(l); var s = 0; foreach (var x in copy) s += x; return s.ToString();")]
    // An array is no list: an element written while it is walked is allowed.
    [InlineData("int[] a = { 1, 2, 3 }; var s = 0; foreach (var x in a) { s += x; a[2] = 10; } return s.ToString();")]
    [InlineData("int[] a = { 1, 2, 3 }; var s = 0; foreach (var x in a) { s += x; ((IList<int>)a)[2] = 10; } return s.ToString();")]
    // The indexer refuses an index outside the list, where a subscript grew the array.
    [InlineData("var l = new List<int> { 1 }; try { l[1] = 5; return l.Count.ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { l[-1] = 5; return l.Count.ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    // A write answers the value written, and a compound reads before it writes.
    [InlineData("var l = new List<int> { 1, 2 }; var y = (l[0] = 7); l[1] *= 3; l[^1]++; return y + \"|\" + string.Join(\",\", l);")]
    public void AListWalk_EndsAsDotNetsDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
