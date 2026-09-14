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
    /// A step DOWN is not the same size as a step up, and this test had it wrong first:
    /// 2.9999999999999996 looks further from 3 than 3.0000000000000004 does and is exactly as far,
    /// one ULP, because the exponent drops below 3 and the spacing halves. Written out because a
    /// reader eyeballing decimals would make the same mistake, and because it is the reason the
    /// tolerance is expressed in bits rather than in digits at all.
    /// </summary>
    [Fact]
    public void TheStepBelowAValueIsNarrowerThanTheStepAbove()
    {
        double.BitDecrement(3.0).Should().Be(2.9999999999999996);
        ConformanceRunner.UlpsBetween(3.0, 2.9999999999999996).Should().Be(1);
        ConformanceRunner.UlpsBetween(3.0, double.BitDecrement(double.BitDecrement(3.0)))
            .Should().Be(2);
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
