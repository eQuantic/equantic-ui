using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// What a value tuple's and an anonymous object's object members lower to, from the static type, which
/// knows a tuple's element types and an anonymous type's C# member names in their order (#743, #762).
/// What each lowering hands the runtime is pinned here; what the runtime answers with it, beside .NET's
/// answer, is the conformance suite's (<c>TupleAndAnonymousObjectConformanceTests</c>).
/// </summary>
public class TupleAndAnonymousObjectLoweringTests
{
    [Fact]
    public void ATuplesText_HandsWhatEachElementsTypeSays()
    {
        // An int's kind, a float's own digits; a string says nothing, and nothing past the last element
        // that says anything is written.
        TestHelper.ConvertExpression("(1, \"a\").ToString()").Should().Be("$eq.text.tuple([1, 'a'], ['int32'])");
        TestHelper.ConvertExpression("(\"a\", 1.5).ToString()").Should().Be("$eq.text.tuple(['a', 1.5])");
        TestHelper.ConvertExpression("(\"a\", 0.1f).ToString()").Should().Be("$eq.text.tuple(['a', Math.fround(0.1)], [null, 'single'])");
        // A nested tuple and an anonymous object are written by a function of the value.
        TestHelper.ConvertExpression("(\"a\", (\"b\", \"c\")).ToString()")
            .Should().Be("$eq.text.tuple(['a', ['b', 'c']], [null, ($v) => $eq.text.tuple($v)])");
        TestHelper.ConvertExpression("(\"a\", new { B = \"b\" }).ToString()")
            .Should().Be("$eq.text.tuple(['a', { b: 'b' }], [null, ($v) => $eq.text.record($v, '', ['B'])])");
    }

    [Fact]
    public void AnAnonymousObjectsText_IsARecordsWithNoName()
    {
        TestHelper.ConvertExpression("new { R = 255, Name = \"a\" }.ToString()")
            .Should().Be("$eq.text.record({ r: 255, name: 'a' }, '', [['R', 'int32'], 'Name'])");
        TestHelper.ConvertExpression("new { }.ToString()").Should().Be("$eq.text.record({}, '', [])");
    }

    [Fact]
    public void ATupleOnItsWayIntoText_IsWrittenThere_AndNotCopiedAroundItsText()
    {
        // A tuple boxed into a concatenation is read where it stands: the copy a boxing takes, taken
        // around the text, spread the string into its characters.
        var js = TestHelper.ConvertExpression("{ var t = (1, \"a\"); var s = \"x\" + t + $\"{t}|{t,8}|{t:N2}\" + string.Format(\"{0,5}\", t); }");
        js.Should().Contain("'x' + $eq.text.tuple(t, ['int32'])");
        js.Should().Contain("${$eq.text.tuple(t, ['int32'])}|");
        js.Should().Contain("${$eq.text.format($eq.text.tuple(t, ['int32']), null, 8)}");
        js.Should().NotContain("[...$eq.text");
        js.Should().NotContain("'N2'");
    }

    [Fact]
    public void AnAnonymousObjectsEquals_IsTheComparisonGeneratedFromItsMembers()
    {
        // Always the generated one, which takes only another anonymous object of the same members in the
        // same order: $eq.equals matched two plain objects by their keys in any order.
        TestHelper.ConvertExpression("new { R = 1, S = \"a\" }.Equals(new { R = 1, S = \"a\" })")
            .Should().Be("$eq.collections.memberEquality({ r: false, s: false })({ r: 1, s: 'a' }, { r: 1, s: 'a' })");
        TestHelper.ConvertExpression("new { M = 1.5m }.Equals(null)")
            .Should().Be("$eq.collections.memberEquality({ m: true })({ m: $eq.num.dec(\"1.5\") }, null)");
    }

    [Fact]
    public void DistinctOverAnonymousObjects_KeepsTheFirstOfEachByTheirTypesEquality()
    {
        // A Set kept every anonymous object, each a reference of its own.
        TestHelper.ConvertExpression("new[] { new { A = 1 } }.Distinct()")
            .Should().Be("[...$eq.collections.hashSet(true, [{ a: 1 }])]");
        TestHelper.ConvertExpression("new[] { new { L = new[] { 1 } } }.Distinct()")
            .Should().Be("[...$eq.collections.hashSet($eq.collections.memberEquality({ l: false }), [{ l: [1] }])]");
    }

    [Fact]
    public void AnAnonymousObject_ReachesStringFormatAsItsText()
    {
        TestHelper.ConvertExpression("{ var s = string.Format(\"{0,5}\", new { A = 1 }); }")
            .Should().Contain("$eq.text.stringFormat('{0,5}', $eq.text.record({ a: 1 }, '', [['A', 'int32']]))");
    }

    [Fact]
    public void ATuplesEquals_IsEqEquals_UnlessAnElementComparesOtherwise()
    {
        TestHelper.ConvertExpression("(1, \"a\").Equals((1, \"a\"))").Should().Be("$eq.equals([1, 'a'], [1, 'a'])");
        TestHelper.ConvertExpression("(new[] { 1 }, 1).Equals((new[] { 1 }, 1))")
            .Should().Be("$eq.collections.tupleEquality(false, false)([[1], 1], [[1], 1])");
    }

    [Fact]
    public void TheirMethodGroups_AreTheDelegatesTheirCallsAre()
    {
        var js = TestHelper.ConvertExpression(
            "{ var t = (1, new[] { 2 }); var a = new { R = 1 }; Func<object, bool> te = t.Equals; Func<string> tt = t.ToString; "
            + "Func<object, bool> ae = a.Equals; Func<string> at = a.ToString; }");
        js.Should().Contain("$eq.equalsGroup([...t], $eq.collections.tupleEquality(false, false))");
        js.Should().Contain("$eq.text.tupleGroup([...t], ['int32'])");
        js.Should().Contain("$eq.equalsGroup(a, $eq.collections.memberEquality({ r: false }))");
        js.Should().Contain("$eq.text.recordGroup(a, '', [['R', 'int32']])");
        js.Should().NotContain(".bind(");
    }

    [Fact]
    public void NoneOfTheseConstructs_IsRefused()
    {
        TestHelper.DiagnosticsFor(
                "{ var a = new { R = 1 }; Func<object, bool> e = a.Equals; Func<string> f = a.ToString; var b = a with { R = 2 }; "
                + "(int, int)? n = (1, 2); var same = n.Equals((1, 2)); var t = (1, 2); Func<string> g = t.ToString; "
                + "var z = new[] { 1 }.Zip(new[] { 2 }).First().ToString(); var s = string.Format(\"{0,5}\", t) + $\"{t,3}\" + a; }")
            .Should().NotContain(diagnostic => diagnostic.Severity == eQuantic.UI.Compiler.CodeGen.ConversionSeverity.Error);
    }
}
