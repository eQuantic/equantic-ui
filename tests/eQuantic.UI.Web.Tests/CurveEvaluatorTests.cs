using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A <see cref="Curve"/> is a CSS cubic-bezier, and this pins that the evaluator agrees with what
/// a browser does with the same four numbers — because the web realizer hands those numbers to CSS
/// and Photon now evaluates them itself, and one authored intent must move the same way on both.
/// </summary>
public class CurveEvaluatorTests
{
    private static readonly Curve Linear = new(0, 0, 1, 1);

    [Fact]
    public void EndpointsArePinned_AndTimeOutsideTheRangeClamps()
    {
        foreach (var curve in new[] { Curve.Standard, Curve.Decelerate, Curve.Accelerate, Linear })
        {
            curve.Ease(0).Should().Be(0);
            curve.Ease(1).Should().Be(1);
            curve.Ease(-0.5f).Should().Be(0, "less begun than begun is begun");
            curve.Ease(1.5f).Should().Be(1, "time past the end is the end");
        }
    }

    [Fact]
    public void LinearIsTheIdentity()
    {
        Linear.Ease(0.25f).Should().BeApproximately(0.25f, 1e-6f);
        Linear.Ease(0.5f).Should().BeApproximately(0.5f, 1e-6f);
        Linear.Ease(0.9f).Should().BeApproximately(0.9f, 1e-6f);
    }

    [Theory]
    // Reference values come from an INDEPENDENT solver (a 60-step bisection over the same bezier,
    // written separately from the evaluator), not from memory: the first draft of this test carried
    // three numbers recalled as "what a browser gives" and all three were wrong by ~0.02 — the
    // evaluator was right and the instrument was not. cubic-bezier(.2,0,0,1) at these times:
    [InlineData(0.25f, 0.6072f)]
    [InlineData(0.50f, 0.8778f)]
    [InlineData(0.75f, 0.9755f)]
    public void StandardCurve_MatchesAnIndependentSolver(float t, float expected)
    {
        Curve.Standard.Ease(t).Should().BeApproximately(expected, 0.0005f);
    }

    [Fact]
    public void DecelerateFrontLoads_AccelerateBackLoads()
    {
        // The two entrance/exit curves are mirror images in intent: decelerate has done most of its
        // travel by the midpoint; accelerate has barely started.
        Curve.Decelerate.Ease(0.5f).Should().BeGreaterThan(0.85f);
        Curve.Accelerate.Ease(0.5f).Should().BeLessThan(0.45f);
    }

    [Fact]
    public void EveryNamedCurveIsMonotonic()
    {
        // Time only moves forward; an easing that went backwards would show as a flicker.
        foreach (var curve in new[] { Curve.Standard, Curve.Decelerate, Curve.Accelerate })
        {
            var previous = 0f;
            for (var i = 1; i <= 100; i++)
            {
                var value = curve.Ease(i / 100f);
                value.Should().BeGreaterThanOrEqualTo(previous - 1e-5f, $"{curve} at {i}%");
                previous = value;
            }
        }
    }
}
