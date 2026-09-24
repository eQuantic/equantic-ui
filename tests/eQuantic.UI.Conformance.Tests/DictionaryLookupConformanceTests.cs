using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A dictionary LOOKUP — TryGetValue and GetValueOrDefault, on both lowerings: the plain object and
/// the runtime map a sorted or value-keyed dictionary becomes — evaluates what C# evaluates, once,
/// in the order it was written, and answers what .NET answers: <c>default(TValue)</c> on a miss, a
/// prototype member as a miss. Each case puts a count or an effect where a second evaluation, a
/// skipped one, or a wrong answer shows.
/// </summary>
public class DictionaryLookupConformanceTests
{
    private const string Point = "public record Point(int X, int Y);";

    [SkippableTheory]
    // ---- the key and the receiver are evaluated once ----
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; string Key() { calls++; return \"a\"; } d.TryGetValue(Key(), out var v); return v + \"/\" + calls;")] // "1/1"
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; Dictionary<string, int> Get() { calls++; return d; } Get().TryGetValue(\"a\", out var v); return v + \"/\" + calls;")] // "1/1"
    [InlineData("int calls = 0; var m = new SortedDictionary<string, int> { [\"a\"] = 1 }; string Key() { calls++; return \"a\"; } m.TryGetValue(Key(), out var v); return v + \"/\" + calls;")] // "1/1"
    [InlineData("int calls = 0; var m = new SortedDictionary<string, int> { [\"a\"] = 1 }; SortedDictionary<string, int> Get() { calls++; return m; } Get().TryGetValue(\"a\", out var v); return v + \"/\" + calls;")] // "1/1"
    // ---- a miss leaves default(TValue) in the out, whatever it held ----
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var hit = d.TryGetValue(\"z\", out var v); return (hit ? \"t\" : \"f\") + v;")] // "f0"
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; int v = 5; d.TryGetValue(\"z\", out v); return v;")] // 0
    [InlineData("var d = new Dictionary<string, string> { [\"a\"] = \"x\" }; string v = \"old\"; d.TryGetValue(\"z\", out v); return v;")] // null
    [InlineData("var d = new Dictionary<string, bool>(); d.TryGetValue(\"z\", out var v); return v;")] // false
    [InlineData("var m = new SortedDictionary<string, int> { [\"a\"] = 1 }; m.TryGetValue(\"z\", out var v); return v;")] // 0
    // The counting idiom reads the out after a miss: undefined + 1 stored NaN.
    [InlineData("var counts = new Dictionary<string, int>(); foreach (var w in new[] { \"a\", \"b\", \"a\" }) { counts.TryGetValue(w, out var n); counts[w] = n + 1; } return counts[\"a\"] * 10 + counts[\"b\"];")] // 21
    // ---- an own key only: a prototype member is a miss ----
    [InlineData("var d = new Dictionary<string, int>(); return d.TryGetValue(\"toString\", out var v) ? 1 : 0;")] // 0
    // ---- a discard receives nothing, and is not a variable ----
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.TryGetValue(\"a\", out _);")] // true
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.TryGetValue(\"a\", out var _);")] // true
    [InlineData("var m = new SortedDictionary<string, int> { [\"a\"] = 1 }; return m.TryGetValue(\"a\", out int _);")] // true
    // ---- an element as the out: its index steps once, on a hit and on a miss ----
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 4 }; var arr = new int[2]; int i = 0; d.TryGetValue(\"a\", out arr[i++]); return arr[0] * 10 + arr[1] + i * 100;")] // 140
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 4 }; var arr = new[] { 7, 7 }; int i = 0; d.TryGetValue(\"z\", out arr[i++]); return arr[0] * 10 + arr[1] + i * 100;")] // 107
    // ---- named arguments land in their parameters, and run in the order written ----
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 3 }; return d.TryGetValue(value: out var v, key: \"a\") ? v : -1;")] // 3
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 3 }; var arr = new int[2]; int i = 0; string log = \"\"; string K() { log += i; return \"a\"; } d.TryGetValue(value: out arr[i++], key: K()); return log + \"/\" + arr[0];")] // "1/3"
    public void TryGetValue_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A field as the out is written through its object: the source text `Field` was
    /// emitted as an undeclared name, a ReferenceError at the first hit.</summary>
    [SkippableFact]
    public void TryGetValue_IntoAField_WritesTheField()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var h = new Holder(); var d = new Dictionary<string, int> { [\"a\"] = 4 }; var hit = h.Probe(d); return hit + \"/\" + h.Field;", // "True/4"
            "public record Holder { public int Field; public bool Probe(Dictionary<string, int> d) => d.TryGetValue(\"a\", out Field); }");
    }

    [SkippableTheory]
    // A value-keyed dictionary is a runtime map too, and its miss is default(TValue) as well.
    [InlineData("var d = new Dictionary<Point, int> { { new Point(1, 2), 1 } }; var hit = d.TryGetValue(new Point(9, 9), out var v); return (hit ? \"t\" : \"f\") + v;")] // "f0"
    [InlineData("int calls = 0; var d = new Dictionary<Point, int> { { new Point(1, 2), 5 } }; Point Key() { calls++; return new Point(1, 2); } d.TryGetValue(Key(), out var v); return v + \"/\" + calls;")] // "5/1"
    public void TryGetValue_OnAValueKeyedDictionary_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Point);
    }

    [SkippableTheory]
    // ---- a miss answers default(TValue), a prototype member included ----
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.GetValueOrDefault(\"z\");")] // 0
    [InlineData("var d = new Dictionary<string, int>(); return d.GetValueOrDefault(\"toString\");")] // 0
    [InlineData("var m = new SortedDictionary<string, long>(); return (m.GetValueOrDefault(\"z\") + 1).ToString();")] // "1"
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; return d.GetValueOrDefault(\"z\", -1);")] // -1
    // A STORED null is a hit: `?? default` answered the default for it.
    [InlineData("var m = new SortedDictionary<string, string?> { [\"a\"] = null }; return m.GetValueOrDefault(\"a\", \"x\");")] // null
    // ---- the key and the receiver are evaluated once ----
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; string Key() { calls++; return \"a\"; } var r = d.GetValueOrDefault(Key()); return r + \"/\" + calls;")] // "1/1"
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; Dictionary<string, int> Get() { calls++; return d; } var r = Get().GetValueOrDefault(\"a\", -1); return r + \"/\" + calls;")] // "1/1"
    // ---- an explicit default is evaluated once, hit or miss, after the key unless named first ----
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; int Fallback() { calls++; return 7; } var r = d.GetValueOrDefault(\"a\", Fallback()); return r + \"/\" + calls;")] // "1/1"
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1 }; int Fallback() { calls++; return 7; } var r = d.GetValueOrDefault(\"z\", Fallback()); return r + \"/\" + calls;")] // "7/1"
    [InlineData("int calls = 0; var m = new SortedDictionary<string, int> { [\"a\"] = 1 }; int Fallback() { calls++; return 7; } var r = m.GetValueOrDefault(\"a\", Fallback()); return r + \"/\" + calls;")] // "1/1"
    [InlineData("string log = \"\"; var d = new Dictionary<string, int> { [\"a\"] = 1 }; string NextKey() { log += \"k\"; return \"a\"; } int Fallback() { log += \"d\"; return 7; } d.GetValueOrDefault(NextKey(), Fallback()); return log;")] // "kd"
    [InlineData("string log = \"\"; var d = new Dictionary<string, int> { [\"a\"] = 1 }; string NextKey() { log += \"k\"; return \"a\"; } int Fallback() { log += \"d\"; return 7; } d.GetValueOrDefault(defaultValue: Fallback(), key: NextKey()); return log;")] // "dk"
    public void GetValueOrDefault_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // The receiver once, not once more per key it deletes.
    [InlineData("int calls = 0; var d = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; Dictionary<string, int> Get() { calls++; return d; } Get().Clear(); return d.Count + \"/\" + calls;")] // "0/1"
    // A dictionary called `k` was shadowed by the arrow's parameter and kept every key.
    [InlineData("var k = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; k.Clear(); return k.Count;")] // 0
    public void Clear_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
