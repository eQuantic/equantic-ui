namespace eQuantic.UI.Primitives;

/// <summary>
/// A press-interaction surface: wraps a child, exposes an activation callback, and guarantees the
/// spec §08 hit contract — the hit rect is expanded symmetrically to at least 48×48dp even when the
/// visual is smaller (realizers register it; overlapping hit rects assert in debug).
/// </summary>
public sealed class Pressable : SingleChildNode
{
    public override string NodeKind => "pressable";

    public Pressable(VisualNode child, Action? onPressed = null)
        : base(child)
    {
        OnPressed = onPressed;
    }

    public Action? OnPressed { get; init; }
    public bool Disabled { get; init; }

    /// <summary>Pressed-state fill (spec §01: pressed is a REAL token swap on the same rrect — never
    /// an overlay). Framework-applied: web = generated `:active` CSS driven by a per-element custom
    /// property; native = the realizer swaps the first descendant Box fill while the press is held.
    /// Null = no pressed visual.</summary>
    public ColorToken? PressedBackground { get; init; }

    /// <summary>Accessible name (role: button). Required when the child carries no text.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// SELECTION state for a button that toggles or picks one of a set — a segmented control's
    /// segment, a filter chip, a page dot. Selection that lives only in a fill colour is invisible
    /// to assistive tech, so this states it: web lowers to <c>aria-pressed</c> (or
    /// <c>aria-checked</c> when <see cref="Role"/> says Radio, Checkbox or Switch), native reports
    /// it as the node's selected state. <c>null</c> = the button does not carry selection at all,
    /// which is NOT the same as <c>false</c> ("selectable, currently not selected").
    /// </summary>
    public bool? Selected { get; init; }

    /// <summary>
    /// When a focus trap opens around this pressable, the INITIAL focus lands here rather than on
    /// the first focusable — §10's rule for a confirm dialog: the SAFE action (Ghost) takes the
    /// focus, so Enter pressed on reflex cancels instead of destroying. A static marker the trap
    /// controller prefers; meaningless outside a modal layer.
    /// </summary>
    public bool InitialFocus { get; init; }

    /// <summary>
    /// This pressable OPENS something, and whether it is open right now — an accordion header, a
    /// select's field, a menu's trigger. Painted state (a rotated chevron) says nothing to
    /// assistive tech; this lowers to <c>aria-expanded</c> on the web and the expanded bit of the
    /// native semantics node. <c>null</c> = the button does not control a disclosure at all,
    /// which is NOT the same as <c>false</c> ("controls one, currently closed").
    /// </summary>
    public bool? Expanded { get; init; }

    /// <summary>
    /// PARTLY selected — the "select all" checkbox over a mixed set. Only a
    /// <see cref="PressableRole.Checkbox"/> can say it (ARIA's own rule: <c>aria-checked="mixed"</c>
    /// exists for checkboxes and nothing else), and it wins over <see cref="Selected"/>, because
    /// "partly" is a statement about the whole set. It is a separate flag rather than a third value
    /// of Selected so the forty callers that answer a yes/no question keep answering one.
    /// </summary>
    public bool Mixed { get; init; }

    /// <summary>
    /// What this pressable IS when it stands inside a composite control. <see cref="PressableRole.Radio"/>
    /// is one choice of an exclusive set (a RadioGroup row, a SegmentedControl segment): the web
    /// lowers it as a radio — <c>role="radio"</c>, <see cref="Selected"/> as <c>aria-checked</c> —
    /// and takes it OUT of the tab order, because the composite's <see cref="Adjustable"/> wrapper
    /// is the one Tab stop and the arrows do the walking. Still pressable by pointer. Native fence:
    /// the semantics tree carries no checked bit yet; the role joins its expansion.
    /// </summary>
    public PressableRole Role { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
