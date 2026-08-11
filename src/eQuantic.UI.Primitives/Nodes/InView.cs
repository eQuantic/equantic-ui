namespace eQuantic.UI.Primitives;

/// <summary>
/// Tells you when its child is ON SCREEN, and when it leaves.
/// <para>
/// The question a table of contents actually asks is "which heading is the reader looking at", and
/// the only way to answer it was the page's scroll position — which lives on a
/// <see cref="ScrollView"/>, so an article had to be wrapped in one. That changes the scroll model
/// of the whole page: the sticky header stops sticking to the window, the browser's own scroll
/// restoration stops working, and the address bar stops hiding on a phone.
/// </para>
/// <para>
/// A raw offset would not have helped much either. It is a number in the page's coordinates, and
/// answering "is this heading past it" needs every heading's position — measurement a write-once
/// component has no business doing, and cannot do the same way on both targets. Presence is the
/// question; presence is what this reports.
/// </para>
/// <para>
/// Layout-transparent: it takes no space and draws nothing. The web attaches an
/// <c>IntersectionObserver</c> (no scroll listener, so no work on frames where nothing crosses);
/// Photon compares bounds with the surface once a frame, which it is already walking.
/// </para>
/// </summary>
public sealed class InView : VisualNode
{
    public override string NodeKind => "inView";

    public InView(VisualNode child, Action<bool> onChanged)
    {
        Child = child;
        OnChanged = onChanged;
    }

    public VisualNode Child { get; init; }

    /// <summary>
    /// Fires on the TRANSITIONS only — true when the child comes into view, false when it goes.
    /// Not once a frame while it is there: a callback that fires sixty times a second is a callback
    /// every caller has to debounce, and the framework can just not do that.
    /// </summary>
    public Action<bool> OnChanged { get; init; }

    /// <summary>
    /// How much of the child has to be showing before it counts, 0–1. The default is any sliver of
    /// it; a table of contents usually wants a heading to count only once it is properly on screen.
    /// </summary>
    public float Threshold { get; init; }
}
