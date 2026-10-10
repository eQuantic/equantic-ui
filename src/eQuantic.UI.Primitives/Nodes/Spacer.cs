namespace eQuantic.UI.Primitives;

/// <summary>
/// Layout-only space (spec A4): draws nothing, hits nothing, announces nothing. The flexible form
/// collapses to 0 when siblings need the space; <see cref="Fixed"/> is a rigid one-off rhythm break
/// (prefer the parent's Gap).
/// </summary>
public sealed class Spacer : VisualNode
{
    public override string NodeKind => "spacer";

    /// <exception cref="ArgumentOutOfRangeException">A weight below 1.</exception>
    public Spacer(int flex = 1) => Flex = flex;

    private Spacer(float fixedLength)
    {
        // The RIGID form has no weight at all, which is the one zero this node holds: it is written
        // here, past the accessor that refuses a zero an app writes.
        _flex = 0;
        FixedLength = fixedLength;
    }

    /// <summary>
    /// The flexible form's share of the leftover, by weight against its flexible siblings: 1 or
    /// more. A spacer with no share has no size, so a weight below 1 is refused where it is written,
    /// as Flutter asserts <c>flex &gt; 0</c> for its own <c>Spacer</c>, rather than raised to 1
    /// without a word (#691). The check is on the accessor, so an object initializer meets it too.
    /// A rigid gap is <see cref="Fixed"/>, whose weight is 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The weight is below 1.</exception>
    public int Flex
    {
        get => _flex;
        init => _flex = value >= 1 ? value
            : throw new ArgumentOutOfRangeException(nameof(Flex), value,
                "A spacer's weight is 1 or more: a spacer with no share has no size. A rigid gap is "
                + "Spacer.Fixed(length), Gap(length) through the factories.");
    }

    private readonly int _flex;

    public float FixedLength { get; init; }

    /// <summary>Spec B14: weight changes animate over Motion.Base — pair with an animated Flexible
    /// so the RATIO glides (constant denominator) instead of jumping when the counterweight snaps.</summary>
    public bool AnimateChanges { get; init; }

    public static Spacer Fixed(float length) => new(length);

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
