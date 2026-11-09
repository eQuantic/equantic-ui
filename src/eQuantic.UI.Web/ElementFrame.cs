using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// ONE FRAME OF A LIVE ELEMENT TREE, as the web realizer lowers it: the position each node and
/// component of the frame stands at, so a component is lowered from what its position BUILT in the
/// frame rather than built again.
/// <para>
/// Building again is what the lowering of a <see cref="VisualNode"/> does, and for one frame it is
/// the right thing: the server draws a page once. Over a second frame it is wrong in a way nothing
/// reports. A parent's build makes a fresh instance of every child component, the element keeps the
/// FIRST one it mounted and hands it the fresh one's configuration, and the state lives on the kept
/// one. Lowering the fresh instances drew a form inside a page with its fields at their defaults
/// after every press, which the browser never does.
/// </para>
/// <para>
/// A FIELD OF THE LOWERING, not an ambient: the element-tree plan moves the web's channels off the
/// async flow and onto the owner (docs/ELEMENT-TREE-PLAN.md 3.5), and the visitor is one instance
/// per lowering, so the frame is handed to it where it is made.
/// </para>
/// </summary>
internal sealed class ElementFrame
{
    private readonly Dictionary<VisualNode, Element> _positions = new(ReferenceEqualityComparer.Instance);

    internal ElementFrame(Element root) => Index(root);

    /// <summary>
    /// The position of <paramref name="component"/> in this frame: the instance it KEEPS, which is not
    /// the one a parent's latest build holds, and what that instance built, null when it built
    /// nothing. The kept one is also the one a discovery round must ask for its data, so a prefetch
    /// lands on the instance the next frame draws.
    /// </summary>
    /// <exception cref="InvalidOperationException">The frame has no position for the component: the
    /// engine went wrong, and lowering a fresh instance instead would draw its defaults in silence
    /// (Photon refuses a layer without a position for the same reason).</exception>
    internal (UiComponent Kept, VisualNode? Built) At(UiComponent component) =>
        _positions.TryGetValue(component, out var element) && element is ComponentElement position
            ? (position.Component, position.ChildAt(0)?.Configuration)
            : throw new InvalidOperationException(
                $"A {component.GetType().Name} was lowered that this frame did not build: the element "
                + "tree and the tree being lowered disagree.");

    /// <summary>Whether <paramref name="node"/> stands at a position of this frame.</summary>
    internal bool Holds(VisualNode node) => _positions.ContainsKey(node);

    /// <summary>
    /// Shows <paramref name="layer"/> in the deferred slot of <paramref name="holder"/>'s position
    /// (an overlay's child, an anchored panel), so the owner builds it now, with the instances it
    /// keeps there. The realizer decides whether a layer shows, and on the web that is whenever it
    /// lowers the layer: one kept mounted while closed, for its motion, is shown too.
    /// </summary>
    /// <returns>The same layer, now with a position for every component in it.</returns>
    internal VisualNode Show(VisualNode holder, VisualNode layer)
    {
        if (!_positions.TryGetValue(holder, out var element) || element is not NodeElement position)
            throw new InvalidOperationException(
                $"A {holder.GetType().Name}'s layer was lowered that this frame has no position for.");

        Index(position.ShowDeferred(layer));
        return layer;
    }

    /// <summary>
    /// Every position under <paramref name="root"/>, by the node it holds. A node placed at two
    /// positions keeps the first: a stateful instance placed twice builds the same tree at both.
    /// </summary>
    private void Index(Element root)
    {
        var pending = new Stack<Element>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            _positions.TryAdd(element.Configuration, element);

            // A node's children are a LIST, which has no empty slot, or at most TWO fixed slots,
            // either of which may be empty (NodeSlots). So only the first slot can be empty with a
            // child after it, and from the second on an empty one is the end.
            for (var slot = 0; ; slot++)
            {
                var child = element.ChildAt(slot);
                if (child is not null) pending.Push(child);
                else if (slot >= 1) break;
            }
        }
    }
}
