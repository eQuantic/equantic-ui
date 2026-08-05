using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Per-host cache of rasterized text blocks (W4): keyed by everything that shapes the pixels
/// EXCEPT color (the tint lives on the draw command, so light/dark share one raster). Instances
/// are stable across frames — the display-list texture table and the GPU upload cache both dedupe
/// by identity. Unbounded for now; eviction joins the perf pass.
/// </summary>
public sealed class TextRasterCache
{
    /// <summary>Fallback for callers that pass no cache (single-shot renders).</summary>
    public static readonly TextRasterCache Shared = new();

    private readonly Dictionary<(string Content, TypeStyle Style, float TypeScale, float MaxWidth, int MaxLines, float Scale), Entry?> _entries = new();

    /// <param name="PadTop">Device pixels of ink ABOVE the line box (see <see cref="TextRaster"/>)
    /// — the draw rect rises by this much so the line box lands where layout put it.</param>
    public sealed record Entry(TextureData Texture, int PadTop = 0);

    public Entry? Get(ITextRasterizer rasterizer, string content, TypeStyle style, float typeScale,
        float maxWidth, int maxLines, float scale)
    {
        var key = (content, style, typeScale, MathF.Round(maxWidth, 1), maxLines, scale);
        if (_entries.TryGetValue(key, out var cached)) return cached;

        var raster = rasterizer.Rasterize(content, style, typeScale, maxWidth, maxLines, scale);
        var entry = raster is null || raster.Width <= 0 || raster.Height <= 0
            ? null
            : new Entry(new TextureData(raster.Width, raster.Height, raster.Alpha), raster.PadTop);
        _entries[key] = entry;
        return entry;
    }
}
