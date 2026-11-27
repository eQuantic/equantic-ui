namespace eQuantic.UI.Primitives;

/// <summary>
/// One child sized and placed by a delegate the app writes in C#: Flutter's <c>CustomSingleChildLayout</c>. The
/// <see cref="Delegate"/> says the node's size, the bounds the child is measured with and where it goes. The node
/// takes its room as <see cref="CustomMultiChildLayout"/> does, and every realizer lays it out as that node, with
/// <see cref="MultiChildDelegate"/> over <see cref="LaidOutChild"/>.
/// </summary>
public sealed class CustomSingleChildLayout : VisualNode
{
    /// <param name="layoutDelegate">How the node is sized and its child placed.</param>
    /// <param name="child">The child it places.</param>
    public CustomSingleChildLayout(SingleChildLayoutDelegate layoutDelegate, VisualNode child)
    {
        ArgumentNullException.ThrowIfNull(layoutDelegate);
        ArgumentNullException.ThrowIfNull(child);
        Delegate = layoutDelegate;
        Child = child;
        MultiChildDelegate = new SingleChildLayout(layoutDelegate);
        LaidOutChild = new LayoutId(SingleChildLayout.ChildId, child);
    }

    public sealed override string NodeKind => "customSingleChildLayout";

    /// <summary>How the node is sized and its child placed.</summary>
    public SingleChildLayoutDelegate Delegate { get; }

    /// <summary>The child it places.</summary>
    public VisualNode Child { get; }

    /// <summary><see cref="Delegate"/> as the delegate of a custom layout of one child, with Flutter's three steps
    /// in Flutter's order: how every realizer lays this node out.</summary>
    public MultiChildLayoutDelegate MultiChildDelegate { get; }

    /// <summary><see cref="Child"/>, known to <see cref="MultiChildDelegate"/> by its id: the node's one child slot.</summary>
    public LayoutId LaidOutChild { get; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
