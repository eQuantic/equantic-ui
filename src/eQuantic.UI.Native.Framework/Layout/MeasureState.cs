using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What VARIES per node on the way down a measurement: the box the parent is offering, and where
/// this node sits. Everything else a measurement needs is fixed for the pass and lives on the
/// visitor — <see cref="IVisualNodeVisitor{TState,TResult}"/> carries one state, so the split is
/// forced and happens to be the right one anyway.
/// <para>
/// A readonly record STRUCT, because it is built once per node of every frame: the layout half of a
/// frame must not allocate, which <c>PerfHarnessTests</c> pins at 74 KB per frame with the node pool
/// on. A class here would be one heap object per node of every tree, every frame.
/// </para>
/// </summary>
/// <param name="Constraints">The box the parent offers, and who is stretching.</param>
/// <param name="At">Where this node is: the path a gesture uses to survive a frame, and the element
/// the build phase made for it.</param>
internal readonly record struct MeasureState(LayoutConstraints Constraints, Position At)
{
    /// <summary>The path alone, for the stores that still key on it.</summary>
    public string Path => At.Path;
}
