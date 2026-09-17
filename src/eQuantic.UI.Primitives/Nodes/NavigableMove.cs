namespace eQuantic.UI.Primitives;

/// <summary>Which WAY a <see cref="Navigable"/> was asked to move. The abstract layer names the
/// MOVE, never the key that made it: the web reads arrows/Page/Home/End, a native shell may bind
/// something else entirely, and neither spelling belongs in a component's Build.</summary>
public enum NavigableMove : byte
{
    /// <summary>One item back along the row — the reading order's previous.</summary>
    PreviousItem = 0,
    /// <summary>One item forward along the row.</summary>
    NextItem = 1,
    /// <summary>One row up — a week earlier in a calendar.</summary>
    PreviousRow = 2,
    /// <summary>One row down.</summary>
    NextRow = 3,
    /// <summary>One page back — the month before (design system C15: PgUp).</summary>
    PreviousPage = 4,
    /// <summary>One page forward.</summary>
    NextPage = 5,
    /// <summary>One SECTION back — the year before (C15: Shift+PgUp). A grid without sections
    /// treats it as a page.</summary>
    PreviousSection = 6,
    /// <summary>One section forward.</summary>
    NextSection = 7,
    /// <summary>The first item of the current row (C15: Home).</summary>
    RowStart = 8,
    /// <summary>The last item of the current row (C15: End).</summary>
    RowEnd = 9,
}
