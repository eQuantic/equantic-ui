using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Where a node is measured: its PATH, which focus, hover and the hit regions still key on, and its
/// ELEMENT, the position the build phase made for it, where what the node keeps between frames lives
/// (<see cref="NodeStates"/>).
/// <para>
/// One value rather than two parameters, because they are the same fact spelled twice and must
/// not drift: every site that formed a child's path forms the child's position, from the same slot
/// (<see cref="LayoutContext.Child(Position, int)"/>). When the host addresses elements too (#976,
/// docs/ELEMENT-TREE-PLAN.md) the path goes and this is the element alone.
/// </para>
/// <para>
/// Nullable because a slot can hold nothing (a box with no child, a layer not shown), and a layout
/// reached through its own tree never measures one; the measure refuses a component without one.
/// </para>
/// </summary>
/// <param name="Path">The path, as focus, hover and the hit regions key it.</param>
/// <param name="Element">The position the build phase made for the node, or null for one it did not.</param>
internal readonly record struct Position(string Path, Element? Element);
