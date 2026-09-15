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

    private readonly Dictionary<(string Content, TypeStyle Style, float TypeScale, float MaxWidth, int MaxLines, float Scale, TextAlignment Align), Entry?> _entries = new();

    /// <param name="Texture">The A8 coverage the draw command samples.</param>
    /// <param name="PadTop">Device pixels of ink ABOVE the line box (see <see cref="TextRaster"/>)
    /// — the draw rect rises by this much so the line box lands where layout put it.</param>
    public sealed record Entry(TextureData Texture, int PadTop = 0);

    /// <param name="rasterizer">The platform text service that produces the raster on a miss.</param>
    /// <param name="content">The text to raster, which is part of the key.</param>
    /// <param name="style">The face it is drawn in — part of the key, since it changes the pixels.</param>
    /// <param name="typeScale">The Dynamic Type multiplier in force, part of the key.</param>
    /// <param name="maxWidth">The wrapping width in dp, rounded into the key.</param>
    /// <param name="maxLines">The line cap before ellipsis, part of the key.</param>
    /// <param name="scale">Device pixels per dp, part of the key.</param>
    /// <param name="align">Part of the KEY, not a detail of the draw: alignment changes the pixels
    /// (a centred block is as wide as its box and the short lines sit further in), so two
    /// alignments of one string are two rasters. Left out of the key, the first one drawn would be
    /// served to the other.
    /// <para>
    /// Required, like the rasterizer's own parameter and for the same reason. This is the seam the
    /// CALLERS come through, so a default here would be the one that matters: a new draw site would
    /// compile while quietly caching and drawing everything as <see cref="TextAlignment.Start"/> —
    /// which is precisely how the property came to be dropped in the first place.
    /// </para></param>
    public Entry? Get(ITextRasterizer rasterizer, string content, TypeStyle style, float typeScale,
        float maxWidth, int maxLines, float scale, TextAlignment align)
    {
        var key = (content, style, typeScale, MathF.Round(maxWidth, 1), maxLines, scale, align);
        if (_entries.TryGetValue(key, out var cached)) return cached;

        var raster = rasterizer.Rasterize(content, style, typeScale, maxWidth, maxLines, scale, align);
        var entry = raster is null || raster.Width <= 0 || raster.Height <= 0
            ? null
            : new Entry(new TextureData(raster.Width, raster.Height, raster.Alpha), raster.PadTop);
        _entries[key] = entry;
        return entry;
    }

    /// <summary>Drops every entry — hot reload's cue: a patched rasterizer or an edited style
    /// would otherwise keep serving yesterday's pixels under today's keys.</summary>
    public void Clear() => _entries.Clear();
}
