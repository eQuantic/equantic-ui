namespace eQuantic.UI.Primitives;

/// <summary>
/// A region whose CONTENT IS ANNOUNCED WHEN IT CHANGES, without moving focus. Layout-transparent
/// and non-interactive, like <see cref="Progress"/> — it takes no focus, answers no key and adds no
/// geometry; what it changes is whether a reader notices that the subtree is different.
///
/// <para>
/// It is a NODE rather than a component's own attribute because the components that need it could
/// not say it at all: a Banner that appears announced nothing (B18), and a Toast lowered to a
/// non-modal <see cref="Overlay"/>, which both realizers strip every semantic from (C4). The
/// vocabulary had NO live region — the only <c>aria-live</c> anywhere in the write-once path was the
/// text field's description — so each of them was a component-shaped hole over one missing word.
/// </para>
///
/// <para>
/// WHAT IT IS NOT, because the neighbouring rows look like this one and are not: a
/// <see cref="Progress"/> already announces through its role and value, and B14 asks for a
/// THROTTLED announcement (25% steps, and "once, not per frame" while indeterminate) — wrapping a
/// bar whose value moves every frame in a live region is the defect that row warns about, not its
/// cure. And B16's loading announcement belongs to the region that is loading, not to each
/// placeholder inside it: a table drawing twelve skeletons would announce twelve times.
/// </para>
///
/// <para>
/// <b>ANNOUNCING IS NOT LABELLING, and the difference is the whole point.</b> A label says what a
/// thing IS and is read when the user arrives at it. A live region says what just HAPPENED and is
/// read wherever the user happens to be. That is why this wraps rather than decorates: the region
/// is a piece of the tree the platform watches, and its contents are the announcement.
/// </para>
///
/// <para>
/// The name is not the web's. Android has <c>accessibilityLiveRegion</c> with
/// <c>ACCESSIBILITY_LIVE_REGION_POLITE</c> and <c>_ASSERTIVE</c>, Flutter has
/// <c>SemanticsProperties.liveRegion</c>, and UIKit posts
/// <c>UIAccessibility.Notification.announcement</c> — three platforms that would have needed this
/// word if the web had never existed.
/// </para>
/// </summary>
public sealed class LiveRegion : SingleChildNode
{
    public override string NodeKind => "liveRegion";

    public LiveRegion(VisualNode child)
        : base(child)
    {
    }

    /// <summary>
    /// How hard the announcement interrupts. <see cref="LiveRegionUrgency.Polite"/> by default
    /// because assertive is a cost paid by every other announcement on the page: a component asks
    /// for it when the user cannot go on without hearing this, and not to be noticed.
    /// </summary>
    public LiveRegionUrgency Urgency { get; init; } = LiveRegionUrgency.Polite;

    /// <summary>
    /// What the region is FOR, when the subtree alone does not say it — "Upload status" beside a
    /// bar whose own text is only a percentage. Left empty the announcement is the content, which
    /// is what a Banner or a Toast wants: its text already reads as a sentence.
    /// </summary>
    public string Label { get; init; } = "";

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
