using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for dictionaries keyed by a structural value type — <c>Dictionary&lt;Point, V&gt;</c>
/// with <c>record Point(int X, int Y)</c>. These route to the runtime <c>$eq.collections.valueMap</c>
/// (structural-equality keys) instead of the plain-object form, so two equal-but-distinct keys collide
/// exactly as in .NET. Also guards that the common string/number-keyed dictionary is left untouched.
/// </summary>
public class RecordKeyedDictionaryConformanceTests
{
    private const string Point = "public record Point(int X, int Y);";

    [SkippableTheory]
    // Indexer set then get — a fresh, structurally-equal key reads back the value (10).
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 10; return d[new Point(1, 2)];")]
    // Structurally-equal keys collapse onto one entry (overwrite) → Count 1.
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 10; d[new Point(1, 2)] = 20; return d.Count;")]
    // Distinct keys are kept apart → Count 2.
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 1; d[new Point(3, 4)] = 2; return d.Count;")]
    // Compound assignment through the value indexer → 8.
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 5; d[new Point(1, 2)] += 3; return d[new Point(1, 2)];")]
    // Add then read → 7.
    [InlineData(
        "var d = new Dictionary<Point, int>(); d.Add(new Point(1, 2), 7); return d[new Point(1, 2)];")]
    // Collection-initializer construction → 200.
    [InlineData(
        "var d = new Dictionary<Point, int> { { new Point(1, 2), 100 }, { new Point(3, 4), 200 } }; return d[new Point(3, 4)];")]
    // GetValueOrDefault present (5) / absent with explicit default (-1).
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 5; return d.GetValueOrDefault(new Point(1, 2));")]
    [InlineData(
        "var d = new Dictionary<Point, int>(); return d.GetValueOrDefault(new Point(9, 9), -1);")]
    // Keys / Values materialise to real sequences (2, 30).
    [InlineData(
        "var d = new Dictionary<Point, int> { { new Point(1, 2), 10 }, { new Point(3, 4), 20 } }; return d.Keys.Count();")]
    [InlineData(
        "var d = new Dictionary<Point, int> { { new Point(1, 2), 10 }, { new Point(3, 4), 20 } }; return d.Values.Sum();")]
    // foreach yields KeyValuePair-shaped entries (Key/Value) → (1+10)+(3+20) = 34.
    [InlineData(
        "var d = new Dictionary<Point, int> { { new Point(1, 2), 10 }, { new Point(3, 4), 20 } }; var s = 0; foreach (var kvp in d) { s += kvp.Key.X + kvp.Value; } return s;")]
    public void RecordKeyedDictionary_Int_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Point);
    }

    [SkippableTheory]
    // ContainsKey / Remove return the .NET booleans for present and absent keys.
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 1; return d.ContainsKey(new Point(1, 2));")]
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 1; return d.ContainsKey(new Point(9, 9));")]
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 1; d.Remove(new Point(1, 2)); return d.ContainsKey(new Point(1, 2));")]
    [InlineData(
        "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 1; return d.Remove(new Point(9, 9));")]
    [InlineData(
        "var d = new Dictionary<Point, int> { { new Point(1, 2), 1 } }; return d.TryGetValue(new Point(1, 2), out var v) ? v : -1;")]
    public void RecordKeyedDictionary_Bool_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Point);
    }

    // Value tuple keys are structural too — `(int, int)` keys must compare element-wise.
    [SkippableTheory]
    [InlineData(
        "var d = new Dictionary<(int, int), string>(); d[(1, 2)] = \"a\"; return d[(1, 2)];")]
    [InlineData(
        "var d = new Dictionary<(int, int), int>(); d[(1, 2)] = 1; d[(2, 1)] = 2; return d.Count;")]
    public void TupleKeyedDictionary_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Emit-shape guards: a value-typed key routes to <c>$eq.collections.valueMap</c>, while the common
    /// string/number key stays a plain object indexer — proving the new path doesn't disturb it.
    /// </summary>
    [Fact]
    public void EmitShape_ValueKeyRoutesToValueMap_StringKeyStaysPlainObject()
    {
        var recordKeyed = Transpiler.TranspileStatements(
            "var d = new Dictionary<Point, int>(); d[new Point(1, 2)] = 10; return d.Count;", Point);
        recordKeyed.Should().Contain("$eq.collections.valueMap");
        recordKeyed.Should().Contain(".set(");
        recordKeyed.Should().Contain(".size");

        var stringKeyed = Transpiler.TranspileExpression(
            "new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 } }[\"a\"]");
        stringKeyed.Should().NotContain("valueMap");
        stringKeyed.Should().NotContain(".get(");

        var intKeyed = Transpiler.TranspileStatements(
            "var d = new Dictionary<int, int>(); d[1] = 10; return d.Count;");
        intKeyed.Should().NotContain("valueMap");
        intKeyed.Should().Contain("Object.keys"); // .Count on a plain-object dictionary
    }
}
