using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S1 engine primitives. The load-bearing assertion is GROUP semantics: a 50% layer over two
/// overlapping black fills reads as ONE 50% blend (0.5 linear → sRGB 188), not the 0.25 double-blend
/// per-command alpha would produce — the difference between CSS opacity and naive alpha.
/// </summary>
public class S1StyleEngineTests
{
    [Fact]
    public void PushLayer_CompositesTheGroupOnce_NeverDoubleBlending()
    {
        using var backend = new ReferenceBackend();
        using var surface = (ReferenceSurface)backend.CreateSurface(4, 4);

        var builder = new DisplayListBuilder();
        builder.Clear(new Color(255, 255, 255, 255));
        builder.PushLayer(0.5f);
        // TWO overlapping full-coverage black fills INSIDE the layer.
        builder.FillRect(new Rect(0, 0, 4, 4), Paint.Solid(new Color(0, 0, 0, 255)));
        builder.FillRect(new Rect(0, 0, 4, 4), Paint.Solid(new Color(0, 0, 0, 255)));
        builder.PopLayer();
        backend.Render(builder.Build(), surface);

        var pixels = new byte[4 * 4 * 4];
        surface.ReadPixelsSrgb(pixels);
        // 0.5 linear encodes to sRGB ≈ 188. Double-blending would land at 0.25 linear ≈ 137.
        pixels[0].Should().BeInRange(186, 190, "the group composites ONCE at the layer alpha");
    }

    [Fact]
    public void UnbalancedLayer_FailsTheBuild()
    {
        var builder = new DisplayListBuilder();
        builder.PushLayer(0.5f);
        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*layer*");
    }

    [Fact]
    public void CenterAnchoredRotation_MapsCornersAsTheCssTransformWould()
    {
        // The realizer's transform convention: M = T(-c) · S · R · T(c + t), with (A·B)(p) = B(A(p)).
        // Rotating 90° around center (20,20): the corner (10,10) lands at (30,10).
        var center = new Point(20, 20);
        var m = Matrix2D.Translation(-center.X, -center.Y)
                * Matrix2D.Scale(1, 1)
                * Matrix2D.Rotation(90 * MathF.PI / 180f)
                * Matrix2D.Translation(center.X, center.Y);

        var mapped = m.Transform(new Point(10, 10));
        mapped.X.Should().BeApproximately(30, 0.001f);
        mapped.Y.Should().BeApproximately(10, 0.001f);
    }
}
