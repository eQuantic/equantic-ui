using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// NO GLYPH IS EVER CLIPPED. The line box belongs to layout and the ink belongs to the font, and
/// the two disagree all the time — a 17dp label in a 16dp line, an accent, a deep 'g' tail. The
/// rasterizer's job is to hold the ink and say how far above the line box it had to reach; the
/// realizer raises the draw by exactly that, so the line box still lands where layout put it.
/// <para>
/// The Edgar test: he read "Large 40" off a button and saw the tail of the 'g' cut off.
/// </para>
/// </summary>
public class TextInkTests
{
    private static int MaxAlpha(TextRaster raster, int row)
    {
        var max = 0;
        for (var x = 0; x < raster.Width; x++)
            max = Math.Max(max, raster.Alpha[row * raster.Width + x]);
        return max;
    }

    [MacTheory]
    [InlineData(SizeVariant.Small)]
    [InlineData(SizeVariant.Medium)]
    [InlineData(SizeVariant.Large)]
    [InlineData(SizeVariant.XLarge)]
    public void ADescenderNeverTouchesTheEdgeOfItsBitmap(SizeVariant size)
    {
        var service = new CoreTextService();
        // Exactly how a Button dresses its label: the ladder's size, the role's everything else.
        var style = TypeStyle.OfSize(Sizing.LabelSize(size), FontWeight.SemiBold, 0.1f);

        var raster = service.Rasterize("Large gjpqy", style, 1f, float.PositiveInfinity, 1, 2f);

        raster.Should().NotBeNull();
        MaxAlpha(raster!, raster!.Height - 1).Should().Be(0,
            $"the descender runs into the bottom edge at {size} — that is a cut 'g'");
        MaxAlpha(raster, 0).Should().Be(0,
            $"the ascender runs into the top edge at {size}");
    }

    /// <summary>
    /// Even when the line box is deliberately too small for the glyphs, the bitmap holds them and
    /// reports the overshoot — the guarantee that no style anyone writes can cut a letter.
    /// </summary>
    [MacFact]
    public void ATooSmallLineBoxGrowsTheBitmapInsteadOfCuttingTheGlyphs()
    {
        var service = new CoreTextService();
        var cramped = new TypeStyle(20, 12, FontWeight.Regular, 0f, 1.3f);

        var raster = service.Rasterize("gjpqy", cramped, 1f, float.PositiveInfinity, 1, 2f);

        raster.Should().NotBeNull();
        raster!.Height.Should().BeGreaterThan((int)(cramped.LineHeight * 2),
            "a 20dp glyph does not fit a 12dp line, so the bitmap has to be taller than the line");
        raster.PadTop.Should().BeGreaterThan(0,
            "the ink starts above the line box, and the realizer needs to know by how much");
        MaxAlpha(raster, raster.Height - 1).Should().Be(0);
        MaxAlpha(raster, 0).Should().Be(0);
    }

    /// <summary>The common case pays nothing: text that fits its line reports no padding.</summary>
    [MacFact]
    public void TextThatFitsItsLineReportsNoPaddingWorthMentioning()
    {
        var service = new CoreTextService();
        var roomy = new TypeStyle(12, 20, FontWeight.Regular, 0f, 1.3f);

        var raster = service.Rasterize("Roomy", roomy, 1f, float.PositiveInfinity, 1, 2f);

        raster!.PadTop.Should().BeLessThanOrEqualTo(2, "one dp of antialiasing guard, no more");
    }
}
