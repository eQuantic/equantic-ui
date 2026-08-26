using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The W1 primitive's normative math, pinned at hand-computed points BEFORE the golden judges the
/// picture — a golden proves the three rasterizers agree; these prove the formula is the SHAPE the
/// doc promises. Quarter ring: center origin, rIn 20, rOut 40, angles 0→π/2 (+X to +Y).
/// </summary>
public class AnnularSectorTests
{
    private const float RIn = 20, ROut = 40;
    private const float A0 = 0, A1 = MathF.PI / 2;

    private static float D(float x, float y, float rounding = 0) =>
        Sdf.AnnularSector(new Point(x, y), RIn, ROut, A0, A1, rounding);

    [Fact]
    public void TheEdgesAreZero()
    {
        // On the start edge at mid-band; on the outer arc at mid-angle.
        D(30, 0).Should().BeApproximately(0, 1e-4f);
        D(40 * MathF.Cos(A1 / 2), 40 * MathF.Sin(A1 / 2)).Should().BeApproximately(0, 1e-3f);
    }

    [Fact]
    public void InsideIsNegativeByTheNearestBoundary()
    {
        // Mid-angle, mid-band: 10 from both arcs, farther from both edges.
        var mid = A1 / 2;
        D(30 * MathF.Cos(mid), 30 * MathF.Sin(mid)).Should().BeApproximately(-10, 1e-3f);
    }

    [Fact]
    public void OutsideTheOuterArcIsRadial()
    {
        var mid = A1 / 2;
        D(50 * MathF.Cos(mid), 50 * MathF.Sin(mid)).Should().BeApproximately(10, 1e-3f);
    }

    [Fact]
    public void BeyondTheAngularEdgeTheCornerDecides()
    {
        // (0, −10) sits outside the wedge; the nearest point of the sector is the inner start
        // corner (20, 0): √(20² + 10²) = 22.360…  — the segment branch, exactly.
        D(0, -10).Should().BeApproximately(MathF.Sqrt(500), 1e-3f);
    }

    [Fact]
    public void RoundingBendsTheCornersAndOnlyTheCorners()
    {
        // Along the STRAIGHT run of an edge, inset-by-ρ then subtract-ρ reproduces the same line:
        // (30, 0) stays at distance zero. The pull-in is the corners' alone: (39.9, 0) sat on the
        // sharp edge and is OUTSIDE the rounded one, because the corner arc curves away before the
        // outer radius. Interior depth away from corners is untouched.
        D(30, 0, rounding: 4).Should().BeApproximately(0, 1e-4f);
        D(39.9f, 0, rounding: 4).Should().BeGreaterThan(0.5f);
        var mid = A1 / 2;
        D(30 * MathF.Cos(mid), 30 * MathF.Sin(mid), rounding: 4).Should().BeApproximately(-10, 1e-3f);
    }

    [Fact]
    public void TheBuilderGuardsDegenerates()
    {
        var builder = new DisplayListBuilder();
        builder.FillAnnularSector(new Point(0, 0), 10, 40, 1f, 0.5f, Paint.Solid(Color.FromRgb(1, 2, 3)));
        builder.FillAnnularSector(new Point(0, 0), 10, 0, 0, 1, Paint.Solid(Color.FromRgb(1, 2, 3)));
        // A zero-width band (inner == outer, or inner beyond outer) has no interior, but the SDF
        // is exactly 0 on the shared circle — AA would ink a hairline arc if the command shipped.
        builder.FillAnnularSector(new Point(0, 0), 40, 40, 0, 1, Paint.Solid(Color.FromRgb(1, 2, 3)));
        builder.FillAnnularSector(new Point(0, 0), 50, 40, 0, 1, Paint.Solid(Color.FromRgb(1, 2, 3)));
        builder.Build().Count.Should().Be(0,
            "an empty sweep, a zero outer radius, and a zero-width band draw nothing");
    }
}
