namespace eQuantic.UI.Primitives;

/// <summary>
/// How a tile of artwork repeats along ONE axis of the area it is placed in (#1937). A
/// <see cref="ArtworkPattern"/> takes one for each axis, so a border of tiles across the top can
/// repeat sideways and stay once down.
/// </summary>
public enum TileRepeat : byte
{
    /// <summary>Tiles across the whole clip area, from the anchored tile outward on both sides.</summary>
    Repeat = 0,

    /// <summary>One tile, at the anchored origin.</summary>
    Once = 1,

    /// <summary>As many whole tiles as fit the placing area, the first and the last at its edges and
    /// the room left over shared between them; one tile at the origin when fewer than two fit.</summary>
    Spaced = 2,

    /// <summary>The tile scaled so a whole number of tiles, at least one, fills the placing area. An
    /// auto length on the other axis is scaled with it, so the artwork keeps its shape.</summary>
    Fitted = 3,
}
