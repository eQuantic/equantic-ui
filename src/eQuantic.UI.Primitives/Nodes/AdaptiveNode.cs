namespace eQuantic.UI.Primitives;

/// <summary>
/// Spec S6 — a subtree that ADAPTS to the window's width: up to three variants, resolved by the
/// fallback chain (Expanded → Medium → Compact) at the node's OWN thresholds (<see cref="MediumFrom"/>,
/// <see cref="ExpandedFrom"/>). Fully general responsiveness — a different nav, a different grid, a
/// different direction — with ZERO listeners: the web realizer emits every declared variant gated by
/// build-time media queries on those thresholds (display:contents/none); Photon and the element tree
/// hold and lay out only the variant <see cref="ResolveWidth"/> answers for the window's width, the same
/// width the web's gates read, so every target switches at the same place (#1465).
/// <para>
/// Because the web emits EVERY arm, anything inside one that must be unique in the document is
/// emitted once per arm it appears in — a <see cref="VisualNode.Bookmark"/> most visibly, since a
/// fragment then resolves to the copy in the hidden arm and the page does not move. So put the
/// shared subtree in the tree ONCE and make only the varying part adaptive: a legal page went from
/// two arms each holding the same article to one row whose ASIDE is the adaptive node, which reads
/// better besides.
/// </para>
/// <para>
/// The node is never laid out itself: its PARENT lays out each arm as a child of its own. So a
/// Spacer arm is space on the parent's axis, a Positioned arm is anchored in its Stack, and an
/// arm's own <see cref="VisualNode.AlignSelf"/> and <see cref="VisualNode.GridSpan"/> place it in
/// its line or grid. Say them on the ARM: the node's own are not read.
/// </para>
/// </summary>
public sealed class AdaptiveNode : VisualNode
{
    public override string NodeKind => "adaptive";

    public AdaptiveNode(VisualNode compact, VisualNode? medium = null, VisualNode? expanded = null)
    {
        Compact = compact;
        Medium = medium;
        Expanded = expanded;
    }

    public VisualNode Compact { get; }
    public VisualNode? Medium { get; }
    public VisualNode? Expanded { get; }

    /// <summary>
    /// Where <see cref="Medium"/> takes over (dp). Defaults to the spec's
    /// <see cref="WindowSizeClass"/> threshold; override when the DESIGN switches elsewhere — a bar
    /// whose nav needs 1024dp of room has no business flipping at 600.
    /// </summary>
    public float MediumFrom { get; init; } = WindowSizeClasses.MediumMinDp;

    /// <summary>Where <see cref="Expanded"/> takes over (dp). See <see cref="MediumFrom"/>.</summary>
    public float ExpandedFrom { get; init; } = WindowSizeClasses.ExpandedMinDp;

    /// <summary>The variant for a window <paramref name="widthDp"/> wide, at this node's thresholds: the one
    /// every target holds and draws (the web's gates show it at the same widths). Missing variants fall back
    /// toward Compact.</summary>
    public VisualNode ResolveWidth(float widthDp)
    {
        if (Expanded is { } expanded && widthDp >= ExpandedFrom) return expanded;
        // This arm is also the fallback the third branch used to repeat: a width past the Expanded
        // threshold with no Expanded variant is past the Medium one too, so it lands here.
        if (Medium is { } medium && widthDp >= MediumFrom) return medium;
        return Compact;
    }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
