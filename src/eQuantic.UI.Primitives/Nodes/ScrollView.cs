namespace eQuantic.UI.Primitives;

/// <summary>
/// A scrolling viewport (spec A6) — BOUNDED content only (virtualized lists are the List component).
/// The child lays out UNBOUNDED on the scroll axis and is clipped to the viewport. v1 fences: the
/// platform physics (decay/fling/rubber-band), gesture capture and the fading scrollbar pill join
/// with the native interaction system; today the scroll position is the programmatic
/// <see cref="Offset"/> (web realizes as native browser scrolling, which owns its own physics).
/// </summary>
public sealed class ScrollView : SingleChildNode
{
    public sealed override string NodeKind => "scrollView";

    public ScrollView(VisualNode child, ScrollAxis axis = ScrollAxis.Vertical)
        : base(child)
    {
        Axis = axis;
    }

    public ScrollAxis Axis { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>Programmatic scroll position in dp (≥ 0, toward the content end).</summary>
    public float Offset { get; init; }

    /// <summary>
    /// Where it IS, whenever that changes — the out channel to the in channel above. A component
    /// that has to know: a list that only builds the rows you can see, a header that shrinks, a
    /// "back to top" that appears past a fold. Without it the offset lives in the host and no
    /// component can ask, which is the difference between a list that scrolls and a list that
    /// can be long.
    /// </summary>
    public Action<float>? OnScrolled { get; init; }

    /// <summary>How tall the viewport turned out to be, reported once it is known. A window over a
    /// long document is (offset, height) and neither is knowable before layout.</summary>
    public Action<float>? OnViewportChanged { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
