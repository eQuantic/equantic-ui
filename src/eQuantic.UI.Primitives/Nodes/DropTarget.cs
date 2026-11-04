namespace eQuantic.UI.Primitives;

/// <summary>
/// An area a person drops FILES onto: the child is the area, and <see cref="OnDropped"/> hands over
/// what landed on it as <see cref="PickedFile"/>s, the same currency <see cref="IFilePicker"/> answers
/// with. Layout-transparent: the child owns every visual, and the caller paints the invitation from
/// <see cref="OnDragOverChanged"/>, as a <see cref="Hoverable"/>'s caller paints its hover.
/// <para>
/// Dropping is a pointer's gesture, so a drop area is never the only way in: put a button that asks an
/// <see cref="IFilePicker"/> beside it, which is what a keyboard or a screen reader can reach.
/// </para>
/// <para>
/// Only files count. Text or a link dragged over the area is not an invitation, and a disabled area
/// takes nothing while saying so, rather than letting the drop fall through to whatever is behind it.
/// </para>
/// </summary>
public sealed class DropTarget : SingleChildNode
{
    public override string NodeKind => "dropTarget";

    public DropTarget(VisualNode child, Action<IReadOnlyList<PickedFile>> onDropped)
        : base(child)
    {
        OnDropped = onDropped;
    }

    /// <summary>The files that landed on the area, in the order the target listed them. Never empty.</summary>
    public Action<IReadOnlyList<PickedFile>> OnDropped { get; init; }

    /// <summary>
    /// <c>true</c> when files are carried over the area and <c>false</c> when they leave it or land on
    /// it. Reported for a disabled area too, so it can show why it takes nothing.
    /// </summary>
    public Action<bool>? OnDragOverChanged { get; init; }

    /// <summary>Whether the area takes what is dropped on it. A disabled one refuses the drop.</summary>
    public bool Enabled { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
