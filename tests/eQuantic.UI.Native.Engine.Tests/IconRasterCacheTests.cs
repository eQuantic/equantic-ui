using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A drawing that fills its parent is rasterized at every width a resize passes through. The cache
/// keeps a screen's worth and starts over past that, rather than holding every width for the session.
/// </summary>
public class IconRasterCacheTests
{
    private static readonly IconGlyph Glyph = new("", "M0 0 L10 0 L10 10 Z", ViewBox: "0 0 10 10");

    [Fact]
    public void ARepeatedSize_IsRasterizedOnce()
    {
        var cache = new IconRasterCache();
        var rasterizer = new CountingRasterizer();

        cache.Get(rasterizer, Glyph, 24, 24, 2);
        cache.Get(rasterizer, Glyph, 24, 24, 2);

        rasterizer.Calls.Should().Be(1);
    }

    [Fact]
    public void AResizeSweep_DoesNotKeepEveryWidth()
    {
        var cache = new IconRasterCache();
        var rasterizer = new CountingRasterizer();

        // 600 widths, then the first again: had every one been kept, the first would be a hit.
        for (var w = 1; w <= 600; w++) cache.Get(rasterizer, Glyph, w, 10, 1);
        cache.Get(rasterizer, Glyph, 1, 10, 1);

        rasterizer.Calls.Should().Be(601, "the oldest widths were evicted, the first among them");
    }

    /// <summary>
    /// What is in use stays: a shape asked for every frame survives a sweep of 600 others, because
    /// the least recently used goes first, not everything at once.
    /// </summary>
    [Fact]
    public void AShapeInUse_SurvivesASweep()
    {
        var cache = new IconRasterCache();
        var rasterizer = new CountingRasterizer();

        cache.Get(rasterizer, Glyph, 24, 24, 1);
        for (var w = 1; w <= 600; w++)
        {
            cache.Get(rasterizer, Glyph, 1000 + w, 10, 1);
            cache.Get(rasterizer, Glyph, 24, 24, 1); // the shape on screen, every frame
        }

        rasterizer.Calls.Should().Be(601, "the shape in use was rasterized once, the 600 widths once each");
    }

    private sealed class CountingRasterizer : IIconRasterizer
    {
        public int Calls { get; private set; }

        public TextRaster? Rasterize(IconGlyph glyph, float widthDp, float heightDp, float scale)
        {
            Calls++;
            return new TextRaster(1, 1, [255]);
        }
    }
}
