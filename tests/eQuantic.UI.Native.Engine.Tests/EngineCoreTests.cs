using eQuantic.UI.Primitives;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Tests.Golden;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Unit tests for the normative SDF math — the spec the Slang shaders must transliterate.</summary>
public class SdfTests
{
    private static readonly Size Half = new(50, 25); // a 100x50 box

    [Fact]
    public void SharpRect_CenterDistance_IsMinusShortestHalfSize()
    {
        Sdf.RoundedRect(Point.Zero, Half, CornerRadii.Zero).Should().BeApproximately(-25f, 1e-4f);
    }

    [Fact]
    public void SharpRect_OnEdge_IsZero()
    {
        Sdf.RoundedRect(new Point(50, 0), Half, CornerRadii.Zero).Should().BeApproximately(0f, 1e-4f);
        Sdf.RoundedRect(new Point(0, -25), Half, CornerRadii.Zero).Should().BeApproximately(0f, 1e-4f);
    }

    [Fact]
    public void SharpRect_OutsideAlongAxis_IsPositiveDistance()
    {
        Sdf.RoundedRect(new Point(60, 0), Half, CornerRadii.Zero).Should().BeApproximately(10f, 1e-4f);
    }

    [Fact]
    public void SharpRect_OutsideCorner_IsEuclidean()
    {
        // 3-4-5 triangle off the bottom-right corner.
        Sdf.RoundedRect(new Point(53, 29), Half, CornerRadii.Zero).Should().BeApproximately(5f, 1e-4f);
    }

    [Fact]
    public void Circle_DegeneratesFromRRect()
    {
        // Radius = half-size on a square box = a circle of radius 25.
        var half = new Size(25, 25);
        var radii = new CornerRadii(25);
        Sdf.RoundedRect(Point.Zero, half, radii).Should().BeApproximately(-25f, 1e-4f);
        Sdf.RoundedRect(new Point(25, 0), half, radii).Should().BeApproximately(0f, 1e-4f);
        Sdf.RoundedRect(new Point(30, 0), half, radii).Should().BeApproximately(5f, 1e-4f);
        // Diagonal: distance from center 25√2 ≈ 35.36 → 10.36 outside the circle (a sharp rect would be 5√2 corner distance).
        Sdf.RoundedRect(new Point(30, 30), half, radii).Should().BeApproximately(MathF.Sqrt(1800) - 25, 1e-3f);
    }

    [Fact]
    public void PerCornerRadius_OnlySoftensItsOwnQuadrant()
    {
        var radii = new CornerRadii(TopLeft: 10, TopRight: 0, BottomRight: 0, BottomLeft: 0);
        // Top-left corner point is now outside (rounded away)…
        Sdf.RoundedRect(new Point(-50, -25), Half, radii).Should().BeGreaterThan(0);
        // …while the sharp bottom-right corner still sits exactly on the edge.
        Sdf.RoundedRect(new Point(50, 25), Half, radii).Should().BeApproximately(0f, 1e-4f);
    }

    [Fact]
    public void Stroke_IsCenteredBandOnEdge()
    {
        Sdf.Stroke(0f, 4f).Should().BeApproximately(-2f, 1e-4f);   // on the edge → deepest inside the band
        Sdf.Stroke(2f, 4f).Should().BeApproximately(0f, 1e-4f);    // band outer boundary
        Sdf.Stroke(-2f, 4f).Should().BeApproximately(0f, 1e-4f);   // band inner boundary
        Sdf.Stroke(5f, 4f).Should().BeApproximately(3f, 1e-4f);
    }

    [Fact]
    public void Coverage_RampsAcrossOnePixel()
    {
        Sdf.Coverage(-0.5f).Should().Be(1f);
        Sdf.Coverage(0f).Should().Be(0.5f);
        Sdf.Coverage(0.5f).Should().Be(0f);
    }
}

public class GeometryTests
{
    [Fact]
    public void Matrix_Rotation_TransformsPoint()
    {
        var m = Matrix2D.Rotation(MathF.PI / 2); // 90° — +X maps to +Y (y-down = clockwise on screen)
        var p = m.Transform(new Point(1, 0));
        p.X.Should().BeApproximately(0f, 1e-5f);
        p.Y.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Matrix_ComposeOrder_AppliesLeftFirst()
    {
        var scaleThenTranslate = Matrix2D.Scale(2, 2) * Matrix2D.Translation(10, 0);
        scaleThenTranslate.Transform(new Point(1, 1)).Should().Be(new Point(12, 2));

        var translateThenScale = Matrix2D.Translation(10, 0) * Matrix2D.Scale(2, 2);
        translateThenScale.Transform(new Point(1, 1)).Should().Be(new Point(22, 2));
    }

    [Fact]
    public void Matrix_Inverse_RoundTrips()
    {
        var m = Matrix2D.Rotation(0.7f) * Matrix2D.Scale(2, 3) * Matrix2D.Translation(5, -8);
        var inv = m.Invert()!.Value;
        var p = new Point(13, -4);
        var round = inv.Transform(m.Transform(p));
        round.X.Should().BeApproximately(p.X, 1e-3f);
        round.Y.Should().BeApproximately(p.Y, 1e-3f);
    }

    [Fact]
    public void Matrix_Degenerate_HasNoInverse()
    {
        Matrix2D.Scale(0, 1).Invert().Should().BeNull();
    }

    [Fact]
    public void RRect_Normalized_ScalesOverflowingRadiiProportionally()
    {
        // 100x50 box with uniform radius 40: vertical sides only fit 25+25 → scale = 50/80.
        var rrect = new RRect(new Rect(0, 0, 100, 50), new CornerRadii(40)).Normalized();
        rrect.Radii.TopLeft.Should().BeApproximately(25f, 1e-4f);
        rrect.Radii.BottomRight.Should().BeApproximately(25f, 1e-4f);
    }

    [Fact]
    public void RRect_Normalized_ClampsNegativeToZero()
    {
        var rrect = new RRect(new Rect(0, 0, 100, 50), new CornerRadii(-5, 10, 10, 10)).Normalized();
        rrect.Radii.TopLeft.Should().Be(0);
        rrect.Radii.TopRight.Should().Be(10);
    }

    [Fact]
    public void Builder_UnbalancedTransforms_Throw()
    {
        var builder = new DisplayListBuilder();
        builder.PushTransform(Matrix2D.Translation(1, 1));
        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unbalanced*");
    }
}

public class ColorSpaceTests
{
    [Fact]
    public void SrgbRoundTrip_IsLossless_ForAllBytes()
    {
        for (var i = 0; i < 256; i++)
            ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear((byte)i)).Should().Be((byte)i);
    }

    [Fact]
    public void MidGray_DecodesToLinearFifth()
    {
        // sRGB 128 ≈ linear 0.2159 — the classic "50% sRGB is ~22% light".
        ColorSpace.SrgbToLinear(128).Should().BeApproximately(0.2159f, 0.001f);
    }

    [Fact]
    public void Premultiply_ScalesByAlphaAndCoverage()
    {
        var lin = ColorSpace.ToPremultipliedLinear(Color.FromRgba(255, 255, 255, 128), alphaScale: 0.5f);
        lin.A.Should().BeApproximately(128f / 255f * 0.5f, 1e-4f);
        lin.R.Should().BeApproximately(lin.A, 1e-4f); // white: R == A after premultiply
    }
}

public class PngCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        const int w = 23, h = 17;
        var rgba = new byte[w * h * 4];
        var rng = new Random(42);
        rng.NextBytes(rgba);

        var png = PngCodec.Encode(w, h, rgba);
        var (width, height, decoded) = PngCodec.Decode(png);

        width.Should().Be(w);
        height.Should().Be(h);
        decoded.Should().Equal(rgba);
    }
}
