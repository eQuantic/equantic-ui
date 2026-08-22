using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// A KEY, as the move it means for a two-dimensional composite (design system C15: arrows walk a
/// day, PgUp/PgDn a month, +Shift a year, Home/End the week's bounds).
/// <para>
/// The abstract layer names MOVES and never keys — which target reads which key is a realizer's
/// business — so the table lives here, on the web side, and its twin lives in
/// <c>lowering.ts</c>'s <c>navigableMove</c>. The two are cross-pinned
/// (<c>NavigableKeyTableTests</c>): a key one half claims and the other ignores is a keyboard that
/// works before hydration and stops after it, or the reverse.
/// </para>
/// <para>
/// A key the grid does NOT claim answers null and must reach the page untouched — an inline grid
/// that swallowed Tab, or the browser's own Home/End, would be worse than one with no keyboard.
/// </para>
/// </summary>
internal static class NavigableKeys
{
    internal static NavigableMove? Move(string key, bool shift) => key switch
    {
        "ArrowLeft" => NavigableMove.PreviousItem,
        "ArrowRight" => NavigableMove.NextItem,
        "ArrowUp" => NavigableMove.PreviousRow,
        "ArrowDown" => NavigableMove.NextRow,
        "PageUp" => shift ? NavigableMove.PreviousSection : NavigableMove.PreviousPage,
        "PageDown" => shift ? NavigableMove.NextSection : NavigableMove.NextPage,
        "Home" => NavigableMove.RowStart,
        "End" => NavigableMove.RowEnd,
        _ => null,
    };
}
