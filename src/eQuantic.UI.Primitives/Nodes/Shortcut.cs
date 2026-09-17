namespace eQuantic.UI.Primitives;

/// <summary>
/// A KEYBOARD SHORTCUT that is live while this subtree is mounted (spec S8): the chord fires
/// <see cref="OnPressed"/> from anywhere on the page — it is not a focus-scoped handler, which is
/// exactly what a command palette (⌘K), an Esc-dismiss and a list's ↑/↓ navigation need. Mounting
/// IS the subscription: render the Shortcut only while its binding should apply (inside the open
/// dialog for Esc/arrows, at the page root for ⌘K) and unmounting removes it — no add/remove
/// listener bookkeeping in app code.
/// <para>
/// Layout-transparent (the child lowers unchanged). Web installs ONE window keydown controller that
/// dispatches to the active bindings and calls <c>preventDefault</c> when a chord matches, so ⌘K
/// never reaches the browser's own search. SSR renders the child and marks the binding on it
/// (<c>data-eq-shortcut</c>) — a keyboard shortcut needs JS by construction. Photon fence: the
/// host's key pipeline (desktop shells) — bindings are inert on native until it lands.
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

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
