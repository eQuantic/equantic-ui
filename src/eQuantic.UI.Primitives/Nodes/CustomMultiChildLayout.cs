namespace eQuantic.UI.Primitives;

/// <summary>
/// Children laid out by a delegate the app writes in C#: Flutter's <c>CustomMultiChildLayout</c>. The
/// <see cref="Delegate"/> measures each child, known by its <see cref="LayoutId"/>, with the constraints it chooses
/// and places it, for a layout the other nodes do not have: a masonry grid, a radial menu.
/// <para>
/// The node takes its room as a <see cref="Box"/> that fills both axes, as <see cref="LayoutBuilder"/> does: the
/// delegate is told the size the parent decides on each axis it decides, and an unbounded maximum where the size is
/// left to what the node holds. The node is the room's size on an axis the parent decides and the delegate's on an
/// axis sized by content.
/// </para>
/// <para>
/// Photon runs the delegate in its layout pass. The browser runs it right after its own layout: the server writes
/// the children, hidden until the browser places them, and the browser lays them out again when the room or a
/// child's size changes. A reader reaches the children in their order here, wherever the delegate places them.
/// </para>
/// </summary>
public sealed class CustomMultiChildLayout : VisualNode
{
    /// <param name="layoutDelegate">How the children are sized and placed.</param>
    /// <param name="children">The children, each known to the delegate by its id.</param>
    public CustomMultiChildLayout(MultiChildLayoutDelegate layoutDelegate, IReadOnlyList<LayoutId>? children = null)
    {
        ArgumentNullException.ThrowIfNull(layoutDelegate);
        Delegate = layoutDelegate;
        Children = children ?? [];
        var seen = new HashSet<object>();
        foreach (var child in Children)
            if (!seen.Add(child.Id))
                throw new ArgumentException(
                    $"Two children of a CustomMultiChildLayout carry the id '{child.Id}': a delegate knows each child by one id.",
                    nameof(children));
    }

    public sealed override string NodeKind => "customMultiChildLayout";

    /// <summary>How the children are sized and placed.</summary>
    public MultiChildLayoutDelegate Delegate { get; }

    /// <summary>The children, in reading order, each known to the delegate by its id.</summary>
    public IReadOnlyList<LayoutId> Children { get; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
