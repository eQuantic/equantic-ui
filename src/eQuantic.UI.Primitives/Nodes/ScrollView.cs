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

    /// <summary>Where the view starts, in dp (≥ 0, toward the content end): read when it is first drawn,
    /// and never again. A component that moves its view later asks with <see cref="Request"/>.</summary>
    public float Offset { get; init; }

    /// <summary>
    /// Where the view is asked to go, honoured once per version (Flutter's <c>ScrollController.jumpTo</c>):
    /// an editor bringing a line to the middle of its view, a list going back to its top. Null asks for
    /// nothing, and a version already honoured moves nothing, so the reader's own scrolling stays.
    /// </summary>
    public ScrollRequest? Request { get; init; }

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

    /// <summary>
    /// Whether the content fills the viewport: it is stretched to the viewport on both
    /// axes, as a stretched child is, and scrolls past it only when it is longer. A size the content
    /// states on an axis stands, a fixed one or the window's, so a box of 40 stays 40. The room
    /// under a short document is then the document's own, which is what a code editor's press under its
    /// last line needs. The viewport is the room INSIDE the scroller: a scrollbar that takes space is
    /// not in it, so content that fills it never makes the scroller around it scroll.
    /// <para>
    /// Decided by layout, in the pass that sizes the viewport, on both targets. Flutter writes it with
    /// a <c>LayoutBuilder</c> and a <c>ConstrainedBox(minHeight:)</c> around the content; a size
    /// reported from layout and built back would lag a frame and count a scrollbar the content cannot
    /// use.
    /// </para>
    /// </summary>
    public bool FillViewport { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
