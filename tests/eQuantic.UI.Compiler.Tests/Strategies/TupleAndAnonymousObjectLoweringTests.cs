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
        // Its ToString is an instance call: the receiver is refused through null, where a concatenation of
        // a null one writes nothing.
        TestHelper.ConvertExpression("new { R = 255, Name = \"a\" }.ToString()")
            .Should().Be("$eq.text.record($eq.receiver({ r: 255, name: 'a' }), '', [['R', 'int32'], 'Name'])");
        TestHelper.ConvertExpression("new { }.ToString()").Should().Be("$eq.text.record($eq.receiver({}), '', [])");
        TestHelper.ConvertExpression("{ var a = new { R = 1 }; var s = \"x\" + a; }")
            .Should().Contain("'x' + $eq.text.record(a, '', [['R', 'int32']])");
    }

    [Fact]
    public void AValueWhoseTextIsItsTypesName_WritesTheName()
    {
        // An array, a list, a delegate: Object.ToString writes the runtime type's name, which only the
        // static type says here; a null one writes nothing, and an instance call through null throws.
        TestHelper.ConvertExpression("{ var xs = new[] { 1 }; var s = \"x\" + xs; }")
            .Should().Contain("'x' + (xs == null ? '' : 'System.Int32[]')");
        TestHelper.ConvertExpression("{ var xs = new List<int>(); var s = (xs, 1).ToString(); }")
            .Should().Contain("$eq.text.tuple([xs, 1], [($v) => $v == null ? '' : 'System.Collections.Generic.List`1[System.Int32]', 'int32'])");
        TestHelper.ConvertExpression("{ Func<int, string> f = n => \"\"; var s = f.ToString(); }")
            .Should().Contain("$eq.receiver(f) == null ? '' : 'System.Func`2[System.Int32,System.String]'");
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
        // An instance call, whose receiver is refused through null after the argument is evaluated.
        TestHelper.ConvertExpression("new { R = 1, S = \"a\" }.Equals(new { R = 1, S = \"a\" })")
            .Should().Be("$eq.equalsInstance({ r: 1, s: 'a' }, { r: 1, s: 'a' }, $eq.collections.memberEquality({ r: false, s: false }))");
        TestHelper.ConvertExpression("new { M = 1.5m }.Equals(null)")
            .Should().Be("$eq.equalsInstance({ m: $eq.num.dec(\"1.5\") }, null, $eq.collections.memberEquality({ m: true }))");
    }

    [Fact]
    public void AnotherTypeBeforeTheBoxing_IsUnequal_AndStillEvaluated()
    {
        // The other value's type before C# boxed it says what the browser cannot: another anonymous or
        // tuple type is unequal, whatever the members hold.
        TestHelper.ConvertExpression("new { A = (byte)1 }.Equals(new { A = 1 })")
            .Should().Be("$eq.equalsInstance({ a: 1 }, { a: 1 }, null)");
        TestHelper.ConvertExpression("((byte)1, 2).Equals((object)(1, 2))")
            .Should().Be("$eq.equalsInstance([1, 2], [...[1, 2]], null)");
        // The same type under other element names is the same type; an object compares at run time.
        TestHelper.ConvertExpression("{ (int X, int Y) p = (1, 2); var b = (1, 2).Equals((object)p); }")
            .Should().Contain("$eq.equals([1, 2], [...p])");
        TestHelper.ConvertExpression("{ object o = 1; var b = new { A = 1 }.Equals(o); }")
            .Should().Contain("$eq.equalsInstance({ a: 1 }, o, $eq.collections.memberEquality({ a: false }))");
        // An empty Nullable tuple equals only null, both values evaluated.
        TestHelper.ConvertExpression("{ (int, int)? n = null; var b = n.Equals((object)(1.0, 2.0)); }")
            .Should().Contain("$eq.logic.and(n == null, [...[1.0, 2.0]] == null)");
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
        js.Should().Contain("$eq.text.tupleGroup([...t], ['int32', ($v) => $v == null ? '' : 'System.Int32[]'])");
        // An anonymous object's are refused where they are made when it is null; a tuple, a value, is never null.
        js.Should().Contain("$eq.equalsGroup($eq.receiver(a), $eq.collections.memberEquality({ r: false }))");
        js.Should().Contain("$eq.text.recordGroup($eq.receiver(a), '', [['R', 'int32']])");
        js.Should().NotContain(".bind(");
    }

    [Fact]
    public void NoneOfTheseConstructs_IsRefused()
    {
        TestHelper.DiagnosticsFor(
                "{ var a = new { R = 1 }; Func<object, bool> e = a.Equals; Func<string> f = a.ToString; var b = a with { R = 2 }; "
                + "(int, int)? n = (1, 2); var same = n.Equals((1, 2)); var t = (1, 2); Func<string> g = t.ToString; "
                + "var z = new[] { 1 }.Zip(new[] { 2 }).First().ToString(); var s = string.Format(\"{0,5}\", t) + $\"{t,3}\" + a; "
                + "var oe = object.Equals(1, 1) && Object.Equals(a, b) && System.Object.Equals(t, n); "
                + "var l = new List<int>(); var le = l.Equals((object)l); Func<object, bool> lg = l.Equals; }")
            .Should().NotContain(diagnostic => diagnostic.Severity == eQuantic.UI.Compiler.CodeGen.ConversionSeverity.Error);
    }

    /// <summary>
    /// The static <c>object.Equals</c> answers as its FIRST parameter's type compares, read before C# boxed
    /// it, the second one's type asked only where the first one's says nothing; a value of a type that
    /// refuses every other beside one of another type is unequal before either is read (Copilot's review of
    /// #947).
    /// </summary>
    [Fact]
    public void StaticObjectEquals_ComparesAsItsFirstParameter_AndReadsBothTypes()
    {
        TestHelper.ConvertExpression("object.Equals(1, 1)").Should().Be("$eq.equals(1, 1)");
        TestHelper.ConvertExpression("Object.Equals(\"a\", null)").Should().Be("$eq.equals('a', null)");
        TestHelper.ConvertExpression("System.Object.Equals((new[] { 1 }, 1), (new[] { 1 }, 1))")
            .Should().Be("$eq.collections.tupleEquality(false, false)([...[[1], 1]], [...[[1], 1]])");
        TestHelper.ConvertExpression("object.Equals(new { A = (byte)1 }, new { A = 1 })")
            .Should().Be("$eq.logic.and({ a: 1 } == null, { a: 1 } == null)");
        // A byte and an int, a char and a string: two types the browser holds alike.
        TestHelper.ConvertExpression("object.Equals((byte)1, 1)").Should().Be("$eq.logic.and(1 == null, 1 == null)");
        TestHelper.ConvertExpression("object.Equals('a', \"a\")").Should().Be("$eq.logic.and('a' == null, 'a' == null)");
        // A list, by reference, whichever operand says so.
        TestHelper.ConvertExpression("object.Equals(numbers, numbers)")
            .Should().Be("$eq.collections.sameItem(this.numbers, this.numbers)");
        TestHelper.ConvertStatement("{ object o = Name; var same = object.Equals(o, numbers); }")
            .Should().Contain("$eq.collections.sameItem(o, this.numbers)");
        // The parameters' order, the written order of evaluation.
        TestHelper.ConvertExpression("object.Equals(objB: Name, objA: numbers)")
            .Should().Be("(($0, $1) => $eq.collections.sameItem($1, $0))(this.name, this.numbers)");
    }

    [Fact]
    public void GetType_IsRefusedOnceAcrossEveryForm()
    {
        foreach (var snippet in new[]
        {
            "var t = \"a\".GetType();",
            "var n = \"a\".GetType().Name;",
            "{ string? s = \"a\"; var n = s?.GetType().Name; }",
            "Func<Type> f = \"a\".GetType;",
            "var t = GetType();",
            "Func<Type> f = GetType;",
        })
        {
            var diagnostics = TestHelper.DiagnosticsFor(snippet);
            diagnostics.Should().ContainSingle(d => d.Code == "EQ2004", $"`{snippet}` must report EQ2004 once");
        }
    }
}
