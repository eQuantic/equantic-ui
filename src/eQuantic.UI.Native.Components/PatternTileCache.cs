using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Per-host cache of the tiles a box's patterns composite (#1937), beside <see cref="IconRasterCache"/>:
/// one Rgba8 texture per <see cref="PatternTileKey"/> (what was composited, its device size and its
/// tint), so a tiled desk composites its artwork once and every later frame draws the same instance,
/// which the renderer keeps uploaded by identity. A new render scale is a new device size, so the tile
/// is composited again, crisp at the new scale.
/// <para>
/// Least recently used first out, under a budget of bytes rather than of entries: a tile is a whole
/// texture, from a few kilobytes to a full-window cover. A tile larger than the budget is drawn and
/// not kept. The budget is a judgment, not a measurement (design.md, open question 3).
/// </para>
/// </summary>
public sealed class PatternTileCache
{
    /// <summary>Fallback for callers that pass no cache (single-shot renders), as <see cref="IconRasterCache.Shared"/> is.</summary>
    public static readonly PatternTileCache Shared = new();

    /// <summary>The bytes of texture kept at most: 64 MiB, a 4096-pixel square tile.</summary>
    internal const long BudgetBytes = 64L * 1024 * 1024;

    private readonly Dictionary<PatternTileKey, LinkedListNode<(PatternTileKey Key, TextureData? Tile)>> _entries = new();
    private readonly LinkedList<(PatternTileKey Key, TextureData? Tile)> _recency = new();

    /// <summary>The bytes the kept tiles hold.</summary>
    internal long Bytes { get; private set; }

    /// <summary>The tile kept for <paramref name="key"/>, made the most recently used. A key whose
    /// composite drew nothing is kept too, as null, so it is not composited again every frame.</summary>
    internal bool TryGet(in PatternTileKey key, out TextureData? tile)
    {
        if (!_entries.TryGetValue(key, out var hit))
        {
            tile = null;
            return false;
        }
        _recency.Remove(hit);
        _recency.AddFirst(hit);
        tile = hit.Value.Tile;
        return true;
    }

    /// <summary>Keeps <paramref name="tile"/> for <paramref name="key"/>, letting the least recently used
    /// tiles go until it fits the budget; a tile larger than the whole budget is not kept.</summary>
    internal void Add(in PatternTileKey key, TextureData? tile)
    {
        var size = SizeOf(tile);
        if (size > BudgetBytes) return;
        if (_entries.Remove(key, out var stale))
        {
            _recency.Remove(stale);
            Bytes -= SizeOf(stale.Value.Tile);
        }
        while (Bytes + size > BudgetBytes && _recency.Last is { } oldest)
        {
            _recency.RemoveLast();
            _entries.Remove(oldest.Value.Key);
            Bytes -= SizeOf(oldest.Value.Tile);
        }
        _entries[key] = _recency.AddFirst((key, tile));
        Bytes += size;
    }

    /// <summary>Drops every tile — see <see cref="TextRasterCache.Clear"/>.</summary>
    public void Clear()
    {
        _entries.Clear();
        _recency.Clear();
        Bytes = 0;
    }

    private static long SizeOf(TextureData? tile) => tile is null ? 0 : (long)tile.Width * tile.Height * 4;
}
