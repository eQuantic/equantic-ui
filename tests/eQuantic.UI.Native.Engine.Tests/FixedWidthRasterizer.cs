using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Glyph metrics without a platform text service: 8dp per character, so a column is a number a test
/// can name, and a line its style's line height. It draws no pixels. Twelve test files carried a
/// private copy of it each.
/// </summary>
internal sealed class FixedWidthRasterizer : ITextRasterizer
{
    public TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
        float maxWidth, int maxLines, float scale, TextAlignment align)
    {
        if (string.IsNullOrEmpty(content)) return null;
        var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
        var height = Math.Max(1, (int)(style.LineHeight * scale));
        return new TextRaster(width, height, new byte[width * height]);
    }
}
