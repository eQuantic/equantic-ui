namespace eQuantic.UI.Primitives;

/// <summary>
/// The id a <see cref="CustomMultiChildLayout"/>'s delegate knows a child by: Flutter's <c>LayoutId</c>. Ids are
/// compared by <see cref="object.Equals(object)"/>, and by value in the browser, so an <c>int</c>, a
/// <c>string</c>, an enum or a record means the same on every target. Two children of one node never share an id.
/// </summary>
public sealed class LayoutId : SingleChildNode
{
    public sealed override string NodeKind => "layoutId";

    /// <param name="id">What the delegate calls the child.</param>
    /// <param name="child">The child it lays out.</param>
    public LayoutId(object id, VisualNode child) : base(child)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(child);
        Id = id;
    }

    /// <summary>What the delegate calls the child.</summary>
    public object Id { get; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
