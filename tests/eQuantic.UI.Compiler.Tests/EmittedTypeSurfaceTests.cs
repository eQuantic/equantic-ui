using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What the emitted TypeScript SAYS a value is, against what the runtime actually hands over.
/// <para>
/// An annotation is not decoration here: the transpiled modules are typechecked by the runtime's
/// own build, and an app's editor reads the generated factory surface. A wrong one is a lie the
/// rest of the file then typechecks against — the `number` these cases pin used to sit over a
/// `bigint` and over a class with methods on it, so `unit.mul(...)` was reachable on a "number".
/// </para>
/// </summary>
public class EmittedTypeSurfaceTests
{
    [Fact]
    public void ALongIsABigint_NotANumber()
    {
        var ts = TestHelper.ConvertClass("""
            public long Ticks { get; init; }
            public long Doubled(long n) => n * 2;
            """);

        // The literal the emitter writes for the field IS a bigint, and `n * 2` emits `n * 2n`.
        ts.Should().Contain("ticks: bigint = $eq.num.long(0)");
        ts.Should().Contain("doubled(n: bigint)");
        ts.Should().NotContain(": number = $eq.num.long");
    }

    [Fact]
    public void ADecimalIsTheRuntimeClass_AndArrivesImported()
    {
        var ts = TestHelper.ConvertClass("""
            public decimal Price { get; init; }
            public decimal Total(decimal unit, int count) => unit * count;
            """);

        ts.Should().Contain("price: Decimal = $eq.num.dec(0)");
        ts.Should().Contain("total(unit: Decimal, count: number)");
        // The name the TRANSLATION invents — no syntax walk can see it, so the import is the half
        // of this fix that a mapping change alone would have missed.
        ts.Should().Contain("import { $eq, Decimal } from \"@equantic/runtime\"");
    }

    [Fact]
    public void ADateTimeIsTheRuntimeTicks_NotTheBrowsersDate()
    {
        var ts = TestHelper.ConvertClass("""
            public DateTime When { get; init; }
            public DateTime Later(DateTime from) => from.AddDays(1);
            """);

        ts.Should().Contain("later(from: DateTime)");
        // `Date` on its own, which is a DIFFERENT class — the negative has to stop short of the
        // name it is a prefix of, or it passes on the very output it is meant to reject.
        Regex.IsMatch(ts, @":\s*Date(?![A-Za-z])").Should().BeFalse("the JS Date is not this type");
    }

    /// <summary>
    /// An element that is a union or a function is parenthesized before its array: TypeScript binds
    /// `[]` tighter than `|` and `=>`, so `string | null[]` is a string OR an array of nulls, and
    /// `() => void[]` a function returning an array. The code engine's row map keeps a
    /// <c>List&lt;string?&gt;</c>, and its twin failed the runtime's own build at the first `push`.
    /// </summary>
    [Fact]
    public void AUnionOrAFunctionElement_IsParenthesizedBeforeItsArray()
    {
        var ts = TestHelper.ConvertClass("""
            private readonly List<string?> _labels = new();
            public string?[] Names { get; init; } = [];
            public IReadOnlyList<int?> Counts { get; init; } = [];
            public List<Action> Callbacks { get; init; } = new();
            public List<Action?> Handlers { get; init; } = new();
            public string?[][] Grid { get; init; } = [];
            public List<string> Plain { get; init; } = new();
            """);

        ts.Should().Contain("_labels: (string | null)[]");
        ts.Should().Contain("names: (string | null)[]");
        ts.Should().Contain("counts: (number | null)[]");
        ts.Should().Contain("callbacks: (() => void)[]");
        ts.Should().Contain("handlers: ((() => void) | null)[]");
        ts.Should().Contain("grid: (string | null)[][]");
        ts.Should().Contain("plain: string[]", "an element that is a plain name needs nothing");
        ts.Should().NotContain("| null[]");
    }

    /// <summary>
    /// An ordering's key selector is typed by the element it compares. It was written to a bare
    /// <c>const _k = (filler) => …</c> inside the comparator, where nothing gives the lambda a type,
    /// and the runtime's own build refused the implicit <c>any</c> (the code engine's row map sorts
    /// its fillers). Typed through the comparator's own parameter, the key is checked again. Plain
    /// JavaScript, which the conformance harness and the playground run, carries no annotation.
    /// </summary>
    [Fact]
    public void AnOrderingsKeySelector_IsTypedByTheElementItCompares()
    {
        var ts = TestHelper.ConvertClass("""
            public List<string> Sorted(List<string> items) => items.OrderBy(item => item.Length).ThenBy(item => item).ToList();
            """);

        // The lambda's own annotation, when it has one, is the same type: the key is typed either way.
        System.Text.RegularExpressions.Regex.Matches(ts, @"const _k: \(x: typeof a\) => any = \(item(: string)?\) => item")
            .Count.Should().Be(2, "both keys of the ordering are typed through the element they compare");
        TestHelper.ConvertExpression("new List<string>().OrderBy(item => item.Length)")
            .Should().Contain("const _k = (item) => item.length;").And.NotContain("typeof a");
    }

    [Fact]
    public void TheWrappingSurvivesTheMapping()
    {
        // The name arrives inside arrays and sequences too, and the import has to follow it there.
        var ts = TestHelper.ConvertClass("""
            public decimal[] Prices { get; init; } = [];
            public long[] Stamps { get; init; } = [];
            """);

        ts.Should().Contain("prices: Decimal[]");
        ts.Should().Contain("stamps: bigint[]");
        ts.Should().Contain("Decimal } from \"@equantic/runtime\"");
    }
}
