namespace eQuantic.UI.Primitives;

/// <summary>
/// Layout-only space (spec A4): draws nothing, hits nothing, announces nothing. The flexible form
/// collapses to 0 when siblings need the space; <see cref="Fixed"/> is a rigid one-off rhythm break
/// (prefer the parent's Gap).
/// </summary>
public sealed class Spacer : VisualNode
{
    public override string NodeKind => "spacer";

    public Spacer(int flex = 1) => Flex = Math.Max(1, flex);

    private Spacer(float fixedLength)
    {
        Flex = 0;
        FixedLength = fixedLength;
    }

    public int Flex { get; init; }
    public float FixedLength { get; init; }

    /// <summary>Spec B14: weight changes animate over Motion.Base — pair with an animated Flexible
    /// so the RATIO glides (constant denominator) instead of jumping when the counterweight snaps.</summary>
    public bool AnimateChanges { get; init; }

    public static Spacer Fixed(float length) => new(length);

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
