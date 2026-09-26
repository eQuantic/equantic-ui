using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// What a dictionary answers on the web, measured against .NET on both sides (#435). A dictionary
/// with a primitive key was a plain object, which listed integer-like keys ascending and handed every
/// key back as a string: <c>d[3] = 30; d[1] = 10;</c> enumerated "1,3" where .NET enumerates "3,1".
/// It is now the runtime's dictionary class, which holds entries by slot as .NET's does: in insertion
/// order while nothing is removed, and a removed entry's slot is the next one reused, the last freed
/// first.
/// <para>
/// A dictionary RETURNED here crosses JSON, whose parser lists integer-like names ascending whatever
/// order they were written in (#437), so a case returns what it read out of the dictionary instead.
/// </para>
/// </summary>
public class DictionaryEnumerationConformanceTests
{
    private const string Point = "public record Point(int X, int Y);";

    [SkippableTheory]
    // Integer keys keep insertion order: Keys, Values, a foreach and a deconstructing foreach.
    [InlineData("var d = new Dictionary<int, int>(); d[3] = 30; d[1] = 10; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<int, int>(); d[3] = 30; d[1] = 10; return string.Join(\",\", d.Values);")]
    [InlineData("var d = new Dictionary<int, int>(); d[3] = 30; d[1] = 10; var s = \"\"; foreach (var kv in d) s += kv.Key + \"=\" + kv.Value + \";\"; return s;")]
    [InlineData("var d = new Dictionary<int, int>(); d[3] = 30; d[1] = 10; var s = \"\"; foreach (var (k, v) in d) s += k + \"=\" + v + \";\"; return s;")]
    [InlineData("var d = new Dictionary<int, string> { [5] = \"e\", [2] = \"b\", [9] = \"i\" }; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<int, string> { { 5, \"e\" }, { 2, \"b\" } }; return string.Join(\",\", d.Values);")]
    // String keys, integer-like ones included.
    [InlineData("var d = new Dictionary<string, int>(); d[\"2\"] = 2; d[\"1\"] = 1; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<string, int>(); d[\"b\"] = 2; d[\"a\"] = 1; d[\"10\"] = 10; return string.Join(\",\", d.Keys);")]
    // A key written again keeps its slot.
    [InlineData("var d = new Dictionary<string, int>(); d[\"a\"] = 1; d[\"b\"] = 2; d[\"a\"] = 3; return string.Join(\",\", d.Keys) + \"|\" + string.Join(\",\", d.Values);")]
    // A removal frees a slot the next insertion takes, the last freed first.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2, [\"c\"] = 3 }; d.Remove(\"a\"); d[\"d\"] = 4; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2, [\"c\"] = 3 }; d.Remove(\"b\"); d.Remove(\"a\"); d[\"x\"] = 0; d[\"y\"] = 0; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2, [\"c\"] = 3 }; d.Remove(\"a\"); d.Remove(\"b\"); d[\"x\"] = 0; d[\"y\"] = 0; return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 }; d.Remove(2); d.Add(7, 7); d.Remove(1); d.Add(8, 8); return string.Join(\",\", d.Keys);")]
    // A copy enumerates compacted, in the order of what it copies, and is a copy, not an alias.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2, [\"c\"] = 3 }; d.Remove(\"a\"); var copy = new Dictionary<string, int>(d); copy[\"a\"] = 1; return string.Join(\",\", copy.Keys);")]
    [InlineData("var a = new Dictionary<string, int> { [\"x\"] = 1 }; var b = new Dictionary<string, int>(a); b[\"y\"] = 2; return a.Count + \"|\" + b.Count;")]
    [InlineData("var a = new Dictionary<string, int> { [\"x\"] = 1 }; var b = new Dictionary<string, int>(a) { [\"y\"] = 2 }; return string.Join(\",\", b.Keys);")]
    // Clear frees every slot, so the next insertion takes the first.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; d.Remove(\"a\"); d.Clear(); d[\"z\"] = 1; d[\"y\"] = 2; return string.Join(\",\", d.Keys);")]
    public void Order_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A key comes back in its own type: numbers add, a long stays a long, a char is a char.
    [InlineData("var d = new Dictionary<int, int> { [3] = 30, [1] = 10 }; var total = 0; foreach (var k in d.Keys) total += k; return total;")]
    [InlineData("var d = new Dictionary<int, int> { [3] = 30, [1] = 10 }; var total = 0; foreach (var (k, v) in d) total += k * v; return total;")]
    [InlineData("var d = new Dictionary<long, int> { [9007199254740993L] = 1 }; var total = 0L; foreach (var k in d.Keys) total += k + 1; return total.ToString();")]
    [InlineData("var d = new Dictionary<char, int>(); d['b'] = 1; d['a'] = 2; var s = \"\"; foreach (var kv in d) s += kv.Key; return s;")]
    [InlineData("var d = new Dictionary<bool, int>(); d[true] = 1; d[false] = 0; var s = \"\"; foreach (var k in d.Keys) s += k + \";\"; return s;")]
    [InlineData("var d = new Dictionary<double, int>(); d[double.NaN] = 1; d[0.5] = 2; return d.ContainsKey(double.NaN) + \"|\" + d[0.5];")]
    // A key compares as .NET's default comparer compares it.
    [InlineData("var d = new Dictionary<DateTime, int>(); d[new DateTime(2026, 1, 1)] = 1; return d[new DateTime(2026, 1, 1)];")]
    [InlineData("var d = new Dictionary<decimal, int>(); d[1.50m] = 1; d[1.5m] = 2; return d.Count + \"|\" + d[1.500m];")]
    [InlineData("var d = new Dictionary<TimeSpan, string>(); d[TimeSpan.FromMinutes(90)] = \"a\"; return d.ContainsKey(TimeSpan.FromHours(1.5));")]
    [InlineData("var d = new Dictionary<DayOfWeek, int>(); d[DayOfWeek.Friday] = 5; d[DayOfWeek.Monday] = 1; return d[DayOfWeek.Friday] * 10 + d.Count;")]
    public void Keys_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // Count, ContainsKey and TryGetValue, after a removal too.
    [InlineData("var d = new Dictionary<int, string> { [3] = \"c\", [1] = \"a\" }; d.Remove(3); return d.Count;")]
    [InlineData("var d = new Dictionary<int, string> { [3] = \"c\" }; return d.ContainsKey(3) + \"|\" + d.ContainsKey(1);")]
    [InlineData("var d = new Dictionary<int, string> { [3] = \"c\" }; return (d.TryGetValue(3, out var a) ? a : \"-\") + (d.TryGetValue(1, out var b) ? b : \"-\");")]
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.GetValueOrDefault(\"a\") + d.GetValueOrDefault(\"z\") + d.GetValueOrDefault(\"z\", 40);")]
    // TryAdd keeps the value of a key already there.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var first = d.TryAdd(\"a\", 2); var second = d.TryAdd(\"b\", 3); return first + \"|\" + second + \"|\" + d[\"a\"] + \"|\" + d.Count;")]
    // ContainsValue, NaN included.
    [InlineData("var d = new Dictionary<string, double> { [\"a\"] = double.NaN, [\"b\"] = 2 }; return d.ContainsValue(double.NaN) + \"|\" + d.ContainsValue(2) + \"|\" + d.ContainsValue(3);")]
    // A lookup through the keys and their count.
    [InlineData("var d = new Dictionary<int, int> { [3] = 30, [1] = 10 }; return d.Keys.Contains(3) + \"|\" + d.Keys.Contains(2) + \"|\" + d.Keys.Count + \"|\" + d.Values.Count;")]
    // Remove with an out moves the value out, and a miss writes the default.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var hit = d.Remove(\"a\", out var v); var miss = d.Remove(\"a\", out var w); return hit + \"|\" + v + \"|\" + miss + \"|\" + w + \"|\" + d.Count;")]
    // The entry reads and writes through the class.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; d[\"a\"] += 2; d[\"a\"]++; return d[\"a\"];")]
    [InlineData("Dictionary<string, int>? d = new(); d?[\"a\"] = 5; return d[\"a\"];")]
    // A null key is refused by every member, in .NET's words (found in review, #443).
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; string k = null; var said = \"\"; try { d.ContainsKey(k); } catch (Exception e) { said += e.Message + \"|\"; } try { d.Remove(k); } catch (Exception e) { said += e.Message + \"|\"; } try { d.TryGetValue(k, out var v); } catch (Exception e) { said += e.Message + \"|\"; } try { d.Add(k, 2); } catch (Exception e) { said += e.Message + \"|\"; } try { d.TryAdd(k, 2); } catch (Exception e) { said += e.Message + \"|\"; } try { var x = d[k]; } catch (Exception e) { said += e.Message + \"|\"; } return said + d.Count;")]
    [InlineData("var d = new SortedDictionary<string, int> { [\"a\"] = 1 }; string k = null; var said = \"\"; try { d.ContainsKey(k); } catch (Exception e) { said += e.Message + \"|\"; } try { d.Add(k, 2); } catch (Exception e) { said += e.Message + \"|\"; } return said + d.Count;")]
    // A property pattern reads the dictionary's count.
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return (d is { Count: > 0 }) + \"|\" + (d is { Count: 0 });")]
    public void Members_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // ToDictionary builds the same dictionary, in the order of its source.
    [InlineData("var d = new[] { 3, 1, 2 }.ToDictionary(x => x, x => x * 10); return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new[] { 3, 1, 2 }.ToDictionary(x => x); var s = 0; foreach (var (k, v) in d) s = s * 10 + k; return s;")]
    [InlineData("var d = new[] { 3, 1, 2 }.ToDictionary(x => x, x => x * 10); d.Remove(3); d[7] = 70; return string.Join(\",\", d.Keys);")]
    // A key a plain object could not hold.
    [InlineData("var d = new[] { new DateTime(2026, 1, 2), new DateTime(2026, 1, 1) }.ToDictionary(x => x, x => x.Day); return d[new DateTime(2026, 1, 1)];")]
    [InlineData("var d = new[] { 1.5m, 2.25m }.ToDictionary(x => x, x => x.ToString()); return d[1.50m];")]
    public void ToDictionary_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A deconstructing foreach over a record-keyed dictionary and over a sorted one, which threw.
    [InlineData("var d = new Dictionary<Point, int> { { new Point(1, 2), 10 }, { new Point(3, 4), 20 } }; var s = 0; foreach (var (k, v) in d) s += k.X + v; return s;")]
    [InlineData("var d = new Dictionary<Point, int> { { new Point(1, 2), 10 }, { new Point(3, 4), 20 } }; d.Remove(new Point(1, 2)); d[new Point(5, 6)] = 30; var s = \"\"; foreach (var kv in d) s += kv.Key.X; return s;")]
    [InlineData("var d = new SortedDictionary<int, string> { { 2, \"b\" }, { 1, \"a\" } }; var s = \"\"; foreach (var (k, v) in d) s += k + v; return s;")]
    [InlineData("var source = new Dictionary<int, int> { [3] = 3, [1] = 1 }; var d = new SortedDictionary<int, int>(source); return string.Join(\",\", d.Keys);")]
    [InlineData("var d = new SortedList<string, int> { [\"b\"] = 2, [\"a\"] = 1 }; return d.ContainsValue(2) + \"|\" + string.Join(\",\", d.Values);")]
    [InlineData("var d = new SortedDictionary<string, int> { [\"b\"] = 2, [\"a\"] = 1 }; return d.ContainsValue(1) + \"|\" + d.ContainsValue(3);")]
    public void RecordKeyedAndSorted_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Point);
    }

    [SkippableTheory]
    // A dictionary returned whole crosses JSON as the object System.Text.Json writes for it: every key
    // but an integer-like one keeps its order (#437).
    [InlineData("var d = new Dictionary<string, int>(); d[\"b\"] = 2; d[\"a\"] = 1; return d;")]
    [InlineData("var d = new Dictionary<bool, string>(); d[true] = \"yes\"; d[false] = \"no\"; return d;")]
    [InlineData("var d = new Dictionary<string, int> { [\"__proto__\"] = 1, [\"a\"] = 2 }; return d;")]
    public void Json_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
