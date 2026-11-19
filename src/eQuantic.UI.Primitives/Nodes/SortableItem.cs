namespace eQuantic.UI.Primitives;

/// <summary>
/// One item of a <see cref="Sortable"/>, picked up by a pointer that means to move it (#1535): a mouse past
/// a few pixels of travel, a finger held still a moment. A press that does not travel is a press, so a card
/// opens as it did, and the click that ends a drag never reaches it. The child is the item as drawn; the
/// node adds no box.
/// <para>
/// The keyboard sorts it too (#1536): Space picks it up, the arrows move it, Space drops it, Escape puts it
/// back, each step said to a reader. A child that takes no focus of its own (a box rather than a button)
/// becomes a Tab stop for that.
/// </para>
/// </summary>
public sealed class SortableItem : SingleChildNode
{
    public override string NodeKind => "sortableItem";

    public SortableItem(VisualNode child, string id)
        : base(child)
    {
        Id = id;
    }

    /// <summary>The item's name in what its list reports (<see cref="SortDrop.Item"/>): a card's id, say.
    /// Unique among the items of its group. Not the node's <see cref="VisualNode.Key"/>, which is its
    /// identity among its siblings in the tree.</summary>
    public string Id { get; init; }

    /// <summary>Whether a pointer may pick the item up. A disabled one stays where it is.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The item's name in what is said while the keyboard sorts it ("Picked up Amount, position 5 of 5
    /// in Columns"), on every target (#1538): a table's heading is named by its column, not by the press it
    /// holds ("Sort by Amount"). Empty, the default, names the item by what its subtree says. Not a label: the
    /// item marks its child and adds no accessible name of its own, so a reader landing on the heading still
    /// hears its press.</summary>
    public string Name { get; init; } = "";

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
