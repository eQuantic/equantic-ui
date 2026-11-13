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
    private readonly int _tree;
    private readonly int _keyDepth;

    /// <param name="root">The tree's root position.</param>
    /// <param name="tree">Which tree of the render this is, in the order the render draws them: what a
    /// server data key starts with (<see cref="KeyOf"/>).</param>
    /// <param name="keyDepth">The depth a position's key is measured from: the depth of the position the
    /// browser builds this tree from (<see cref="MarkupHost.ForPage"/>).</param>
    internal ElementFrame(Element root, int tree = 0, int keyDepth = 0)
    {
        _tree = tree;
        _keyDepth = keyDepth;
        Index(root);
    }

    /// <summary>
    /// The key the server names <paramref name="position"/>'s data by, and the browser hands it back by
    /// (#1477): the tree, the component's type, and the slots from the tree's root down to the position
    /// (<see cref="Element.SlotsBelow"/>). Neither side's build order enters it, nor which arm of an
    /// adaptive node a window resolves. Above the depth the browser builds the tree from stands the page
    /// itself, which the browser builds by itself and names <c>Type#0</c>, and so does this.
    /// </summary>
    internal string KeyOf(ComponentElement position) => KeyAt(position, _tree, _keyDepth);

    /// <summary>The key of <paramref name="position"/> in tree <paramref name="tree"/>, measured from
    /// <paramref name="keyDepth"/>: what <see cref="KeyOf"/> answers, for a host that names a position
    /// while it builds, before any frame is lowered.</summary>
    internal static string KeyAt(ComponentElement position, int tree, int keyDepth) =>
        position.Depth < keyDepth
            ? ComponentIdentity.Key(position.Component.GetType(), 0)
            : $"r{tree}/{ComponentIdentity.Of(position.Component.GetType())}@{position.SlotsBelow(keyDepth)}";

    /// <summary>
    /// The position of <paramref name="component"/> in this frame: the instance it KEEPS, which is not
    /// the one a parent's latest build holds, and what that instance built, null when it built
    /// nothing. The kept one is also the one a discovery round must ask for its data, so a prefetch
    /// lands on the instance the next frame draws.
    /// </summary>
    /// <exception cref="InvalidOperationException">The frame has no position for the component: the
    /// engine went wrong, and lowering a fresh instance instead would draw its defaults in silence
    /// (Photon refuses a layer without a position for the same reason).</exception>
    internal (UiComponent Kept, VisualNode? Built, ComponentElement Position) At(UiComponent component) =>
        _positions.TryGetValue(component, out var element) && element is ComponentElement position
            ? (position.Component, position.ChildAt(0)?.Configuration, position)
            : throw new InvalidOperationException(
                $"A {component.GetType().Name} was lowered that this frame did not build: the element "
                + "tree and the tree being lowered disagree.");

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
            element.VisitChildren(pending.Push);
        }
    }
}
