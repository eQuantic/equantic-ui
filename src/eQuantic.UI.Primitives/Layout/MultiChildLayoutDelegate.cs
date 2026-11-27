namespace eQuantic.UI.Primitives;

/// <summary>
/// How a <see cref="CustomMultiChildLayout"/> sizes itself and lays out its children, written by the app in C#:
/// Flutter's <c>MultiChildLayoutDelegate</c>. The app extends it, measures each child with the constraints it
/// chooses (<see cref="LayoutChild"/>, which answers the child's size) and places it (<see cref="PositionChild"/>).
/// <para>
/// Flutter's two steps are here: <see cref="GetSize"/> says the node's size before its children are laid out,
/// and <see cref="PerformLayout"/> lays them out in it. <see cref="Layout"/> is the one step both targets call,
/// and its default runs those two. A delegate whose size follows its children (a masonry grid's height)
/// overrides <see cref="Layout"/> instead: it lays them out and answers the size it settled on, which Flutter's
/// delegate cannot do (<c>boxy</c>'s <c>BoxyDelegate</c> is that shape).
/// </para>
/// <para>
/// Photon runs it in its layout pass, as Flutter does; the browser runs the transpiled class right after its own
/// layout. Each child is laid out exactly once per layout, as Flutter asks: <see cref="HasChild"/> is how a delegate
/// lays out a child that is not always there.
/// </para>
/// </summary>
public abstract class MultiChildLayoutDelegate
{
    private ILayoutChildren? _children;
    private readonly HashSet<object> _laidOut = new();

    /// <summary>The node's size for <paramref name="constraints"/>, before any child is laid out: the biggest
    /// the room allows by default, as Flutter's. An axis the room leaves unbounded needs a finite size.</summary>
    public virtual Size GetSize(BoxConstraints constraints) => constraints.Biggest;

    /// <summary>Lays the children out in a node of <paramref name="size"/>, with <see cref="LayoutChild"/> and
    /// <see cref="PositionChild"/>.</summary>
    public virtual void PerformLayout(Size size)
    {
    }

    /// <summary>
    /// Lays the children out for the room <paramref name="constraints"/> and answers the node's size: by default
    /// <see cref="GetSize"/>, constrained to the room, then <see cref="PerformLayout"/>. Override it to size the
    /// node from its children.
    /// </summary>
    public virtual Size Layout(BoxConstraints constraints)
    {
        var size = constraints.Constrain(GetSize(constraints));
        PerformLayout(size);
        return size;
    }

    /// <summary>Whether this delegate lays out otherwise than <paramref name="oldDelegate"/>, the one it replaces
    /// when the node is built again: a realizer that keeps a layout runs it again only when this says so.</summary>
    public virtual bool ShouldRelayout(MultiChildLayoutDelegate oldDelegate) => true;

    /// <summary>Whether a child carries <paramref name="id"/>.</summary>
    protected bool HasChild(object id) => Bound().Has(id);

    /// <summary>Lays the child with <paramref name="id"/> out as the content of a box with
    /// <paramref name="constraints"/> as its bounds, and answers its size. Each child is laid out once.</summary>
    /// <exception cref="InvalidOperationException">No child carries the id, or it was laid out already.</exception>
    protected Size LayoutChild(object id, BoxConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        var children = Bound();
        if (!children.Has(id))
            throw new InvalidOperationException($"{Name} laid out a child with the id '{id}', which no child carries.");
        if (!_laidOut.Add(id))
            throw new InvalidOperationException($"{Name} laid out the child with the id '{id}' twice: each child is laid out once.");
        return children.Layout(id, constraints);
    }

    /// <summary>Places the child with <paramref name="id"/> at <paramref name="offset"/> from the node's origin.</summary>
    /// <exception cref="InvalidOperationException">No child carries the id.</exception>
    protected void PositionChild(object id, Point offset)
    {
        var children = Bound();
        if (!children.Has(id))
            throw new InvalidOperationException($"{Name} placed a child with the id '{id}', which no child carries.");
        children.Position(id, offset);
    }

    /// <summary>
    /// Runs <see cref="Layout"/> with <paramref name="children"/> bound for its length, and answers the node's
    /// size. A realizer's door: an app writes the steps, never calls this.
    /// </summary>
    /// <exception cref="InvalidOperationException">The size is not finite on an axis the room leaves unbounded, or a
    /// child was not laid out.</exception>
    /// <param name="children">The children, bound for this layout.</param>
    /// <param name="room">The room the node has.</param>
    /// <param name="ids">Every id a child carries, so a child left out is named.</param>
    public Size LayoutWith(ILayoutChildren children, BoxConstraints room, IReadOnlyList<object> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(room);
        if (_children is not null)
            throw new InvalidOperationException($"{Name} is laying out already: a delegate lays out one node at a time.");
        _children = children;
        _laidOut.Clear();
        try
        {
            var size = Layout(room);
            if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height))
                throw new InvalidOperationException(
                    $"{Name} answered a size of {size.Width} by {size.Height} for a room of {room}: a custom "
                    + "layout's size is finite, so an axis the room leaves unbounded needs a size of the delegate's own.");
            foreach (var id in ids)
                if (!_laidOut.Contains(id))
                    throw new InvalidOperationException(
                        $"{Name} did not lay out the child with the id '{id}': each child is laid out exactly once.");
            return size;
        }
        finally
        {
            _children = null;
            _laidOut.Clear();
        }
    }

    /// <summary>The delegate's name in what a refusal says: the app's class, or the single child's delegate a
    /// <see cref="CustomSingleChildLayout"/> lays out through this one.</summary>
    private protected virtual string Name => GetType().Name;

    private ILayoutChildren Bound() => _children ?? throw new InvalidOperationException(
        $"{Name} reached its children outside a layout: they are reached only from GetSize, PerformLayout and Layout.");
}
