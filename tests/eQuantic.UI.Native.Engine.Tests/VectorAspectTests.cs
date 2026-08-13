using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A SHAPE IS NOT ALWAYS SQUARE. An icon's em-box has one number and always will, but a figure a
/// component GENERATES — the connector between two nodes of a diagram, a rule under a banner, a
/// sparkline — has whatever aspect its own viewBox has, and a square box squashes it.
/// <para>
/// Stroke intent was never the gap: <see cref="IconGlyphStyle.Stroke"/> and its width have been
/// honoured by every rasterizer and by the SVG on the web since icon packs landed. The box was.
/// </para>
/// </summary>
public class VectorAspectTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    /// <summary>A curved connector: 140 across, 48 down, on its own grid.</summary>
    private static readonly IconGlyph Edge =
        new("edge", "M0 8 C 46 8, 94 40, 140 40", IconGlyphStyle.Stroke, "0 0 140 48", 1.5f);

    [Fact]
    public void TheBoxIsTheAspectTheAuthorAskedFor()
    {
        var node = LayoutEngine.Layout(new Vector(Edge, 140, height: 48), 400, 300, Ctx);

        node.Bounds.Width.Should().Be(140);
        node.Bounds.Height.Should().Be(48);
    }

    [Fact]
    public void OneNumberStillMeansSquare()
    {
        var node = LayoutEngine.Layout(new Vector(Edge, 64), 400, 300, Ctx);

        node.Bounds.Width.Should().Be(64);
        node.Bounds.Height.Should().Be(64, "a vector with one extent is square, as it always was");
    }

    [Fact]
    public void AHeightIsNeverNegative()
    {
        var negative = () => new Vector(Edge, 140, height: -1);

        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The raster follows the box: the bitmap is width × height device pixels, and each axis scales
    /// on its own. A square rasterizer would have drawn this connector into a 140×140 bitmap and the
    /// curve would have arrived flattened — measured right, drawn wrong.
    /// </summary>
    [MacFact]
    public void TheRasterHasTheBoxShape()
    {
        var raster = new CoreGraphicsIconRasterizer().Rasterize(Edge, 140, 48, 2f)!;

        raster.Width.Should().Be(280);
        raster.Height.Should().Be(96);
        raster.Alpha.Length.Should().Be(280 * 96);
        raster.Alpha.Should().Contain(b => b > 0, "the stroke has to have covered something");
    }

    /// <summary>An icon asks for the same number twice, and comes out exactly as it did before the
    /// box grew a second extent.</summary>
    [MacFact]
    public void ASquareGlyphIsUnchanged()
    {
        var glyph = new IconGlyph("check", "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
        var raster = new CoreGraphicsIconRasterizer().Rasterize(glyph, 24, 24, 2f)!;

        raster.Width.Should().Be(48);
        raster.Height.Should().Be(48);
    }
}
