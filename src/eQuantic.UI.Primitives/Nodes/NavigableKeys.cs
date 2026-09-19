namespace eQuantic.UI.Primitives;

/// <summary>
/// A KEY, as the move it means for a two-dimensional composite (design system C15: arrows walk a
/// day, PgUp/PgDn a month, +Shift a year, Home/End the week's bounds).
/// <para>
/// It lived in the WEB realizer, under the reasoning that "the abstract layer names MOVES and never
/// keys — which target reads which key is a realizer's business". That held while one realizer read
/// keys. <c>PhotonHost.KeyDown</c> takes the same key NAMES ("ArrowLeft", "Home", "PageUp"), so the
/// second realizer reads the very same table, and the premise is spent: what distinguished the
/// targets was never the spelling of a key.
/// </para>
/// <para>
/// So it sits where both can read it, which is the only assembly they share — and a second copy
/// beside the first would be the drift its cross-pin exists to catch, three ways instead of two.
/// This is a TRANSCRIPTION of a keyboard rather than vocabulary: it names no visual thing, and the
/// moves it answers with are the vocabulary's own.
/// </para>
/// <para>
/// Its twin lives in <c>lowering.ts</c>'s <c>navigableMove</c>, and the two are cross-pinned
/// (<c>NavigableKeyTableTests</c>): a key one half claims and the other ignores is a keyboard that
/// works before hydration and stops after it, or the reverse.
/// </para>
/// <para>
/// A key the grid does NOT claim answers null and must reach the page untouched — an inline grid
/// that swallowed Tab, or the browser's own Home/End, would be worse than one with no keyboard.
/// </para>
/// </summary>
public static class NavigableKeys
{
    public static NavigableMove? Move(string key, bool shift) => key switch
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
