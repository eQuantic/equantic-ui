namespace eQuantic.UI.Primitives;

/// <summary>Marks a flex child that shares LEFTOVER main-axis space by weight (spec A2 <c>Flex(n)</c>).</summary>
public sealed class Flexible : SingleChildNode
{
    public override string NodeKind => "flexible";

    /// <exception cref="ArgumentOutOfRangeException">
    /// A negative <paramref name="flex"/> or <paramref name="shrink"/>, or a <paramref name="basis"/>
    /// that is negative or not a finite number.
    /// </exception>
    public Flexible(VisualNode child, int flex = 1, float basis = 0, int shrink = 1)
        : base(child)
    {
        Flex = flex;
        Basis = basis;
        Shrink = shrink;
    }

    /// <summary>
    /// This child's share of the LEFTOVER main-axis space, by weight against its flexible siblings —
    /// CSS <c>flex-grow</c>, Flutter's <c>flex</c>.
    /// <para>
    /// Zero takes no share. The child is inflexible and keeps its <see cref="Basis"/>, or its own
    /// size when it has none: Flutter's "inflexible and determines its own size", CSS's
    /// <c>flex-grow: 0</c>. The constructor used to raise a zero to 1, so "start at 540 and never
    /// grow" rendered as an item that grew, and squeezed the text beside it (#680).
    /// </para>
    /// <para>
    /// A negative weight means nothing on any target (CSS drops a negative <c>flex-grow</c>), so it is
    /// refused HERE, where it is written, rather than clamped into a weight nobody wrote. The check
    /// is on the accessor, not the parameter, so an object initializer cannot walk past it.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The weight is negative.</exception>
    public int Flex
    {
        get;
        init => field = value >= 0 ? value
            : throw new ArgumentOutOfRangeException(nameof(Flex), value,
                "A flex weight is zero or more. Zero takes no share of the leftover: the child keeps its "
                + "basis, or its own size when it has none.");
    }

    /// <summary>
    /// The size this child STARTS from before any leftover is shared — CSS <c>flex-basis</c>.
    /// <para>
    /// Zero (the default) is no basis. A weighted child then contributes nothing of its own and is
    /// sized purely from its weight, the historical behaviour; a child of weight zero is sized by its
    /// content, because nothing else would give it a size. A non-zero basis is what makes a WRAPPING
    /// container work like the web's, because the basis is the size the line-breaker measures
    /// against: give two panes a basis of 440 and they sit side by side while there is room for both,
    /// then each takes a full line of its own. Without it a wrapping row could only ever place
    /// children at their natural size, which is why a responsive two-pane layout was previously
    /// inexpressible.
    /// </para>
    /// <para>
    /// A basis is a size in dp, so a negative one, or one that is not a finite number, is refused
    /// where it is written instead of being read as zero.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The basis is negative or not a finite number.</exception>
    public float Basis
    {
        get;
        init => field = value >= 0 && float.IsFinite(value) ? value
            : throw new ArgumentOutOfRangeException(nameof(Basis), value,
                "A flex basis is a size in dp, zero or more, and zero is no basis.");
    }

    /// <summary>
    /// How readily this child gives space back when the line overflows — CSS <c>flex-shrink</c>.
    /// Defaults to 1, matching what the web realizer has always emitted. Zero refuses to shrink, and
    /// a negative shrink means nothing on any target, so it is refused where it is written.
    /// Shrinking is weighted by the shrink times the size, as CSS scales it. A Flexible's minimum
    /// main size is zero (the web writes it <c>min-width: 0</c> in a row), so in a ROW a weight of
    /// zero shrinks past its child's min-content on both targets, and a child with a fixed width
    /// overflows it. The other items Photon shrinks keep their min-content floor, and so, for now,
    /// does a Flexible in a wrapping row, where the web lets it go past (see
    /// <see cref="FlexNode.Wrap"/>).
    /// <para>
    /// In a single-line COLUMN Photon takes nothing back from an overflowing line yet, from any
    /// item: a zero weight at a basis of 540 after a fixed 100, in a column 400 tall, stays 540
    /// there, where a browser shrinks it to 300.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The shrink is negative.</exception>
    public int Shrink
    {
        get;
        init => field = value >= 0 ? value
            : throw new ArgumentOutOfRangeException(nameof(Shrink), value,
                "A flex shrink is zero or more, and zero refuses to give space back.");
    }

    /// <summary>Animate WEIGHT changes at Base 200ms standard (spec B14: "value changes animate…").
    /// The composing component decides per render — forward-only contracts set it false on a
    /// regression so the change SNAPS (honesty over smoothness). Web = a flex-grow transition;
    /// native joins with the transition animator (until then weights snap, the documented fence).</summary>
    public bool AnimateChanges { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
