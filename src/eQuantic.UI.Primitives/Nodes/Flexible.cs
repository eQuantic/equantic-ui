namespace eQuantic.UI.Primitives;

/// <summary>Marks a flex child that shares LEFTOVER main-axis space by weight (spec A2 <c>Flex(n)</c>).</summary>
public sealed class Flexible : SingleChildNode
{
    public override string NodeKind => "flexible";

    public Flexible(VisualNode child, int flex = 1, float basis = 0, int shrink = 1)
        : base(child)
    {
        Flex = Math.Max(1, flex);
        Basis = MathF.Max(0, basis);
        Shrink = Math.Max(0, shrink);
    }

    public int Flex { get; init; }

    /// <summary>
    /// The size this child STARTS from before any leftover is shared — CSS <c>flex-basis</c>.
    /// <para>
    /// Zero (the default) is the historical behaviour: the child contributes nothing of its own and
    /// is sized purely from its weight. A non-zero basis is what makes a WRAPPING container work
    /// like the web's, because the basis is the size the line-breaker measures against: give two
    /// panes a basis of 440 and they sit side by side while there is room for both, then each takes
    /// a full line of its own. Without it a wrapping row could only ever place children at their
    /// natural size, which is why a responsive two-pane layout was previously inexpressible.
    /// </para>
    /// </summary>
    public float Basis { get; init; }

    /// <summary>
    /// How readily this child gives space back when the line overflows — CSS <c>flex-shrink</c>.
    /// Defaults to 1, matching what the web realizer has always emitted. Zero refuses to shrink.
    /// Shrinking is weighted by the basis, as CSS does, and never crosses the min-content floor.
    /// </summary>
    public int Shrink { get; init; }

    /// <summary>Animate WEIGHT changes at Base 200ms standard (spec B14: "value changes animate…").
    /// The composing component decides per render — forward-only contracts set it false on a
    /// regression so the change SNAPS (honesty over smoothness). Web = a flex-grow transition;
    /// native joins with the transition animator (until then weights snap, the documented fence).</summary>
    public bool AnimateChanges { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
