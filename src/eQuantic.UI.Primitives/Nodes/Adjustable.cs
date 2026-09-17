namespace eQuantic.UI.Primitives;

/// <summary>
/// ADJUSTMENT semantics in the vocabulary: the child is a control whose value the arrow keys
/// nudge — a slider, a tab strip, a segmented choice, and whatever else answers to "a little
/// more / a little less". One Tab stop for the whole control (its inner press targets stay
/// pointer-only), arrows call <see cref="OnAdjust"/> with the direction, and the web twin is
/// <see cref="Role"/> with a keydown handler — the reason this is a NODE and not component
/// wiring: both realizers need to agree on what focus means here.
/// </summary>
public sealed class Adjustable : SingleChildNode
{
    public override string NodeKind => "adjustable";

    public Adjustable(VisualNode child, Action<int> onAdjust)
        : base(child)
    {
        OnAdjust = onAdjust;
    }


    /// <summary>+1 for the increasing arrow, −1 for the decreasing one. The CONTROL owns what a
    /// step is worth — the keyboard only says which way; whether the ends WRAP is the control's
    /// call too (a radio group wraps like the native one, a slider clamps).</summary>
    public Action<int> OnAdjust { get; init; }

    /// <summary>Announced by assistive tech, exactly as <see cref="Pressable.Label"/> is.</summary>
    public string Label { get; init; } = "";

    /// <summary>The ARIA identity of the web twin. The native side treats every role the same —
    /// one stop, arrows adjust.</summary>
    public AdjustableRole Role { get; init; } = AdjustableRole.Slider;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
