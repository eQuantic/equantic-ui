namespace eQuantic.UI.Primitives;

/// <summary>
/// Anchors a <see cref="Stack"/> child to the stack's edges (spec A3) — offsets may be negative
/// (the Badge overlay attaches at top −4 / end −4). Unset axes fall back to the stack alignment.
/// </summary>
public sealed class Positioned : SingleChildNode
{
    public sealed override string NodeKind => "positioned";

    public Positioned(VisualNode child, float? top = null, float? end = null,
        float? bottom = null, float? start = null)
        : base(child)
    {
        Top = top;
        End = end;
        Bottom = bottom;
        Start = start;
    }

    public float? Top { get; init; }
    public float? End { get; init; }
    public float? Bottom { get; init; }
    public float? Start { get; init; }

    /// <summary>Spec S7: explicit stacking inside the Stack — higher paints (and hit-tests) on top.
    /// Equal values keep declaration order (stable). 0 = flow order.</summary>
    public int Layer { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
