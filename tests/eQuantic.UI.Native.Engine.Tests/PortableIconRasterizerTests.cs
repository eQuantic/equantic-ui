using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The icon rasterizer in plain C# (#1240), which the build of an app icon uses on every machine, Linux
/// included, where no platform one exists and a vector came out as the engine's placeholder box. It
/// answers as <c>CoreGraphicsIconRasterizer</c> does: A8 coverage, the nonzero rule, round caps and
/// joins, each axis scaled on its own. Coverage is exact area, so an edge through the middle of a
/// pixel covers half of it.
/// </summary>
public class PortableIconRasterizerTests
{
    private static readonly PortableIconRasterizer Rasterizer = new();

    private static TextRaster Raster(string path, string viewBox, float width, float height,
        IconGlyphStyle style = IconGlyphStyle.Fill, float strokeWidth = 1) =>
        Rasterizer.Rasterize(new IconGlyph("", path, style, viewBox, strokeWidth), width, height, 1)!;

    private static int At(TextRaster raster, int x, int y) => raster.Alpha[y * raster.Width + x];

    [Fact]
    public void AFilledSquare_CoversItsPixels_AndNothingElse()
    {
        var raster = Raster("M0 0H10V10H0Z", "0 0 20 20", 20, 20);

        raster.Width.Should().Be(20);
        raster.Height.Should().Be(20);
        At(raster, 0, 0).Should().Be(255);
        At(raster, 9, 9).Should().Be(255);
        At(raster, 10, 9).Should().Be(0);
        At(raster, 9, 10).Should().Be(0);
        raster.Alpha.Sum(alpha => alpha).Should().Be(100 * 255, "a hundred pixels, each covered whole");
    }

    [Fact]
    public void AnEdgeThroughThePixelsMiddle_CoversHalfOfIt()
    {
        var raster = Raster("M0 0H10.5V10H0Z", "0 0 20 20", 20, 20);

        At(raster, 10, 5).Should().BeInRange(127, 128);
        At(raster, 11, 5).Should().Be(0);
    }

    [Fact]
    public void AHoleTurnedTheOtherWay_IsLeftEmpty_ByTheNonzeroRule()
    {
        // The outer square clockwise, the inner one counter-clockwise: their windings cancel inside.
        var raster = Raster("M0 0H20V20H0Z M5 5V15H15V5Z", "0 0 20 20", 20, 20);

        At(raster, 1, 1).Should().Be(255);
        At(raster, 10, 10).Should().Be(0);
    }

    [Fact]
    public void TwoShapesTurnedTheSameWay_AreTheirUnion()
    {
        var raster = Raster("M0 0H12V10H0Z M8 0H20V10H8Z", "0 0 20 20", 20, 20);

        At(raster, 10, 5).Should().Be(255, "where the two overlap the coverage is full, never more");
        raster.Alpha.Sum(alpha => alpha).Should().Be(200 * 255);
    }

    [Fact]
    public void ACircle_CoversItsArea()
    {
        // Two arcs, which VectorPath lowers to cubics; the rasterizer cuts them finely enough that the
        // covered area is the circle's within a fraction of a percent.
        var raster = Raster("M2 10A8 8 0 1 0 18 10A8 8 0 1 0 2 10Z", "0 0 20 20", 20, 20);

        var area = raster.Alpha.Sum(alpha => alpha) / 255.0;
        area.Should().BeApproximately(Math.PI * 64, Math.PI * 64 * 0.01);
        At(raster, 10, 10).Should().Be(255);
        At(raster, 0, 0).Should().Be(0);
    }

    [Fact]
    public void AStroke_CoversItsWidth_AndEndsInARoundCap()
    {
        var raster = Raster("M5 10H15", "0 0 20 20", 20, 20, IconGlyphStyle.Stroke, strokeWidth: 4);

        foreach (var row in new[] { 8, 9, 10, 11 }) At(raster, 10, row).Should().Be(255, $"row {row} is inside the stroke");
        At(raster, 10, 7).Should().Be(0);
        At(raster, 10, 12).Should().Be(0);
        At(raster, 3, 9).Should().BeGreaterThan(200, "the cap reaches two units past the end, round");
        At(raster, 3, 7).Should().BeLessThan(80, "a round cap leaves the corner a square one would fill");
    }

    [Fact]
    public void EachAxisScalesOnItsOwn()
    {
        var raster = Raster("M0 0H10V10H0Z", "0 0 10 10", 40, 20);

        raster.Width.Should().Be(40);
        raster.Height.Should().Be(20);
        raster.Alpha.Should().OnlyContain(alpha => alpha == 255, "the square fills a box of any aspect");
    }

    [Fact]
    public void TheViewBoxsOriginIsWhereTheRasterStarts()
    {
        var raster = Raster("M10 10H20V20H10Z", "10 10 10 10", 10, 10);

        raster.Alpha.Should().OnlyContain(alpha => alpha == 255);
    }

    [Fact]
    public void APathOrAViewBoxItCannotRead_IsNull()
    {
        Rasterizer.Rasterize(new IconGlyph("", "", IconGlyphStyle.Fill, "0 0 10 10"), 10, 10, 1).Should().BeNull();
        Rasterizer.Rasterize(new IconGlyph("", "M0 0H1V1Z", IconGlyphStyle.Fill, "0 0 0 10"), 10, 10, 1).Should().BeNull();
    }
}
