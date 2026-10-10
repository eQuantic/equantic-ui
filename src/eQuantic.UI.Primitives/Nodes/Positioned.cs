namespace eQuantic.UI.Primitives;

/// <summary>
/// Anchors a <see cref="Stack"/> child to the stack's edges (spec A3) — offsets may be negative
/// (the Badge overlay attaches at top −4 / end −4). Unset axes fall back to the stack alignment.
/// <para>
/// An edge is a point offset, a FRACTION of the stack, or both added: <c>TopFraction = 0.3f,
/// Top = -16</c> is 16dp above 30% of the stack's height — what a handoff writes as
/// <c>top: calc(30% - 16px)</c>, and the only way to place something on a box whose size the app
/// never learns. <see cref="ShiftX"/>/<see cref="ShiftY"/> then move the child by fractions of its
/// OWN size (Flutter's <c>FractionalTranslation</c>): <c>-0.5, -1</c> puts its bottom centre on the
/// anchor, which is how a tooltip sits above the point it names.
/// </para>
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

    /// <summary>Distance from the stack's top as a fraction of its height (0.3 = 30%), added to <see cref="Top"/>.</summary>
    public float? TopFraction { get; init; }

    /// <summary>Distance from the stack's end edge as a fraction of its width, added to <see cref="End"/>.</summary>
    public float? EndFraction { get; init; }

    /// <summary>Distance from the stack's bottom as a fraction of its height, added to <see cref="Bottom"/>.</summary>
    public float? BottomFraction { get; init; }

    /// <summary>Distance from the stack's start edge as a fraction of its width, added to <see cref="Start"/>.</summary>
    public float? StartFraction { get; init; }

    /// <summary>
    /// Moves the placed child right by this fraction of its OWN width (left when negative), as a
    /// CSS <c>translate</c> does in either writing direction: -0.5 centres it on its anchor. Applied after the edges, on both targets as layout
    /// (the hit region follows the drawn box).
    /// </summary>
    public float ShiftX { get; init; }

    /// <summary>Moves the placed child down by this fraction of its OWN height: -1 puts its bottom on its top anchor.</summary>
    public float ShiftY { get; init; }

    /// <summary>Whether the horizontal axis is anchored by an edge (a point or a fraction) rather than the stack's alignment.</summary>
    public bool AnchorsStart => Start is not null || StartFraction is not null;

    /// <summary>Whether the end edge is given, as a point or a fraction.</summary>
    public bool AnchorsEnd => End is not null || EndFraction is not null;

    /// <summary>Whether the top edge is given, as a point or a fraction.</summary>
    public bool AnchorsTop => Top is not null || TopFraction is not null;

    /// <summary>Whether the bottom edge is given, as a point or a fraction.</summary>
    public bool AnchorsBottom => Bottom is not null || BottomFraction is not null;

    /// <summary>Spec S7: explicit stacking inside the Stack — higher paints (and hit-tests) on top.
    /// Equal values keep declaration order (stable). 0 = flow order.</summary>
    public int Layer { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
