namespace eQuantic.UI.Primitives;

/// <summary>
/// A child built for the room its parent gives: the <see cref="Builder"/> is handed the
/// <see cref="BoxConstraints"/> this node's box has, and what it answers is the child.
/// <para>
/// The node takes its room as a <see cref="Box"/> whose width and height <see cref="SizeValue.Fill"/>
/// does. On an axis its parent decides, the builder is told that size as the maximum; on an axis whose
/// size is left to what the box holds (a column sizing its height from its children, a scroller's
/// axis), it is told an unbounded maximum. The minimums are zero: the builder is told how much room
/// there is, and how its child takes it is the child's own <c>Width</c> and <c>Height</c>.
/// </para>
/// <para>
/// Flutter's <c>LayoutBuilder</c>, and Photon runs it where Flutter does, in the layout pass. The web
/// cannot build inside the browser's layout, so it builds right after it, against the same room: the
/// server writes the box empty, the browser measures it and builds the child, and builds it again when
/// the room changes. Nothing under a builder is drawn by the server, so a decision that has to render
/// there belongs to the window's size class, which <see cref="AdaptiveNode"/> serves.
/// </para>
/// <para>
/// The builder runs again when the room changes or the component that holds this node builds it again,
/// and not when the same node is laid out again in the same room.
/// </para>
/// </summary>
public sealed class LayoutBuilder : VisualNode
{
    /// <param name="builder">What the child is, for the room this box has.</param>
    public LayoutBuilder(Func<BoxConstraints, VisualNode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Builder = builder;
    }

    public sealed override string NodeKind => "layoutBuilder";

    /// <summary>Answers the child for the room this box has.</summary>
    public Func<BoxConstraints, VisualNode> Builder { get; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
