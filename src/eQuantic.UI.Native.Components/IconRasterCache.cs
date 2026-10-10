using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Per-host cache of rasterized icon glyphs (W4): keyed by glyph VALUE (IconGlyph is a record
/// struct) + the BOX (width and height, because a figure need not be square) + scale — never by
/// color, the tint lives on the draw command. Stable instances
/// feed the display-list texture table and the GPU upload cache (identity dedupe).
/// </summary>
public sealed class IconRasterCache
{
    /// <summary>Fallback for callers that pass no cache (single-shot renders).</summary>
    public static readonly IconRasterCache Shared = new();

    /// <summary>The most rasters kept at once; a screen's icons fit many times over.</summary>
    private const int MaxEntries = 512;

    // Least recently used first out: a drawing that fills its parent is rasterized at every width
    // a resize passes through, and those stale widths are what goes, while the shapes on screen
    // (re-used every frame) stay. Clearing everything at the cap would rasterize a drawing of more
    // shapes than the cap holds all over again, every frame.
    private readonly Dictionary<(IconGlyph Glyph, float Width, float Height, float Scale),
        LinkedListNode<((IconGlyph, float, float, float) Key, TextureData? Raster)>> _entries = new();
    private readonly LinkedList<((IconGlyph, float, float, float) Key, TextureData? Raster)> _recency = new();

    public TextureData? Get(IIconRasterizer rasterizer, IconGlyph glyph, float widthDp, float heightDp, float scale)
    {
        var key = (glyph, widthDp, heightDp, scale);
        if (_entries.TryGetValue(key, out var hit))
        {
            _recency.Remove(hit);
            _recency.AddFirst(hit);
            return hit.Value.Raster;
        }

        if (_entries.Count >= MaxEntries && _recency.Last is { } oldest)
        {
            _recency.RemoveLast();
            _entries.Remove(oldest.Value.Key);
        }
        var raster = rasterizer.Rasterize(glyph, widthDp, heightDp, scale);
        var entry = raster is null || raster.Width <= 0 || raster.Height <= 0
            ? null
            : new TextureData(raster.Width, raster.Height, raster.Alpha);
        _entries[key] = _recency.AddFirst((key, entry));
        return entry;
    }

    /// <summary>Drops every entry — see <see cref="TextRasterCache.Clear"/>.</summary>
    public void Clear()
    {
        _entries.Clear();
        _recency.Clear();
    }
}
