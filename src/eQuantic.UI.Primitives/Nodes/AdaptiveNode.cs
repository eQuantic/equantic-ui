namespace eQuantic.UI.Primitives;

/// <summary>
/// Spec S6 — a subtree that ADAPTS to the window size class: up to three variants, resolved by the
/// fallback chain (Expanded → Medium → Compact). Fully general responsiveness — a different nav, a
/// different grid, a different direction — with ZERO listeners: the web realizer emits every
/// declared variant gated by build-time media queries (display:contents/none); Photon lays out only
/// the variant matching the window class and re-lays-out when the class crosses a threshold.
/// <para>
/// Because the web emits EVERY arm, anything inside one that must be unique in the document is
/// emitted once per arm it appears in — a <see cref="VisualNode.Bookmark"/> most visibly, since a
/// fragment then resolves to the copy in the hidden arm and the page does not move. So put the
/// shared subtree in the tree ONCE and make only the varying part adaptive: a legal page went from
/// two arms each holding the same article to one row whose ASIDE is the adaptive node, which reads
/// better besides.
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

    /// <summary>The variant for a WIDTH (dp) — the general form, honoring custom thresholds.
    /// Missing variants fall back toward Compact.</summary>
    public VisualNode ResolveWidth(float widthDp)
    {
        if (Expanded is { } expanded && widthDp >= ExpandedFrom) return expanded;
        // This arm is also the fallback the third branch used to repeat: a width past the Expanded
        // threshold with no Expanded variant is past the Medium one too, so it lands here.
        if (Medium is { } medium && widthDp >= MediumFrom) return medium;
        return Compact;
    }

    /// <summary>The variant for a size CLASS — the spec-threshold shorthand (custom thresholds
    /// resolve through <see cref="ResolveWidth"/>).</summary>
    public VisualNode Resolve(WindowSizeClass sizeClass) => sizeClass switch
    {
        WindowSizeClass.Expanded => Expanded ?? Medium ?? Compact,
        WindowSizeClass.Medium => Medium ?? Compact,
        _ => Compact,
    };

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
