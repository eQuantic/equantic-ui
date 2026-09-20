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

    /// <summary>
    /// The ARIA identity the web twin ASKS FOR. It is honoured wherever ARIA allows it to be: the
    /// slider role requires <see cref="Value"/>, so a node that asks for it without one is announced
    /// as a group instead — the realizer settles the pair, because that rule is ARIA's and the
    /// realizer is where the SDK speaks ARIA. The native side treats every role the same — one stop,
    /// arrows adjust.
    /// </summary>
    public AdjustableRole Role { get; init; } = AdjustableRole.Slider;

    /// <summary>
    /// WHERE the value sits, for a role that has one (spec C7). Without it a slider is announced by
    /// NAME and nothing else: <c>role="slider"</c> requires <c>aria-valuenow</c>, so a host that
    /// emits the role and no value is invalid ARIA, and a screen-reader user hears what the control
    /// is for and never what it holds.
    /// <para>
    /// Null for the roles that have no such number — a tablist and a radiogroup announce a SELECTION
    /// (their items carry <c>aria-selected</c> / <c>aria-checked</c>), not a position on a range, and
    /// a value on those would be a second answer to a question their children already answer.
    /// </para>
    /// </summary>
    public RangeValue? Value { get; init; }

    /// <summary>
    /// The value IN WORDS, when the number is not what a person would say — "3 of 7 files",
    /// "2 minutes left", "R$ 400", "Large". A reader that has this says it INSTEAD of the number.
    /// <para>
    /// On the NODE rather than inside <see cref="RangeValue"/>, which is #243: the words used to
    /// ride on the value, so a node with no value had no words either. A slider always has one, so nothing was lost here — it moves for the same reason a shared type carries only what both share: the words are the NODE's, and putting them back on the value would give <see cref="Progress"/> two places to look.
    /// </para>
    /// </summary>
    public string? ValueText { get; init; }

    /// <summary>
    /// What a reader announces — the words when there are any, the number otherwise, and nothing
    /// at all when there is neither. ONE place to look, determinate or not.
    /// </summary>
    public string? Spoken => ValueText is { Length: > 0 } text ? text : Value?.Number;


    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
