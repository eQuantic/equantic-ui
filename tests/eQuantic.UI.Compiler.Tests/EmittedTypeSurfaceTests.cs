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
