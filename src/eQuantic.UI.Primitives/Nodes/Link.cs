namespace eQuantic.UI.Primitives;

/// <summary>
/// NAVIGATION semantics in the vocabulary: the child becomes a link to <see cref="Destination"/>. The child
/// owns ALL visuals (like Pressable) — Link adds only the semantics and the interaction. Web lowers
/// to a real <c>&lt;a href&gt;</c> (SSR-crawlable; the SPA router intercepts internal clicks, so
/// guards/prefetch apply); native registers a link region and resolves a tap through the HOST's
/// navigation seam (<c>PhotonHost.NavigationRequested</c> — the platform shell maps hrefs to pages).
/// Pressables INSIDE a link win the tap (topmost dispatch), exactly like a button inside an anchor.
/// </summary>
public sealed class Link : SingleChildNode
{
    public override string NodeKind => "link";

    public Link(string destination, VisualNode child)
        : base(child)
    {
        Destination = destination;
    }

    public string Destination { get; init; }

    /// <summary>Accessible name when the child carries no text of its own (icon-only links).</summary>
    public string? Label { get; init; }

    /// <summary>
    /// Keeps the reader WHERE THEY ARE instead of starting the new page at its top.
    /// <para>
    /// Arriving at the top is right for a link that takes you somewhere else, and wrong for one that
    /// swaps a panel beside a list you were half-way down — a documentation sidebar being the case
    /// that names it: the content changes, everything jumps to the top, and a navigation that
    /// preserved the entire shell is indistinguishable from a page that reloaded.
    /// </para>
    /// <para>
    /// Chrome that keeps its own scrolling never had this problem and does not need this flag. It is
    /// for the layouts where the list and the content share one scroll.
    /// </para>
    /// </summary>
    public bool KeepsPosition { get; init; }

    /// <summary>
    /// This link points at the page the reader is ON — a sidebar's active row, a top bar's current
    /// section. The canonical <c>aria-current="page"</c>, and the web twin of
    /// <see cref="PressableRole.Destination"/>: a navigation that marks its active entry with a fill
    /// colour has told everyone who can see the colour and nobody else.
    /// <para>
    /// Left alone on ordinary links, and deliberately not a nullable: a link either is the current
    /// page or is not, and every other link in a page saying "not current" is noise a screen reader
    /// reads out loud.
    /// </para>
    /// </summary>
    public bool Current { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
