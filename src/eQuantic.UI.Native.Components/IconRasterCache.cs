using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Per-host cache of rasterized icon glyphs (W4): keyed by glyph VALUE (IconGlyph is a record
/// struct) + size + scale — never by color, the tint lives on the draw command. Stable instances
/// feed the display-list texture table and the GPU upload cache (identity dedupe).
/// </summary>
public sealed class IconRasterCache
{
    /// <summary>Fallback for callers that pass no cache (single-shot renders).</summary>
    public static readonly IconRasterCache Shared = new();

    private readonly Dictionary<(IconGlyph Glyph, float Size, float Scale), TextureData?> _entries = new();

    public TextureData? Get(IIconRasterizer rasterizer, IconGlyph glyph, float sizeDp, float scale)
    {
        var key = (glyph, sizeDp, scale);
        if (_entries.TryGetValue(key, out var cached)) return cached;

        var raster = rasterizer.Rasterize(glyph, sizeDp, scale);
        var entry = raster is null || raster.Width <= 0 || raster.Height <= 0
            ? null
            : new TextureData(raster.Width, raster.Height, raster.Alpha);
        _entries[key] = entry;
        return entry;
    }
}
