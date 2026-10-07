using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A dictionary's <c>Keys</c> and <c>Values</c> are live views, and its capacity is .NET's (#463).
/// <c>Keys</c> and <c>Values</c> were arrays copied when they were read, so a key added afterwards was
/// not in them; <c>EnsureCapacity</c> answered the capacity asked for, where .NET answers the prime it
/// settles on; and <c>TrimExcess</c> did nothing, where .NET packs the entries and empties the free
/// list, so the next key went into a freed slot instead of after the others.
/// </summary>
public class DictionaryViewConformanceTests
{
    private const string Five =
        "var d = new Dictionary<string, int>(); foreach (var k in new[] { \"k0\", \"k1\", \"k2\", \"k3\", \"k4\" }) d[k] = 1; "
        + "d.Remove(\"k0\"); d.Remove(\"k1\"); ";

    [SkippableTheory]
    // A view read after the dictionary changed shows the change.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var ks = d.Keys; d[\"z\"] = 2; return ks.Count + \"|\" + string.Join(\",\", ks);")] // "2|a,z"
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var vs = d.Values; d[\"a\"] = 5; d[\"b\"] = 6; return string.Join(\",\", vs);")]    // "5,6"
    [InlineData("var d = new Dictionary<int, int> { [1] = 1, [2] = 2 }; var ks = d.Keys; d.Remove(1); return string.Join(\",\", ks);")]                  // "2"
    [InlineData("var d = new Dictionary<int, int> { [1] = 1 }; var ks = d.Keys; d.Clear(); return ks.Count;")]                                            // 0
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var ks = d.Keys; d[\"b\"] = 2; return ks.Where(k => k != \"a\").Count();")]         // 1: through LINQ
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var ks = d.Keys; d[\"b\"] = 2; return ks.Contains(\"b\");")]                       // true
    // A copy of a view is a copy.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var l = d.Keys.ToList(); d[\"b\"] = 2; return l.Count;")]                          // 1
    // A key added while the keys are walked ends the walk; a value replaced does not.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; try { foreach (var k in d.Keys) d[\"z\"] = 2; return \"no\"; } catch (InvalidOperationException e) { return e.Message; }")] // "Collection was modified; enumeration operation may not execute."
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; foreach (var v in d.Values) d[\"a\"] = 5; return d[\"a\"];")]                        // 5
    [InlineData("var d = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 }; foreach (var k in d.Keys) d.Remove(k); return d.Count;")]               // 0: a removal leaves the walk running
    // An element as it is: a delegate read back from the values is the one stored, where it was bound
    // to the snapshot and was another.
    [InlineData("Func<int> f = () => 1; var d = new Dictionary<int, Func<int>> { [0] = f }; return d.Values.First() == f;")] // true
    // The capacity .NET settles on, a copy's sized for what it copies.
    [InlineData("var s = new Dictionary<string, int>(); for (var i = 0; i < 8; i++) s[\"k\" + i] = i; return new Dictionary<string, int>(s).EnsureCapacity(0);")] // 11, where it grew to 17
    [InlineData("var s = new HashSet<KeyValuePair<int, int>>(); for (var i = 0; i < 8; i++) s.Add(new KeyValuePair<int, int>(i, i)); return new Dictionary<int, int>(s).EnsureCapacity(0);")] // 11: any ICollection<T> by its count
    [InlineData("var q = new Queue<KeyValuePair<int, int>>(); for (var i = 0; i < 8; i++) q.Enqueue(new KeyValuePair<int, int>(i, i)); return new Dictionary<int, int>(q).EnsureCapacity(0);")] // 17: a queue is no ICollection<T>
    [InlineData("var d = new Dictionary<string, int>(); for (var i = 0; i < 8; i++) d[\"k\" + i] = i; return new HashSet<KeyValuePair<string, int>>(d).EnsureCapacity(0);")] // 11: a set copies a dictionary by its count too
    // A view refuses ICollection<T>'s mutators as .NET's does, where the frozen snapshot threw a
    // TypeError, read as a NullReferenceException.
    [InlineData("ICollection<string> keys = new Dictionary<string, int> { [\"a\"] = 1 }.Keys; try { keys.Add(\"b\"); return \"no\"; } catch (NotSupportedException e) { return e.Message; }")]   // "Mutating a key collection derived from a dictionary is not allowed."
    [InlineData("ICollection<string> keys = new Dictionary<string, int> { [\"a\"] = 1 }.Keys; try { keys.Clear(); return \"no\"; } catch (NotSupportedException e) { return e.Message; }")]     // the same
    [InlineData("ICollection<int> values = new Dictionary<string, int> { [\"a\"] = 1 }.Values; try { values.Add(2); return \"no\"; } catch (NotSupportedException e) { return e.Message; }")] // "Mutating a value collection derived from a dictionary is not allowed."
    [InlineData("ICollection<int> values = new Dictionary<string, int> { [\"a\"] = 1 }.Values; try { values.Remove(1); return \"no\"; } catch (NotSupportedException e) { return e.Message; }")] // the same
    [InlineData("return new Dictionary<string, int>().EnsureCapacity(10);")]                                                                              // 11
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.EnsureCapacity(2);")]                                                     // 3
    [InlineData("return new Dictionary<string, int>(10).EnsureCapacity(0);")]                                                                             // 11: the constructor's
    [InlineData("try { new Dictionary<string, int>().EnsureCapacity(-1); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "Specified argument was out of the range of valid values. (Parameter 'capacity')"
    [InlineData("try { var d = new Dictionary<string, int>(-1); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]          // the same
    // TrimExcess packs the entries, so the next key goes after the others.
    [InlineData(Five + "d.TrimExcess(); d[\"n\"] = 1; return string.Join(\",\", d.Keys);")]                                                               // "k2,k3,k4,n"
    [InlineData(Five + "d.TrimExcess(100); d[\"n\"] = 1; return string.Join(\",\", d.Keys);")]                                                            // "n,k2,k3,k4": a larger prime trims nothing
    [InlineData(Five + "d.TrimExcess(3); d[\"n\"] = 1; return string.Join(\",\", d.Keys) + \"|\" + d.EnsureCapacity(0);")]                                // "k2,k3,k4,n|7"
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; try { d.TrimExcess(1); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "… (Parameter 'capacity')"
    public void ADictionary_ViewsAndSizesAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
