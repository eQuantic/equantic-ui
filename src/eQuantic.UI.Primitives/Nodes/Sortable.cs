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
/// A mouse picks an item up past a few pixels and a finger after holding it still, on the web and on Photon
/// (#1537), and the keyboard sorts it too (#1536), each step said to a reader in the list's
/// <see cref="Announcements"/> (#1564).
/// </para>
/// </summary>
public sealed class Sortable : SingleChildNode
{
    public override string NodeKind => "sortable";

    public Sortable(VisualNode child, string id, Action<SortDrop> onDropped, SortAnnouncements? announcements = null,
        SortableTheme? style = null)
        : base(child)
    {
        Id = id;
        OnDropped = onDropped;
        Announcements = announcements;
        Style = style;
    }

    /// <summary>The look of an item carried from this list (#1602), laid over the theme's
    /// (<see cref="IAppTheme.Sortable"/>) member by member, on every target: the item dimmed in its place, the copy
    /// that follows a pointer, its shadow and how far it leans. Null takes the theme's look. A carried item keeps
    /// the look of the list it was picked up from, in every list of the group.</summary>
    public SortableTheme? Style { get; init; }

    /// <summary>What a reader hears while an item of this list is sorted by the keyboard: how to, and each step,
    /// on every target, in the words of the list the item was picked up from. <c>UI.Sortable</c> gives the SDK's
    /// in the reader's culture unless the app gives its own; null says nothing, since no realizer has words of
    /// its own to fall back on.</summary>
    public SortAnnouncements? Announcements { get; init; }

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

    /// <summary>
    /// How far across the <see cref="Axis"/> one depth of the items is indented, in logical pixels: a tree's
    /// indentation per level (#1594). Above 0 the list's items have depths (<see cref="SortableItem.Depth"/>): an item
    /// carries the deeper items right after it, its subtree, and a place between two items says in
    /// <see cref="SortDrop.Depth"/> the depth the item takes there, which a pointer picks by its travel across the axis
    /// and the keyboard by its cross arrows (<see cref="SortPlacement.Depths"/>). 0, the default, the list has none.
    /// </summary>
    public float Indent { get; init; }

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
