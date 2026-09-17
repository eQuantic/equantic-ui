namespace eQuantic.UI.Primitives;

/// <summary>
/// Z-axis composition (spec A3): paint order = child order (last on top), hit-testing walks
/// top-down, and the stack SIZES TO ITS LARGEST NON-POSITIONED child (explicit Width/Height
/// override). Non-positioned children align by <see cref="Align"/>; <see cref="Positioned"/>
/// children anchor to the stack's edges with signed offsets.
/// </summary>
public sealed class Stack : VisualNode
{
    public sealed override string NodeKind => "stack";

    public Stack(Alignment align = Alignment.TopStart) => Align = align;

    public Alignment Align { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }
    public List<VisualNode> Children { get; } = new();

    public void Add(VisualNode child) => Children.Add(child);

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
