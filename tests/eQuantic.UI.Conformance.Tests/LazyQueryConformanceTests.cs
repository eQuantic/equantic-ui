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
    // And so do the ones with a strategy of their own: an ordering, a set, a grouping, a reversal, a type
    // test, a filler and a zip read the source as it is when they are walked.
    [InlineData("var l = new List<int> { 3, 1 }; var q = l.OrderBy(x => x); var d = l.OrderByDescending(x => x).ThenBy(x => -x); var o = l.Order(); l.Add(2); return string.Join(\",\", q) + \"|\" + string.Join(\",\", d) + \"|\" + string.Join(\",\", o) + \"|\" + q.Count() + q.First() + q.ToList().Count;")]
    [InlineData("var calls = 0; var l = new List<int> { 2, 1 }; var q = l.OrderBy(x => { calls++; return x; }); var before = calls; var r = q.ToList(); return before + \"|\" + (calls > 0) + \"|\" + string.Join(\",\", r);")]
    [InlineData("var l = new List<int> { 1, 1, 2 }; var q = l.Distinct(); l.Add(9); l.Add(2); return string.Join(\",\", q) + \"|\" + q.Count();")]
    [InlineData("var l = new List<int> { 1, 2 }; var q = l.GroupBy(x => x % 2); l.Add(3); return string.Join(\";\", q.Select(g => g.Key + \":\" + string.Join(\",\", g))) + \"|\" + string.Join(\",\", l.GroupBy(x => x % 2, (k, g) => k + g.Count()));")]
    [InlineData("var l = new List<int> { 1, 2 }; var q = l.AsEnumerable().Reverse(); l.Add(3); return string.Join(\",\", q);")]
    [InlineData("var l = new List<object> { 1, \"a\" }; var q = l.OfType<int>(); var s = l.OfType<string>(); l.Add(2); l.Add(\"b\"); return string.Join(\",\", q) + \"|\" + string.Join(\",\", s);")]
    [InlineData("var l = new List<int>(); var n = 7; var q = l.DefaultIfEmpty(n); n = 5; var a = string.Join(\",\", q); l.Add(1); return a + \"|\" + string.Join(\",\", q);")]
    [InlineData("var a = new List<int> { 1, 2 }; var b = new List<string> { \"x\", \"y\", \"z\" }; var q = a.Zip(b, (n, s) => s + n); var t = a.Zip(b); a.Add(3); return string.Join(\",\", q) + \"|\" + string.Join(\",\", t.Select(p => p.First + p.Second));")]
    // A null source is refused when the operator is called, by the name its receiver binds to.
    [InlineData("List<int> none = null; try { var q = none.Union(new[] { 1 }); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
    [InlineData("List<int> none = null; try { var q = none.Zip(new[] { 1 }); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
    [InlineData("List<int> none = null; try { var q = none.OrderBy(x => x); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
    [InlineData("List<int> none = null; try { var q = none.GroupBy(x => x); return \"made\"; } catch (ArgumentNullException e) { return e.Message; }")]
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
    // A query read to its end or to an answer is walked by pushing, in the same steps.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2 }; var r = l.SelectMany(x => { log += x; return new[] { x, x * 10 }; }).Take(3).ToList(); return log + \"|\" + string.Join(\",\", r);")]
    [InlineData("var log = \"\"; var a = new List<int> { 1 }; var b = new List<int> { 2 }; var r = a.Concat(b.Select(x => { log += x; return x; })).Take(1).ToList(); return log + \"|\" + string.Join(\",\", r);")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; try { var r = l.Where(x => { if (x == 1) l.Add(9); return true; }).ToList(); return r.Count.ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; return string.Join(\",\", l.Prepend(0).Append(9).Skip(1).Take(2).ToList()) + \"|\" + string.Join(\",\", l.SkipWhile((x, i) => i < 1).ToList()) + \"|\" + \"abc\".Where(c => c != 'b').Count() + \"|\" + string.Join(\",\", l.Distinct().ToList()) + string.Join(\",\", l.OrderBy(x => -x).Take(2).ToList());")]
    [InlineData("var h = new HashSet<int> { 1 }; var d = new Dictionary<int, int>(); var q = new Queue<int>(); return h.Any() + \"|\" + d.Any() + \"|\" + q.Any() + \"|\" + h.Concat(h).Any() + \"|\" + h.Append(2).ToList().Count;")]
    // A projection of a list reads one element alone, as .NET's does.
    [InlineData("var log = \"\"; var l = new List<int> { 1, 2, 3 }; var e = l.Select(x => { log += x; return x; }).ElementAt(2); var f = l.Select(x => { log += x; return x; }).Last(x => x < 3); var g = l.Select(x => { log += x; return x; }).LastOrDefault(); return log + \"|\" + e + f + g;")]
    [InlineData("var l = new List<int>(); try { return l.Select(x => x).Last().ToString(); } catch (InvalidOperationException e) { return e.Message + \"|\" + l.Select(x => x).LastOrDefault() + l.Select(x => x).FirstOrDefault(); }")]
    public void AQuery_RunsAsDotNetsIteratorsRun(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
