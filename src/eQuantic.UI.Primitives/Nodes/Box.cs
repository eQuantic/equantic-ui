namespace eQuantic.UI.Primitives;

/// <summary>
/// The atom (spec A1): the engine's rrect surfaced as a component — background fill, uniform INSIDE
/// border, per-corner radius, padding around one child. Every visible surface decomposes into Boxes.
/// A11y: none by default; interactive Boxes are a spec smell — use Pressable/Button.
/// </summary>
public sealed class Box : VisualNode
{
    public override string NodeKind => "box";

    public Box(BoxStyle style = default, VisualNode? child = null)
    {
        Style = style;
        Child = child;
    }

    public BoxStyle Style { get; init; }
    public VisualNode? Child { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
