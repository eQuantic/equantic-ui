namespace eQuantic.UI.Primitives;

/// <summary>
/// A list whose <see cref="SortableItem"/>s are put in order by DRAG along its <see cref="Axis"/>, and which
/// takes the items of every other sortable of its <see cref="Group"/> (#1535). The child is the list as it is
/// laid out, a column, a row, a scroller with a column inside: the node marks it and adds no box. An item
/// belongs to the nearest sortable above it.
/// <para>
/// The list is the APP's. A drop is reported, never applied: <see cref="OnDropped"/> hands over a
/// <see cref="SortDrop"/>, and the app moves the item in its own data and builds again, as Flutter's
/// <c>onReorder</c> leaves it to the app. While an item is carried over the list, <see cref="OnHovered"/>
/// reports where it would land, so the component draws its own indicator from the theme, and
/// <see cref="Accepts"/> is asked before a place is offered.
/// </para>
/// <para>
/// Realized on the web, where a mouse picks an item up past a few pixels and a finger after holding it
/// still; on Photon the items are drawn and take no drag yet (#1537). The keyboard's way to sort is #1536.
/// </para>
/// </summary>
public sealed class Sortable : SingleChildNode
{
    public override string NodeKind => "sortable";

    public Sortable(VisualNode child, string id, Action<SortDrop> onDropped)
        : base(child)
    {
        Id = id;
        OnDropped = onDropped;
    }

    /// <summary>The list's name in what it reports, <see cref="SortDrop.From"/> and <see cref="SortDrop.To"/>:
    /// a board's column id, say. Unique among the sortables of its group.</summary>
    public string Id { get; init; }

    /// <summary>An item dropped here, in this list's words. Never called for a place <see cref="Accepts"/>
    /// refused.</summary>
    public Action<SortDrop> OnDropped { get; init; }

    /// <summary>The lists that take each other's items share it: a board's columns. Empty, the default, is a
    /// group of its own: the list sorts only what it holds.</summary>
    public string Group { get; init; } = "";

    /// <summary>The direction the items run in, which decides where a pointer lands between two of them.</summary>
    public DragAxis Axis { get; init; }

    /// <summary>Whether an item may also land INTO another (the middle half of the other's box), which
    /// <see cref="SortDrop.Into"/> then names: a tree's folders.</summary>
    public bool Nests { get; init; }

    /// <summary>Where a carried item would land while it is over this list, each time that changes, and
    /// <c>null</c> once it leaves or the drag ends. Opt-in, because each report builds the component again.</summary>
    public Action<SortDrop?>? OnHovered { get; init; }

    /// <summary>The app's guard, asked before a place is offered: a place it refuses is neither reported nor
    /// dropped on. Null takes every place.</summary>
    public Func<SortDrop, bool>? Accepts { get; init; }

    /// <summary>The list's name to a reader: a labelled group on every target, and the list's name in what is
    /// said while an item is sorted by the keyboard ("position 2 of 3 in To do"), a column's title, say.
    /// Empty, the list is no group of its own, and the keyboard names it by its <see cref="Id"/>.</summary>
    public string Label { get; init; } = "";

    /// <summary>Whether the list takes items at all. A disabled list neither starts nor takes a drag.</summary>
    public bool Enabled { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
