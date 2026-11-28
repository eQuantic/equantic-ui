namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="Pattern"/> is placed against (#1937): its box, which a scroll moves, or the
/// window, which it does not, so the pattern stays still while the content passes over it.
/// </summary>
public enum TileAttachment : byte
{
    /// <summary>Placed in the box's own area and moved with it.</summary>
    Box = 0,

    /// <summary>
    /// Placed against the window, and still clipped to the box. A box with a transform, or one under
    /// a transform, places it against its own area instead, as a box attachment does.
    /// </summary>
    Window = 1,
}
