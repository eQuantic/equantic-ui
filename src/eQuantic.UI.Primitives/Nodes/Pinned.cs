namespace eQuantic.UI.Primitives;

/// <summary>
/// Spec S7 — scroll-anchored chrome (section headers): the child renders in flow, but PINS to the
/// start of the scroll viewport once scrolling would push it out, offset by <see cref="Offset"/>.
/// v1 scope: vertical scrolling (CSS <c>position: sticky; top</c>); the Photon pinning joins the
/// native scroll compositor when engine scrolling lands — until then it renders in flow (correct
/// at scroll offset 0).
/// </summary>
public sealed class Pinned : SingleChildNode
{
    public override string NodeKind => "pinned";

    public Pinned(VisualNode child, float offset = 0)
        : base(child)
    {
        Offset = offset;
    }


    /// <summary>Distance from the viewport's start edge while pinned (dp).</summary>
    public float Offset { get; init; }

    /// <summary>
    /// FLOATING chrome (the fixed header): the bar paints ABOVE the page at the viewport edge and
    /// takes no layout space — content slides underneath (give the first section its own top
    /// padding). CSS <c>position:fixed; inset-inline:0</c>; Photon fence: the native realizer
    /// pins floating chrome with the overlay pass when the shell lands.
    /// </summary>
    public bool Float { get; init; }

    /// <summary>
    /// SCROLL-LINKED style (the handoff's transparent-until-scrolled header): this diff applies
    /// while the window has scrolled past a small threshold. Web = a root-gated rule set
    /// (<c>html.eq-scrolled …</c>) toggled by a tiny passive listener; Photon fence: joins the
    /// host scroll compositor. <c>null</c> = none.
    /// </summary>
    public StyleDiff? ScrolledStyle { get; init; }

    /// <summary>Spec S6: animates the swap INTO and out of <see cref="ScrolledStyle"/> — without it
    /// the bar flips from transparent to veiled in one frame. <c>null</c> = snap.</summary>
    public TransitionSpec? Transition { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
