namespace eQuantic.UI.Primitives;

/// <summary>
/// The pointer shapes that MEAN something to the person moving the mouse (CSS names, exactly —
/// the web emits these verbatim and the shells map them to their native cursors). Deliberately
/// small; grows only when a control genuinely needs a new meaning.
/// </summary>
public enum PointerCursor : byte
{
    Default = 0,
    Pointer = 1,
    Text = 2,
    NotAllowed = 3,
    /// <summary>Excel's fill handle; precision picking.</summary>
    Crosshair = 4,
    /// <summary>A column-resize grip.</summary>
    ColResize = 5,
    /// <summary>A row-resize grip.</summary>
    RowResize = 6,
}
