using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The CONTROL LADDER is one table (spec A12). A Button, a text field, a select, a menu row and a
/// chip of the same size must measure the same — so the numbers live in <see cref="Sizing"/> and
/// every control READS them. These tests pin the ladder and pin the fact that Button no longer
/// owns a private copy of it.
/// </summary>
public class SizingLadderTests
{
    [Theory]
    [InlineData(SizeVariant.Small, 32, 12, 6, 13, 16, 10, 48)]
    [InlineData(SizeVariant.Medium, 40, 16, 8, 15, 20, 10, 48)]
    [InlineData(SizeVariant.Large, 48, 20, 8, 16, 20, 10, 48)]
    [InlineData(SizeVariant.XLarge, 56, 24, 10, 17, 24, 14, 56)]
    public void Ladder_MatchesSpec(SizeVariant size, float height, float padX, float gap,
        float label, float icon, float radius, float hit)
    {
        Sizing.Height(size).Should().Be(height);
        Sizing.PaddingX(size).Should().Be(padX);
        Sizing.Gap(size).Should().Be(gap);
        Sizing.LabelSize(size).Should().Be(label);
        Sizing.Icon(size).Should().Be(icon);
        Sizing.Radius(size).Should().Be(radius);
        Sizing.HitTarget(size).Should().Be(hit);
    }

    [Theory]
    [InlineData(SizeVariant.Small)]
    [InlineData(SizeVariant.Medium)]
    [InlineData(SizeVariant.Large)]
    [InlineData(SizeVariant.XLarge)]
    public void ButtonMetrics_ReadTheSharedLadder(SizeVariant size)
    {
        ButtonStyles.Metrics(size).Should().Be((Sizing.Height(size), Sizing.PaddingX(size),
            Sizing.Gap(size), Sizing.LabelSize(size), Sizing.Icon(size), Sizing.Radius(size),
            Sizing.HitTarget(size)));
    }

    [Fact]
    public void Ladder_IsMonotonic()
    {
        SizeVariant[] order = [SizeVariant.Small, SizeVariant.Medium, SizeVariant.Large, SizeVariant.XLarge];
        for (var i = 1; i < order.Length; i++)
        {
            Sizing.Height(order[i]).Should().BeGreaterThan(Sizing.Height(order[i - 1]));
            Sizing.PaddingX(order[i]).Should().BeGreaterThan(Sizing.PaddingX(order[i - 1]));
            Sizing.Icon(order[i]).Should().BeGreaterThanOrEqualTo(Sizing.Icon(order[i - 1]));
        }
    }

    /// <summary>The icon ladder is a ladder too — Sm &lt; Dense &lt; Md &lt; Lg, no crossings.</summary>
    [Fact]
    public void IconSizes_Ascend()
    {
        IconSize.Sm.Should().BeLessThan(IconSize.Dense);
        IconSize.Dense.Should().BeLessThan(IconSize.Md);
        IconSize.Md.Should().BeLessThan(IconSize.Lg);
    }

    /// <summary>Named roles keep authors ON the scale — no role invents a duration.</summary>
    [Fact]
    public void MotionRoles_StayOnScale()
    {
        int[] scale = [Motion.FastMs, Motion.BaseMs, Motion.SlowMs, Motion.ExitFor(Motion.SlowMs)];
        MotionSpec[] roles = [Motion.Press, Motion.State, Motion.Enter, Motion.Exit];
        roles.Select(r => r.DurationMs).Should().OnlyContain(d => scale.Contains(d));
    }

    [Fact]
    public void MotionRoles_CarryTheirCurve()
    {
        Motion.Press.DurationMs.Should().Be(Motion.FastMs);
        Motion.Press.Curve.Should().Be(Curve.Standard);
        Motion.Enter.Curve.Should().Be(Curve.Decelerate);
        Motion.Exit.Curve.Should().Be(Curve.Accelerate);
        Motion.Exit.DurationMs.Should().Be(Motion.ExitFor(Motion.SlowMs));
    }

    /// <summary><c>TransitionSpec.Of</c> carries BOTH halves of the role — duration and curve.</summary>
    [Fact]
    public void TransitionOf_CarriesDurationAndCurve()
    {
        var spec = TransitionSpec.Of(StyleChannels.Transform, Motion.Enter);
        spec.Channels.Should().Be(StyleChannels.Transform);
        spec.DurationMs.Should().Be(Motion.Enter.DurationMs);
        spec.Easing.Should().Be(Motion.Enter.Curve);
    }

    /// <summary>The hover tint is fast feedback, not a 150ms number nobody can place.</summary>
    [Fact]
    public void ColorTransition_DefaultsToFastFeedback()
    {
        TransitionSpec.Colors().DurationMs.Should().Be(Motion.FastMs);
    }
}
