namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="TableViewport"/> is asked to do with its scroll: go to an <see cref="Offset"/>, or
/// bring a range of cells into view by the least movement (<see cref="Reveal"/>). A tree is built
/// again and again, so the request carries a <see cref="Version"/>, and the viewport honours each
/// version once: a request still in the tree after the user scrolled away moves nothing.
/// <para>
/// The precedent is the code editor's caret, which a host reveals when the editor's reveal version
/// moves. A component that wants the active cell kept in view raises the version on every move.
/// </para>
/// </summary>
public sealed record TableScrollRequest(int Version)
{
    /// <summary>Where to scroll to, clamped to the grid; ignored when <see cref="Reveal"/> is set.</summary>
    public TableOffset? Offset { get; init; }

    /// <summary>The cells to bring into the part of the window the pinned bands leave uncovered.</summary>
    public TableRange? Reveal { get; init; }
}
