namespace eQuantic.UI.Primitives;

/// <summary>
/// What every realizer implements to draw a <see cref="Mask"/>: one method per kind of the closed
/// set, the shape <see cref="IPatternVisitor{TState, TResult}"/> has for patterns. A kind added to
/// the set adds its method here, so every realizer that does not draw it stops building (#1938).
/// </summary>
/// <typeparam name="TState">What travels into the visit: the box the mask fades, say.</typeparam>
/// <typeparam name="TResult">What comes back: a CSS value, or a layer.</typeparam>
public interface IMaskVisitor<in TState, out TResult>
{
    TResult Visit(LinearMask mask, TState state);

    TResult Visit(RadialMask mask, TState state);
}
