namespace eQuantic.UI.Primitives;

/// <summary>
/// What every realizer implements to draw a <see cref="Pattern"/>: one method per kind of the closed
/// set, the shape <see cref="IVisualNodeVisitor{TState, TResult}"/> has for nodes. A kind added to
/// the set adds its method here, so every realizer that does not draw it stops building (#1937).
/// </summary>
/// <typeparam name="TState">What travels into the visit: the box the pattern paints, say.</typeparam>
/// <typeparam name="TResult">What comes back: a layer, or <see cref="Nothing"/>.</typeparam>
public interface IPatternVisitor<in TState, out TResult>
{
    TResult Visit(GridPattern pattern, TState state);

    TResult Visit(DrawingPattern pattern, TState state);

    TResult Visit(ImagePattern pattern, TState state);
}
