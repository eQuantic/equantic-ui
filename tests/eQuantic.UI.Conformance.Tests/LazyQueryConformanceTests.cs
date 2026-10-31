using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A LINQ query runs as .NET's iterators run it (#685): an operator reads nothing when it is called, and
/// its walk reads the source one element at a time, each only after the callback before it returned. So
/// a callback that changes its source changes what is read next, a list changed under the walk ends it
/// with .NET's InvalidOperationException, a chain of operators interleaves its callbacks, and an operator
/// that answers stops at the element that answers. Every operator was an array method over a copy of the
/// source, read whole before the first callback ran.
/// </summary>
public class LazyQueryConformanceTests
{
    [SkippableTheory]
    // The rows of #685.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; return string.Join(\",\", d.Select(kv => { d[\"b\"] = 5; return kv.Value; }));")]
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; return string.Join(\",\", d.Values.Select(v => { d[\"b\"] = 5; return v; }));")]
    [InlineData("var l = new List<int> { 1, 2 }; try { return string.Join(\",\", l.Select(v => { l[1] = 5; return v; })); } catch (InvalidOperationException e) { return e.Message; }")]
    // A chain interleaves its callbacks, element by element.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var r = l.Where(x => { log += \"w\" + x; return x != 2; }).Select(x => { log += \"s\" + x; return x * 10; }).ToList(); return log + \"|\" + string.Join(\",\", r);")]
    // An answer stops the walk.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var any = l.Select(x => { log += x; return x; }).Any(x => x == 2); return log + \"|\" + any;")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var all = l.Select(x => { log += x; return x; }).All(x => x < 2); return log + \"|\" + all;")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var any = l.Select(x => { log += x; return x; }).Any(); return log + \"|\" + any;")]
    [InlineData("var log = \"\"; var l = new List<int> { 4, 5 }; var t = l.TakeWhile(x => { log += x; return x < 5; }).ToList(); return log + \"|\" + string.Join(\",\", t);")]
    // An operator reads nothing until it is walked, and reads again each time it is walked.
    [InlineData("var l = new List<int> { 1, 2 }; var q = l.Where(x => x > 0); l.Add(3); return string.Join(\",\", q);")]
    [InlineData("var calls = 0; var l = new List<int> { 1, 2 }; var q = l.Select(x => { calls++; return x; }); var a = q.ToList(); var b = q.ToList(); return calls + \"|\" + a.Count + b.Count;")]
    [InlineData("var calls = 0; var l = new List<int> { 1, 2 }; var q = l.Select(x => { calls++; return x; }); return calls.ToString();")]
    // Its arguments are checked when it is called.
    [InlineData("List<int> none = null; try { var q = none.Where(x => true); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; Func<int, bool> f = null; try { var q = l.Where(f); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
    // A list changed under a walk of a query over it is refused, as .NET's iterator refuses it.
    [InlineData("var l = new List<int> { 1, 2 }; try { foreach (var x in l.Where(v => true)) l.Add(x); return l.Count.ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    // A query is a sequence to every consumer: a foreach, a join, a concatenation, JSON.
    [InlineData("var l = new List<int> { 1, 2, 3 }; var s = 0; foreach (var x in l.Where(v => v > 1)) s += x; return s + \"|\" + string.Join(\"-\", l.Select(v => v * 2)) + \"|\" + l.Concat(l.Where(v => v == 2)).Count();")]
    [InlineData("var s = \"abc\"; return string.Join(\",\", s.Where(c => c != 'b').Select(c => (int)c));")]
    [InlineData("var l = new List<int> { 1, 2 }; return string.Join(\",\", l.Append(3).Prepend(0).SelectMany(x => new[] { x, x }));")]
    [InlineData("var l = new List<int> { 1, 2, 3, 1 }; return string.Join(\",\", l.SkipWhile(x => x < 2)) + \"|\" + string.Join(\",\", l.Where((x, i) => i % 2 == 0)) + \"|\" + string.Join(\",\", l.Select((x, i) => x * i));")]
    // Skip and Take walk as .NET's do: Take stops once it has its count, a negative count is none.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3, 4 }; var t = l.Select(x => { log += x; return x; }).Take(2).ToList(); return log + \"|\" + string.Join(\",\", t);")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; return string.Join(\",\", l.Skip(-1)) + \"|\" + string.Join(\",\", l.Take(-1)) + \"|\" + string.Join(\",\", l.Skip(1).Take(1)) + \"|\" + l.Skip(5).Count();")]
    [InlineData("var l = new List<int> { 1, 2 }; var q = l.Skip(1); l.Add(3); return string.Join(\",\", q);")]
    // The operators the runtime has no walk for read their source when they are walked.
    [InlineData("var l = new List<int> { 1, 2 }; var q = l.Union(new[] { 5 }); l.Add(3); return string.Join(\",\", q) + \"|\" + string.Join(\",\", l.Except(new[] { 1 })) + \"|\" + string.Join(\",\", l.TakeLast(2)) + \"|\" + string.Join(\",\", l.SkipLast(1));")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; var q = l.Chunk(2); l.Add(4); return string.Join(\";\", q.Select(c => string.Join(\",\", c)));")]
    // An operator that answers from a query stops where .NET's stops.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var f = l.Select(x => { log += x; return x * 10; }).First(); var g = l.Where(x => { log += \"w\" + x; return x > 1; }).First(x => x > 2); return log + \"|\" + f + g;")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var f = l.Where(x => { log += x; return x > 5; }).FirstOrDefault(); var g = l.Select(x => { log += x; return x; }).FirstOrDefault(x => x == 2); return log + \"|\" + f + g;")]
    [InlineData("var l = new List<int> { 1 }; try { return l.Where(x => x > 5).First().ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { return l.Where(x => x > 0).First(x => x > 5).ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var e = l.Where(x => { log += x; return true; }).ElementAt(1); var d = l.Where(x => true).ElementAtOrDefault(7); return log + \"|\" + e + d;")]
    [InlineData("var l = new List<int> { 1 }; try { return l.Where(x => true).ElementAt(3).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var c = l.Select(x => { log += x; return x; }).Contains(2); return log + \"|\" + c;")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var s = l.Where(x => { log += x; return x == 2; }).Single(); var t = l.Select(x => { log += x; return x; }).Last(); return log + \"|\" + s + t;")]
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var n = l.Select(x => { log += x; return x; }).Count(); return log + \"|\" + n;")]
    // A projection of a list reads one element alone, as .NET's does.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var e = l.Select(x => { log += x; return x; }).ElementAt(2); var f = l.Select(x => { log += x; return x; }).Last(x => x < 3); var g = l.Select(x => { log += x; return x; }).LastOrDefault(); return log + \"|\" + e + f + g;")]
    [InlineData("var l = new List<int>(); try { return l.Select(x => x).Last().ToString(); } catch (InvalidOperationException e) { return e.Message + \"|\" + l.Select(x => x).LastOrDefault() + l.Select(x => x).FirstOrDefault(); }")]
    public void AQuery_RunsAsDotNetsIteratorsRun(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
