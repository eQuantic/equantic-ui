using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The vocabulary of a subtree's mask (#1938): the closed set, the refusals of each kind naming their
/// parameter, the geometry both targets share (the gradient's line and the ellipse's radii), the stops as CSS
/// resolves them, and the value equality of each kind. Each expected number is the browsers': Chrome 157 and
/// Firefox 159 drew the same masks within 0.5/255 of it (tasks 1.1 and 1.2 of subtree-fades-along-a-gradient).
/// </summary>
public class OpacityGradientVocabularyTests
{
    private static readonly Size Wide = new(400, 240);

    private static readonly OpacityStop[] Ramp = [new(0, 1), new(1, 0)];

    private const float Tolerance = 1e-4f;

    // -- The refusals, each naming its parameter -------------------------------------------------------

    [Fact]
    public void ANoStopOpacityGradient_IsRefused_NamingStops()
    {
        var linear = () => new LinearOpacityGradient(90, []);
        linear.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
        var radial = () => new RadialOpacityGradient(new Point(0.5f, 0.5f), RadialExtent.ClosestSide, []);
        radial.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void AnOffsetThatIsNotFinite_IsRefused_NamingStops(float offset)
    {
        var linear = () => new LinearOpacityGradient(90, [new(0, 1), new(offset, 0)]);
        linear.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
        var radial = () => new RadialOpacityGradient(new Point(0.5f, 0.5f), default, [new(offset, 1)]);
        radial.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
    }

    [Theory]
    [InlineData(1.2f)]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AnOpacityOutsideZeroToOne_IsRefused_NamingStops(float opacity)
    {
        var linear = () => new LinearOpacityGradient(90, [new(0, 1), new(1, opacity)]);
        linear.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
        var radial = () => new RadialOpacityGradient(new Point(0.5f, 0.5f), default, [new(0, opacity)]);
        radial.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stops");
    }

    [Fact]
    public void AOpacityGradientWithNoListAtAll_IsRefused_NamingStops()
    {
        var linear = () => new LinearOpacityGradient(90, null!);
        linear.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("stops");
    }

    [Theory]
    [InlineData(float.NaN, 0.5f)]
    [InlineData(0.5f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0.5f)]
    [InlineData(0.5f, float.NegativeInfinity)]
    public void ACentreThatIsNotFinite_IsRefused_NamingCenter(float x, float y)
    {
        var refuse = () => new RadialOpacityGradient(new Point(x, y), RadialExtent.ClosestSide, Ramp);
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("center");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ARadiusThatIsNotPositive_IsRefused_NamingItsAxis(float radius)
    {
        var fractionX = () => RadialExtent.Fraction(radius, 0.5f);
        fractionX.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("x");
        var fractionY = () => RadialExtent.Fraction(0.5f, radius);
        fractionY.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("y");
        var dpX = () => RadialExtent.Dp(radius, 20);
        dpX.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("x");
        var dpY = () => RadialExtent.Dp(20, radius);
        dpY.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("y");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AnAngleThatIsNotFinite_IsRefused_NamingAngle(float angle)
    {
        var refuse = () => new LinearOpacityGradient(angle, Ramp);
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("angle");
    }

    /// <summary>What CSS decides is never refused: an offset out of order, repeated or outside 0..1 is a
    /// valid gradient, resolved by its rules, and a radius larger than the box is an ellipse that overflows.</summary>
    [Fact]
    public void WhatCssAccepts_IsNotRefused()
    {
        var outOfOrder = () => new LinearOpacityGradient(90, [new(0.6f, 1), new(0.3f, 0)]);
        outOfOrder.Should().NotThrow();
        var repeated = () => new LinearOpacityGradient(90, [new(0.5f, 1), new(0.5f, 0)]);
        repeated.Should().NotThrow();
        var outside = () => new LinearOpacityGradient(90, [new(-0.5f, 1), new(1.5f, 0)]);
        outside.Should().NotThrow();
        var single = () => new LinearOpacityGradient(90, [new(0.3f, 0.5f)]);
        single.Should().NotThrow();
        var huge = () => new RadialOpacityGradient(new Point(-1, 2), RadialExtent.Fraction(3, 3), Ramp);
        huge.Should().NotThrow();
        new LinearOpacityGradient(-45, Ramp).Angle.Should().Be(-45);
        new LinearOpacityGradient(720, Ramp).Angle.Should().Be(720);
    }

    [Fact]
    public void AOpacityMaskNode_NeedsItsOpacityGradient()
    {
        var refuse = () => new OpacityMask(null!, new Spacer());
        refuse.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("gradient");
    }

    // -- The gradient's line ------------------------------------------------------------------------------

    /// <summary>The line runs through the centre along (sin θ, −cos θ) and is |W·sin θ| + |H·cos θ| long.</summary>
    [Theory]
    [InlineData(0f, 200f, 240f, 200f, 0f)]
    [InlineData(90f, 0f, 120f, 400f, 120f)]
    [InlineData(135f, 40f, -40f, 360f, 280f)]
    [InlineData(200f, 261.9628f, -50.2414f, 138.0372f, 290.2414f)]
    [InlineData(-90f, 400f, 120f, 0f, 120f)]
    public void TheAxisOfALinearOpacityGradient_RunsThroughTheCentreAtItsAngle(
        float angle, float startX, float startY, float endX, float endY)
    {
        var (start, end) = new LinearOpacityGradient(angle, Ramp).Axis(Wide);
        start.X.Should().BeApproximately(startX, 1e-2f);
        start.Y.Should().BeApproximately(startY, 1e-2f);
        end.X.Should().BeApproximately(endX, 1e-2f);
        end.Y.Should().BeApproximately(endY, 1e-2f);
    }

    [Theory]
    [InlineData(0f, 240f)]
    [InlineData(90f, 400f)]
    [InlineData(135f, 452.548f)]
    [InlineData(200f, 362.334f)]
    public void TheAxisOfALinearOpacityGradient_IsAsLongAsItsEndsTouchingTheCornersMakesIt(float angle, float length)
    {
        var (start, end) = new LinearOpacityGradient(angle, Ramp).Axis(Wide);
        Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2)).Should().BeApproximately(length, 1e-2);
    }

    /// <summary>Projected on the line, the box's four corners span exactly offset 0 to offset 1: the ends touch
    /// the corners, at every angle, which is what makes 0% and 100% mean the box's own extremes.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(37f)]
    [InlineData(90f)]
    [InlineData(135f)]
    [InlineData(200f)]
    [InlineData(300f)]
    public void TheCornersOfTheBox_SpanTheAxisExactly(float angle)
    {
        var (start, end) = new LinearOpacityGradient(angle, Ramp).Axis(Wide);
        var line = new Point(end.X - start.X, end.Y - start.Y);
        var squared = line.Dot(line);
        var corners = new[] { new Point(0, 0), new Point(400, 0), new Point(0, 240), new Point(400, 240) };
        var ts = corners.Select(c => new Point(c.X - start.X, c.Y - start.Y).Dot(line) / squared).ToArray();
        ts.Min().Should().BeApproximately(0, Tolerance);
        ts.Max().Should().BeApproximately(1, Tolerance);
    }

    [Fact]
    public void At135Degrees_TheTopLeftCornerIsTheStartAndTheBottomRightTheEnd()
    {
        var (start, end) = new LinearOpacityGradient(135, Ramp).Axis(Wide);
        var line = new Point(end.X - start.X, end.Y - start.Y);
        float At(Point p) => new Point(p.X - start.X, p.Y - start.Y).Dot(line) / line.Dot(line);
        At(new Point(0, 0)).Should().BeApproximately(0, Tolerance);
        At(new Point(400, 240)).Should().BeApproximately(1, Tolerance);
    }

    // -- The ellipse's radii ------------------------------------------------------------------------------

    private static RadialOpacityGradient Radial(float x, float y, RadialExtent extent) => new(new Point(x, y), extent, Ramp);

    private static readonly float Root2 = MathF.Sqrt(2);

    [Fact]
    public void TheRadiiOfEachExtent_OnACentredPoint()
    {
        Radial(0.5f, 0.5f, RadialExtent.ClosestSide).Radii(Wide).Should().Be(new Size(200, 120));
        Radial(0.5f, 0.5f, RadialExtent.FarthestSide).Radii(Wide).Should().Be(new Size(200, 120));
        Radial(0.5f, 0.5f, RadialExtent.ClosestCorner).Radii(Wide).Should().Be(new Size(200 * Root2, 120 * Root2));
        Radial(0.5f, 0.5f, RadialExtent.FarthestCorner).Radii(Wide).Should().Be(new Size(200 * Root2, 120 * Root2));
        Radial(0.5f, 0.5f, default).Radii(Wide).Should().Be(new Size(200 * Root2, 120 * Root2), "the default");
        Radial(0.5f, 0.5f, RadialExtent.Fraction(0.7f, 0.7f)).Radii(Wide).Should().Be(new Size(280, 0.7f * 240));
        Radial(0.5f, 0.5f, RadialExtent.Dp(50, 30)).Radii(Wide).Should().Be(new Size(50, 30));
    }

    /// <summary>Off centre, at (70%, 35%) of a 400 × 240 box: 280 dp from the left, 84 from the top.</summary>
    [Fact]
    public void TheRadiiOfEachExtent_OnAnOffCentrePoint()
    {
        Radial(0.7f, 0.35f, RadialExtent.ClosestSide).Radii(Wide).Should().Be(new Size(120, 84));
        Radial(0.7f, 0.35f, RadialExtent.FarthestSide).Radii(Wide).Should().Be(new Size(280, 156));
        var closestCorner = Radial(0.7f, 0.35f, RadialExtent.ClosestCorner).Radii(Wide);
        closestCorner.Width.Should().BeApproximately(120 * Root2, Tolerance);
        closestCorner.Height.Should().BeApproximately(84 * Root2, Tolerance);
        var farthestCorner = Radial(0.7f, 0.35f, RadialExtent.FarthestCorner).Radii(Wide);
        farthestCorner.Width.Should().BeApproximately(280 * Root2, Tolerance);
        farthestCorner.Height.Should().BeApproximately(156 * Root2, Tolerance);
    }

    /// <summary>Cura's hero: 70% by 70% of a 400 × 300 box.</summary>
    [Fact]
    public void TheHerosFraction_IsSeventyPercentOfEachSide()
    {
        var hero = new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]);
        var radii = hero.Radii(new Size(400, 300));
        radii.Width.Should().BeApproximately(280, Tolerance);
        radii.Height.Should().BeApproximately(210, Tolerance);
    }

    /// <summary>A centre on an edge, sized by the closest side, is CSS's degenerate ellipse: a radius of zero
    /// on that axis, which the composite reads as the last stop everywhere off the centre line.</summary>
    [Fact]
    public void AClosestSideOnAnEdge_IsADegenerateRadius()
    {
        Radial(0, 0.5f, RadialExtent.ClosestSide).Radii(Wide).Should().Be(new Size(0, 120));
        Radial(0.5f, 1, RadialExtent.ClosestSide).Radii(Wide).Should().Be(new Size(200, 0));
        Radial(0, 0.5f, RadialExtent.FarthestSide).Radii(Wide).Should().Be(new Size(400, 120));
    }

    [Fact]
    public void ARadialExtent_DefaultsToTheFarthestCorner_AsCssDoes()
    {
        default(RadialExtent).Kind.Should().Be(RadialExtentKind.FarthestCorner);
        default(RadialExtent).Should().Be(RadialExtent.FarthestCorner);
        RadialExtent.Fraction(0.7f, 0.4f).Kind.Should().Be(RadialExtentKind.Fraction);
        RadialExtent.Fraction(0.7f, 0.4f).X.Should().Be(0.7f);
        RadialExtent.Fraction(0.7f, 0.4f).Y.Should().Be(0.4f);
        RadialExtent.Dp(50, 30).Kind.Should().Be(RadialExtentKind.Dp);
        RadialExtent.ClosestSide.Kind.Should().Be(RadialExtentKind.ClosestSide);
        RadialExtent.FarthestSide.Kind.Should().Be(RadialExtentKind.FarthestSide);
        RadialExtent.ClosestCorner.Kind.Should().Be(RadialExtentKind.ClosestCorner);
    }

    // -- The stops, as CSS resolves them -------------------------------------------------------------------

    private static LinearOpacityGradient Linear(params OpacityStop[] stops) => new(90, stops);

    /// <summary>The stop table of design.md decision 4, each row what Chrome 157 and Firefox 159 drew.</summary>
    public static TheoryData<string, OpacityStop[], OpacityStop[], (float T, float Opacity)[]> StopRows() => new()
    {
        {
            "an offset below an earlier one moves up to it: a hard step",
            [new(0.6f, 1), new(0.3f, 0)], [new(0.6f, 1), new(0.6f, 0)],
            [(0f, 1f), (0.59f, 1f), (0.6f, 0f), (0.61f, 0f), (1f, 0f)]
        },
        {
            "the third of three moves up to the second",
            [new(0, 1), new(0.7f, 0.5f), new(0.4f, 0)], [new(0, 1), new(0.7f, 0.5f), new(0.7f, 0)],
            [(0f, 1f), (0.35f, 0.75f), (0.69f, 0.5071f), (0.7f, 0f), (0.9f, 0f)]
        },
        {
            "stops repeated at one offset: opaque before, transparent from it",
            [new(0.5f, 1), new(0.5f, 0.5f), new(0.5f, 0)], [new(0.5f, 1), new(0.5f, 0.5f), new(0.5f, 0)],
            [(0f, 1f), (0.49f, 1f), (0.5f, 0f), (0.8f, 0f)]
        },
        {
            "a step inside a ramp: the later stop wins from the step on",
            [new(0, 1), new(0.5f, 0.5f), new(0.5f, 0.2f), new(1, 0)], [new(0, 1), new(0.5f, 0.5f), new(0.5f, 0.2f), new(1, 0)],
            [(0f, 1f), (0.25f, 0.75f), (0.5f, 0.2f), (0.75f, 0.1f), (1f, 0f)]
        },
        {
            "offsets outside 0..1 are kept and extrapolated along the line",
            [new(-0.5f, 1), new(1.5f, 0)], [new(-0.5f, 1), new(1.5f, 0)],
            [(0f, 0.75f), (0.5f, 0.5f), (1f, 0.25f), (-1f, 1f), (2f, 0f)]
        },
        {
            "every offset past 1: the first stop's opacity across the box",
            [new(1.2f, 1), new(1.5f, 0)], [new(1.2f, 1), new(1.5f, 0)],
            [(0f, 1f), (1f, 1f), (1.35f, 0.5f), (1.6f, 0f)]
        },
        {
            "every offset before 0: the last stop's opacity across the box",
            [new(-1.5f, 1), new(-1.2f, 0)], [new(-1.5f, 1), new(-1.2f, 0)],
            [(0f, 0f), (1f, 0f), (-1.35f, 0.5f), (-2f, 1f)]
        },
        {
            "one stop is that opacity everywhere",
            [new(0.3f, 0.5f)], [new(0.3f, 0.5f)],
            [(-3f, 0.5f), (0f, 0.5f), (0.3f, 0.5f), (1f, 0.5f), (9f, 0.5f)]
        },
        {
            "a hard stop at the middle of a ramp's ends",
            [new(0.5f, 1), new(0.5f, 0)], [new(0.5f, 1), new(0.5f, 0)],
            [(0f, 1f), (0.4999f, 1f), (0.5f, 0f), (1f, 0f)]
        },
        {
            "eight stops of alternating opacity are piecewise linear",
            [new(0f / 7, 1), new(1f / 7, 0), new(2f / 7, 1), new(3f / 7, 0), new(4f / 7, 1), new(5f / 7, 0), new(6f / 7, 1), new(1, 0)],
            [new(0f / 7, 1), new(1f / 7, 0), new(2f / 7, 1), new(3f / 7, 0), new(4f / 7, 1), new(5f / 7, 0), new(6f / 7, 1), new(1, 0)],
            [(0f, 1f), (0.5f / 7, 0.5f), (1f / 7, 0f), (1.5f / 7, 0.5f), (2f / 7, 1f), (4.25f / 7, 0.75f), (5.5f / 7, 0.5f), (1f, 0f)]
        },
        {
            "a plain ramp",
            [new(0, 1), new(1, 0)], [new(0, 1), new(1, 0)],
            [(0f, 1f), (0.25f, 0.75f), (0.5f, 0.5f), (1f, 0f), (-1f, 1f), (2f, 0f)]
        },
    };

    [Theory]
    [MemberData(nameof(StopRows))]
    public void TheStops_ResolveAsCssResolvesThem(string row, OpacityStop[] stops, OpacityStop[] resolved,
        (float T, float Opacity)[] samples)
    {
        var gradient = Linear(stops);

        gradient.Stops.Should().Equal(stops, "the stops stay as they were written: {0}", row);
        gradient.Resolved.Should().Equal(resolved, "the fix-up moves an offset up and sorts nothing: {0}", row);
        foreach (var (t, opacity) in samples)
            gradient.OpacityAt(t).Should().BeApproximately(opacity, 1e-3f, "{0}, at {1}", row, t);
    }

    /// <summary>The same rows in a radial mask: both kinds share one fix-up and one answer.</summary>
    [Theory]
    [MemberData(nameof(StopRows))]
    public void ARadialOpacityGradient_ResolvesItsStopsAsALinearOneDoes(string row, OpacityStop[] stops, OpacityStop[] resolved,
        (float T, float Opacity)[] samples)
    {
        var gradient = new RadialOpacityGradient(new Point(0.5f, 0.5f), default, stops);

        gradient.Resolved.Should().Equal(resolved, row);
        foreach (var (t, opacity) in samples)
            gradient.OpacityAt(t).Should().BeApproximately(opacity, 1e-3f, "{0}, at {1}", row, t);
    }

    /// <summary>A plausible wrong fix-up is to SORT the stops. Sorted, an out-of-order stop would ramp where CSS
    /// makes a step, so this row would read 0 at 0.3 and 0.5 at 0.45.</summary>
    [Fact]
    public void AnOutOfOrderStop_IsMovedUp_NeverSorted()
    {
        var gradient = Linear(new(0.6f, 1), new(0.3f, 0));

        gradient.Resolved[0].Offset.Should().Be(0.6f);
        gradient.Resolved[1].Offset.Should().Be(0.6f);
        gradient.OpacityAt(0.3f).Should().Be(1, "before the step the first stop's opacity holds");
        gradient.OpacityAt(0.45f).Should().Be(1);
    }

    [Fact]
    public void ANotANumberPosition_IsPastTheEnd()
    {
        Linear(new(0, 1), new(1, 0)).OpacityAt(float.NaN).Should().Be(0);
    }

    [Fact]
    public void TheListGivenToAOpacityGradient_IsCopied()
    {
        var stops = new List<OpacityStop> { new(0, 1), new(1, 0) };
        var gradient = Linear([.. stops]);
        var viaList = new LinearOpacityGradient(90, stops);

        stops.Add(new(2, 1));
        stops[0] = new(0.5f, 0.5f);

        viaList.Stops.Should().Equal(new OpacityStop(0, 1), new OpacityStop(1, 0));
        viaList.Resolved.Should().Equal(new OpacityStop(0, 1), new OpacityStop(1, 0));
        gradient.Stops.Should().HaveCount(2);
    }

    // -- Value equality ---------------------------------------------------------------------------------------

    [Fact]
    public void ALinearOpacityGradient_IsEqualByItsAngleAndItsStops()
    {
        var one = new LinearOpacityGradient(135, [new(0, 1), new(1, 0)]);

        one.Should().Be(new LinearOpacityGradient(135, [new(0, 1), new(1, 0)]), "a list compares by what it holds");
        one.GetHashCode().Should().Be(new LinearOpacityGradient(135, [new(0, 1), new(1, 0)]).GetHashCode());
        one.Should().NotBe(new LinearOpacityGradient(136, [new(0, 1), new(1, 0)]));
        one.Should().NotBe(new LinearOpacityGradient(135, [new(0, 1), new(1, 0.5f)]));
        one.Should().NotBe(new LinearOpacityGradient(135, [new(0, 1)]));
        one.Should().NotBe(new LinearOpacityGradient(135, [new(1, 0), new(0, 1)]), "stops are ordered");
    }

    [Fact]
    public void ARadialOpacityGradient_IsEqualByItsCentreItsExtentAndItsStops()
    {
        var one = new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]);

        one.Should().Be(new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]));
        one.GetHashCode().Should().Be(
            new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]).GetHashCode());
        one.Should().NotBe(new RadialOpacityGradient(new Point(0.7f, 0.4f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]));
        one.Should().NotBe(new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.6f), [new(0.35f, 1), new(0.76f, 0)]));
        one.Should().NotBe(new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.8f, 0)]));
    }

    [Fact]
    public void TwoKindsWithTheSameStops_AreNotEqual()
    {
        OpacityGradient linear = new LinearOpacityGradient(90, Ramp);
        OpacityGradient radial = new RadialOpacityGradient(new Point(0.5f, 0.5f), default, Ramp);

        linear.Should().NotBe(radial);
    }

    [Fact]
    public void AnExtent_IsEqualByItsKindAndItsRadii()
    {
        RadialExtent.Fraction(0.7f, 0.4f).Should().Be(RadialExtent.Fraction(0.7f, 0.4f));
        RadialExtent.Fraction(0.7f, 0.4f).Should().NotBe(RadialExtent.Fraction(0.7f, 0.5f));
        RadialExtent.Fraction(1, 2).Should().NotBe(RadialExtent.Dp(1, 2), "half a box is not half a dp");
        RadialExtent.ClosestSide.Should().NotBe(RadialExtent.FarthestSide);
        new OpacityStop(0.5f, 0.5f).Should().Be(new OpacityStop(0.5f, 0.5f));
        new OpacityStop(0.5f, 0.5f).Should().NotBe(new OpacityStop(0.5f, 0.6f));
    }

    [Fact]
    public void RadialExtentKind_HasTheValuesTheAbiPins()
    {
        ((int)RadialExtentKind.FarthestCorner).Should().Be(0);
        ((int)RadialExtentKind.ClosestSide).Should().Be(1);
        ((int)RadialExtentKind.FarthestSide).Should().Be(2);
        ((int)RadialExtentKind.ClosestCorner).Should().Be(3);
        ((int)RadialExtentKind.Fraction).Should().Be(4);
        ((int)RadialExtentKind.Dp).Should().Be(5);
    }

    // -- The node ------------------------------------------------------------------------------------------------

    [Fact]
    public void AOpacityMaskNode_HoldsItsOpacityGradientAndItsChild_AndIsLayoutTransparent()
    {
        var child = new Spacer();
        var gradient = new LinearOpacityGradient(90, Ramp);
        var node = new OpacityMask(gradient, child);

        node.Gradient.Should().BeSameAs(gradient);
        node.Child.Should().BeSameAs(child);
        node.NodeKind.Should().Be("opacityMask");
        node.IsLayoutTransparent().Should().BeTrue("a gradient adds no geometry: its parent sizes it as it sizes its child");
    }

    [Fact]
    public void TheFactory_BuildsTheNode()
    {
        var child = new Spacer();
        var gradient = new LinearOpacityGradient(90, Ramp);

        var node = eQuantic.UI.Components.UI.OpacityMask(gradient, child);

        node.Gradient.Should().BeSameAs(gradient);
        node.Child.Should().BeSameAs(child);
    }

    // -- The closed set ------------------------------------------------------------------------------------------

    /// <summary>
    /// A type outside the vocabulary cannot be a mask: a record's copy constructor must stay protected, so a
    /// derived record could chain to it, and only the private protected constructor and dispatch keep it out.
    /// Compiled against the real assembly, as an app's code would be.
    /// </summary>
    [Fact]
    public void ATypeOutsideTheVocabulary_CannotDeriveAKind()
    {
        const string source = """
            using eQuantic.UI.Primitives;
            public sealed record Outside : OpacityGradient
            {
                public Outside(Outside original) : base(original) { }
                public Outside() : base(new OpacityStop[] { new(0, 1) }) { }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(OpacityGradient).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("Outside", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().Contain(d => d.Id == "CS0534",
            "a kind must implement the dispatch it cannot reach from outside the assembly");
        errors.Should().Contain(d => d.Id == "CS0122",
            "a kind must chain to the constructor it cannot reach from outside the assembly");
    }

    /// <summary>The visitor reaches each kind's own method.</summary>
    [Fact]
    public void Accept_HandsEachKindToItsOwnVisit()
    {
        var names = new KindNames();
        new LinearOpacityGradient(90, Ramp).Accept(names, 0).Should().Be("linear");
        new RadialOpacityGradient(new Point(0.5f, 0.5f), default, Ramp).Accept(names, 0).Should().Be("radial");
        ((OpacityGradient)new LinearOpacityGradient(90, Ramp)).Accept(names, 0).Should().Be("linear");
        ((OpacityGradient)new RadialOpacityGradient(new Point(0.5f, 0.5f), default, Ramp)).Accept(names, 0).Should().Be("radial");
    }

    private sealed class KindNames : IOpacityGradientVisitor<int, string>
    {
        public string Visit(LinearOpacityGradient gradient, int state) => "linear";
        public string Visit(RadialOpacityGradient gradient, int state) => "radial";
    }
}
