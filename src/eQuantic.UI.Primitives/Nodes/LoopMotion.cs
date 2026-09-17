namespace eQuantic.UI.Primitives;

/// <summary>
/// Continuous transform-only loop motion around one child (spec §06) — the indeterminate-progress /
/// shimmer building block. Deliberately a FUNCTION OF TIME, not a stateful animator: the native
/// realizer resolves the offset from the frame clock (pure, deterministic, golden-testable at a fixed
/// t) and re-renders while active; the web realizer lowers to generated CSS keyframes (the browser
/// owns the clock; `prefers-reduced-motion` statically disables it — the spec's Reduce Motion rule).
/// </summary>
public sealed class LoopMotion : SingleChildNode
{
    public override string NodeKind => "loopMotion";

    public LoopMotion(VisualNode child, LoopEffect effect, float fromX, float toX, int durationMs)
        : base(child)
    {
        Effect = effect;
        FromX = fromX;
        ToX = toX;
        DurationMs = durationMs;
    }

    public LoopEffect Effect { get; init; }

    /// <summary>Loop start offset as a fraction of the node's own width (e.g. -0.35 = -35%).</summary>
    public float FromX { get; init; }

    /// <summary>Loop end offset as a fraction of the node's own width.</summary>
    public float ToX { get; init; }

    /// <summary>Loop period. The motion is linear and repeats seamlessly from <see cref="FromX"/>.</summary>
    public int DurationMs { get; init; }

    /// <summary>Reduce Motion policy: <c>true</c> hides the subtree entirely at rest (decorative
    /// overlays like the Skeleton shimmer — spec B16's Reduce Motion IS the plain placeholder);
    /// <c>false</c> renders it at its natural position (an indeterminate bar keeps a still segment).</summary>
    public bool HideAtRest { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
