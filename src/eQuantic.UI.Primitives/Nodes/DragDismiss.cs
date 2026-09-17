namespace eQuantic.UI.Primitives;

/// <summary>
/// Gestures v2 — VERTICAL drag-to-dismiss (the sheet contract): the child follows a downward drag
/// (paint-only translate — layout untouched); releasing past <see cref="ThresholdDp"/> fires
/// <see cref="OnDismiss"/> (state then removes the subtree and the EXIT motion completes from the
/// dragged position), releasing short glides back over Motion.Base. Taps inside still work — a drag
/// only engages past the Touch.PressCancelSlop, which also cancels the in-flight press. On web a
/// pointer-capture controller drives the same contract through the <c>data-eq-drag-dismiss</c>
/// marker. v1 fences: detents (partial heights), flick-velocity dismissal, horizontal axis, and
/// nested-scroll interplay.
/// </summary>
public sealed class DragDismiss : SingleChildNode
{
    public override string NodeKind => "dragDismiss";

    /// <summary>Release at or past this drag distance (dp) dismisses; short of it glides back.</summary>
    public const float ThresholdDp = 96;

    public DragDismiss(VisualNode child, Action? onDismiss = null)
        : base(child)
    {
        OnDismiss = onDismiss;
    }

    public Action? OnDismiss { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
