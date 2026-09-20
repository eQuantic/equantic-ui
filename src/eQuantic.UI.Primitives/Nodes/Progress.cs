namespace eQuantic.UI.Primitives;

/// <summary>
/// PROGRESS semantics in the vocabulary: the child is a painting that REPORTS how far along
/// something is, and this says so to assistive tech. Layout-transparent and non-interactive — it
/// takes no focus, answers no key and adds no geometry, which is the whole difference from
/// <see cref="Adjustable"/>. A slider is MOVED; a progress bar is READ.
///
/// <para>
/// It is a NODE rather than component wiring for the same reason Adjustable is: both realizers have
/// to agree on what this means, and a screen reader on Photon must hear what one on the web reads.
/// Before it existed the ProgressBar lowered to a bare Row or Box — a pair of unlabelled divs to a
/// screen reader, and on Photon nothing at all.
/// </para>
///
/// <para>
/// <b><see cref="Value"/> null means INDETERMINATE, and that is a state rather than an omission</b>
/// — the INVERSE of the slider's rule, which is why the two cannot share a node. ARIA says an
/// indeterminate progressbar KEEPS its role and omits <c>aria-valuenow</c>, because "something is
/// happening and nobody knows how far" is exactly what a reader should say; a slider with no value,
/// by contrast, is not a slider at all and the web realizer refuses to call it one.
/// </para>
/// </summary>
public sealed class Progress : SingleChildNode
{
    public override string NodeKind => "progress";

    public Progress(VisualNode child)
        : base(child)
    {
    }

    /// <summary>
    /// What the progress is FOR, announced by assistive tech — "Uploading", never "45%". The number
    /// is <see cref="Value"/>'s job, and a label that carried it would change every frame, which
    /// reads to a screen reader as a different control each time.
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// How far along, over the range it covers — or <c>null</c> for INDETERMINATE. See the type
    /// note: null here is a legitimate state, unlike on <see cref="Adjustable"/>.
    /// </summary>
    public RangeValue? Value { get; init; }

    /// <summary>
    /// The value IN WORDS, when the number is not what a person would say — "3 of 7 files",
    /// "2 minutes left", "R$ 400", "Large". A reader that has this says it INSTEAD of the number.
    /// <para>
    /// On the NODE rather than inside <see cref="RangeValue"/>, which is #243: the words used to
    /// ride on the value, so a node with no value had no words either. For a bar that is INDETERMINATE that is exactly backwards: it has no number by definition, and "Estimating time remaining" is the case where a spoken description is most useful, because there is nothing to fall back on.
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
