using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The tolerance the platform-difference conformance cases lean on, exercised where it can be.
///
/// <para>
/// `AssertStatementsWithinAnUlpOfDotNet` returns early when the two sides agree, and on macOS they
/// do for the one case that uses it — `double.RootN(27.0, 3)` is exactly 3 here and
/// 3.0000000000000004 on Linux. So on this machine the comparison never runs, and a suite that
/// only ran that case would be claiming a tolerance it had never exercised. This runs it.
/// </para>
/// </summary>
public class UlpComparisonTests
{
    [Fact]
    public void IdenticalValuesAreZeroUlpsApart()
    {
        ConformanceRunner.UlpsBetween(3.0, 3.0).Should().Be(0);
    }

    /// <summary>THE case: the exact pair the RootN fence is about, measured on the two platforms.</summary>
    [Fact]
    public void ThreeAndTheDoubleAboveIt_AreOneUlpApart()
    {
        ConformanceRunner.UlpsBetween(3.0, 3.0000000000000004).Should().Be(1,
            "3.0000000000000004 IS the next double after 3 — which is why one ULP is the whole "
            + "tolerance, and why a decimal epsilon would have been a bigger promise than intended");
        ConformanceRunner.UlpsBetween(3.0, double.BitIncrement(3.0)).Should().Be(1);
    }

    /// <summary>…and anything further is a defect, which is the half that makes the tolerance a
    /// tolerance rather than a pass.</summary>
    [Theory]
    [InlineData(3.0, 3.0000000000000009)]
    [InlineData(3.0, 3.01)]
    [InlineData(3.0, 4.0)]
    public void AnythingFurtherApartIsMoreThanOneUlp(double a, double b)
    {
        ConformanceRunner.UlpsBetween(a, b).Should().BeGreaterThan(1);
    }

    /// <summary>
    /// Where the spacing DOES change, which is not where this test first said it did.
    ///
    /// <para>
    /// It claimed a step down from 3 was narrower than a step up, and that is simply false: 3 sits
    /// inside the binade [2, 4), both neighbours are in it, and the spacing either side is the same
    /// 4.44e-16 — measured. The rule is about a POWER OF TWO, and 3 is not one. At 2 it is real:
    /// the step up is 4.44e-16 and the step down is half that, because below 2 the exponent drops.
    /// </para>
    ///
    /// <para>
    /// The original expectation was wrong for neither reason — I had guessed from how the two
    /// decimals LOOK — and then the comment explaining the corrected expectation taught a rule that
    /// does not exist. Caught in review, which is the shape of the defect this repository keeps
    /// finding: prose with a comment's authority, describing something that is not there.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSpacingChangesAtAPowerOfTwo_AndThreeIsNotOne()
    {
        (double.BitIncrement(3.0) - 3.0).Should().Be(3.0 - double.BitDecrement(3.0),
            "3 is inside a binade, so its neighbours are the same distance away on both sides");
        ConformanceRunner.UlpsBetween(3.0, double.BitDecrement(3.0)).Should().Be(1);
        ConformanceRunner.UlpsBetween(3.0, double.BitIncrement(3.0)).Should().Be(1);

        // …and at 2, which IS a power of two, the step down is half the step up.
        (2.0 - double.BitDecrement(2.0)).Should().Be((double.BitIncrement(2.0) - 2.0) / 2);
        // One ULP is still one ULP on each side: the COUNT is what the tolerance speaks in, which is
        // the whole reason it is expressed in bits rather than in a decimal epsilon.
        ConformanceRunner.UlpsBetween(2.0, double.BitDecrement(2.0)).Should().Be(1);
        ConformanceRunner.UlpsBetween(2.0, double.BitIncrement(2.0)).Should().Be(1);
    }

    /// <summary>
    /// The ends of the domain, where the obvious subtraction is wrong in two different ways.
    /// <para>
    /// `Math.Abs(left - right)` THREW on (-2, 2) — that difference is exactly `long.MinValue`, which
    /// has no positive counterpart — and wrapped the widest pair of all to about nine quadrillion,
    /// a small number that a tolerance would have swallowed. Both measured before the fix.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCountSurvivesTheEndsOfTheDomain()
    {
        ConformanceRunner.UlpsBetween(-2.0, 2.0).Should().BeGreaterThan(1,
            "this threw an OverflowException before the distance was computed unsigned");
        ConformanceRunner.UlpsBetween(-double.MaxValue, double.MaxValue).Should().Be(long.MaxValue,
            "the widest pair there is saturates rather than wrapping to something small");
        ConformanceRunner.UlpsBetween(double.NegativeInfinity, double.PositiveInfinity)
            .Should().BeGreaterThan(1);
        // …and it is symmetric, which the signed version also was not at the edges.
        ConformanceRunner.UlpsBetween(-2.0, 2.0).Should().Be(ConformanceRunner.UlpsBetween(2.0, -2.0));
    }

    /// <summary>Across ZERO, where a naive bit comparison reads two adjacent values as astronomically
    /// far apart: -0.0 and +0.0 are the same number and their bit patterns differ by a sign bit.</summary>
    [Fact]
    public void TheComparisonMeansTheSameThingAroundZero()
    {
        ConformanceRunner.UlpsBetween(-0.0, 0.0).Should().Be(0);
        ConformanceRunner.UlpsBetween(double.BitDecrement(0.0), double.BitIncrement(0.0))
            .Should().Be(2, "one step either side of zero, and zero itself between them");
    }

    [Fact]
    public void NaNIsNotWithinAnUlpOfAnything()
    {
        ConformanceRunner.UlpsBetween(double.NaN, 3.0).Should().Be(long.MaxValue);
        ConformanceRunner.UlpsBetween(double.NaN, double.NaN).Should().Be(0);
    }
}
