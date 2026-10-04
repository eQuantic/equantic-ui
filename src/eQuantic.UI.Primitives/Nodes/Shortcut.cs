namespace eQuantic.UI.Primitives;

/// <summary>
/// A KEYBOARD SHORTCUT that is live while this subtree is mounted (spec S8): the chord fires
/// <see cref="OnPressed"/> from anywhere on the page, which is exactly what a command palette (⌘K),
/// an Esc-dismiss and a list's ↑/↓ navigation need. Mounting IS the subscription and unmounting
/// removes it — no add/remove listener bookkeeping in app code: a dialog's Esc lives inside the
/// dialog, ⌘K at the page root. A chord that holds only while something is true, around a subtree
/// that stays (a find bar's Esc, an open list's arrows), stays mounted too and says whether it is
/// live with <see cref="Enabled"/>: mounted and unmounted around that subtree, it would move the
/// subtree in the tree, a new identity for everything under it.
/// <para>
/// A component's OWN chords are the other kind, and <see cref="FocusScoped"/> is for them: an
/// editor's ⌘F, a diff's F7. Of two page-wide bindings of one chord the later sibling answers, and
/// of two nested ones the inner, so with two editors on one page ⌘F opened the find of whichever came
/// last, wherever the keyboard was. Flutter's <c>Shortcuts</c> are always of this kind: a key travels
/// up from the focus.
/// </para>
/// <para>
/// Layout-transparent (the child lowers unchanged). Web installs ONE window keydown controller that
/// dispatches to the active bindings and calls <c>preventDefault</c> when a chord matches, so ⌘K
/// never reaches the browser's own search. SSR renders the child and marks the binding on it
/// (<c>data-eq-shortcut</c>) — a keyboard shortcut needs JS by construction. Photon: the host's
/// <c>KeyDown</c> asks the frame's bindings first, in the same order.
/// </para>
/// </summary>
public sealed class Shortcut : SingleChildNode
{
    public override string NodeKind => "shortcut";

    public Shortcut(VisualNode child, KeyChord chord, Action onPressed)
        : base(child)
    {
        Chord = chord;
        OnPressed = onPressed;
    }

    public KeyChord Chord { get; init; }
    public Action OnPressed { get; init; }

    /// <summary>
    /// Whether the chord answers only while the keyboard focus is inside this subtree: the chords a
    /// component keeps for itself, which two of it on one screen must not both answer. Off, the chord
    /// answers wherever the focus is.
    /// </summary>
    public bool FocusScoped { get; init; }

    /// <summary>
    /// Whether the chord answers at all. A chord that holds only while something is on screen, a
    /// find bar's Escape, stays in the tree, which keeps everything under it where it is, and says
    /// here whether it is live: not, and the key goes on to whatever else would take it, a dialog's
    /// Escape around the editor among them, as an Action that is not enabled lets it go in Flutter.
    /// Mounted and unmounted around its subtree instead, it moved that subtree in the tree.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
