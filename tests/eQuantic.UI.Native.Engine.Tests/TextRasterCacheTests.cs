using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A fluid heading is a new raster at every width a resize passes through. The cache keeps the
/// text in use and lets the least recently used go, rather than every width or nothing at all.
/// </summary>
public class TextRasterCacheTests
{
    private static readonly TypeStyle Style = TypeStyle.OfSize(16, FontWeight.Regular);

    [Fact]
    public void TheTextInUse_SurvivesASweepOfWidths()
    {
        var cache = new TextRasterCache();
        var rasterizer = new Counting();

        cache.Get(rasterizer, "Fala", Style, 1, 400, 1, 1, TextAlignment.Start);
        for (var w = 1; w <= 5000; w++)
        {
            cache.Get(rasterizer, "Heading", Style, 1, w, 1, 1, TextAlignment.Start);
            cache.Get(rasterizer, "Fala", Style, 1, 400, 1, 1, TextAlignment.Start); // on screen, every frame
        }

        rasterizer.Calls.Should().Be(5001, "the text in use was rasterized once, each width once");
    }

    private sealed class Counting : ITextRasterizer
    {
        public int Calls { get; private set; }

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines,
            float scale, TextAlignment align)
        {
            Calls++;
            return new TextRaster(1, 1, [255]);
        }
    }
}
