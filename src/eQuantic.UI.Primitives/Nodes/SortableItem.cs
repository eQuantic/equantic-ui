namespace eQuantic.UI.Primitives;

/// <summary>
/// One item of a <see cref="Sortable"/>, picked up by a pointer that means to move it (#1535): a mouse past
/// a few pixels of travel, a finger held still a moment. A press that does not travel is a press, so a card
/// opens as it did, and the click that ends a drag never reaches it. The child is the item as drawn; the
/// node adds no box.
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

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
