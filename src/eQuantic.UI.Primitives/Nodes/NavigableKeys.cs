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
/// <para>
/// <c>[ServerOnly]</c> for what that buys over an exception in the runtime-export pin: the compiler
/// REFUSES a page that names it (EQ2010), rather than letting one compile, emit an import and die at
/// hydration against a symbol the runtime never exported. Three types left that hand-kept list the
/// same way, and the list says why — two files whose contents had already drifted apart.
/// </para>
/// </summary>
[ServerOnly]
public static class NavigableKeys
{
    /// <summary>
    /// The move <paramref name="key"/> means to a composite that reads in <paramref name="direction"/>,
    /// or null for a key the composite does not claim.
    /// <para>
    /// The horizontal arrows are the NEXT and the PREVIOUS item along the reading, so what ArrowLeft
    /// means depends on the direction: <see cref="NavigableMove.PreviousItem"/> where it reads left to
    /// right and <see cref="NavigableMove.NextItem"/> where it reads right to left, and ArrowRight the
    /// other way round. Every other key means the same in both: ArrowUp and ArrowDown are rows, the page
    /// keys are pages and sections, and Home and End are the row's start and end.
    /// </para>
    /// <para>
    /// A realizer that lays out left to right whatever the culture says (Photon, for 1.0) passes
    /// <see cref="TextDirection.LeftToRight"/>, which is what its physical keys already are; the
    /// day it mirrors, it passes its own direction and nothing else moves.
    /// </para>
    /// </summary>
    public static NavigableMove? Move(string key, bool shift, TextDirection direction) => key switch
    {
        "ArrowLeft" => direction == TextDirection.RightToLeft ? NavigableMove.NextItem : NavigableMove.PreviousItem,
        "ArrowRight" => direction == TextDirection.RightToLeft ? NavigableMove.PreviousItem : NavigableMove.NextItem,
        "ArrowUp" => NavigableMove.PreviousRow,
        "ArrowDown" => NavigableMove.NextRow,
        "PageUp" => shift ? NavigableMove.PreviousSection : NavigableMove.PreviousPage,
        "PageDown" => shift ? NavigableMove.NextSection : NavigableMove.NextPage,
        "Home" => NavigableMove.RowStart,
        "End" => NavigableMove.RowEnd,
        _ => null,
    };
}
