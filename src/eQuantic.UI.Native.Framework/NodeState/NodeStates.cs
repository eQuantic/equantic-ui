using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What Photon keeps for a node between frames, read and made on the node's position
/// (<see cref="NodeElement.State"/>): a scroll view's offset, a drag, an entrance, a transition's
/// tracks. It belongs to the position, so it leaves the tree with it, and the next node drawn at its
/// place starts with its own (docs/ELEMENT-TREE-PLAN.md, 3.1).
/// <para>
/// A position keeps ONE state, because the kinds are disjoint by node type: a scroll view keeps a
/// scroll, a draggable a drag, an <see cref="InView"/> its last report, a <see cref="Presence"/> its
/// entrance, a styled box or a weighted child its tracks. A position asked for a kind other than the
/// one it keeps is the engine gone wrong, and it is refused naming both, as the measure refuses a node
/// handed an element that does not hold it: the frame would otherwise be merely wrong.
/// </para>
/// </summary>
public static class NodeStates
{
    /// <summary>The state of kind <typeparamref name="T"/> that <paramref name="at"/> keeps, or null
    /// when it keeps none yet, or is no node's position (a component's, or none at all).</summary>
    public static T? Find<T>(Element? at) where T : class
    {
        if (at is not NodeElement node || node.State is not { } state) return null;
        return state as T ?? throw Mismatch<T>(node, state);
    }

    /// <summary>
    /// The state of kind <typeparamref name="T"/> that <paramref name="at"/> keeps, made the first time
    /// it is asked for. Null for no node's position, a node with none keeps nothing, and null for a
    /// position that has left the tree: a gesture held across the frame its view left in would
    /// otherwise make a state there that nothing releases, and a fling on it would glide, asking for
    /// frames, for a view nobody draws.
    /// </summary>
    public static T? For<T>(Element? at) where T : class, new()
    {
        if (at is not NodeElement { IsActive: true } node) return null;
        if (node.State is not { } state)
        {
            var made = new T();
            node.State = made;
            return made;
        }

        return state as T ?? throw Mismatch<T>(node, state);
    }

    private static InvalidOperationException Mismatch<T>(NodeElement node, object state) => new(
        $"The position of a {node.Configuration.GetType().Name} keeps a {state.GetType().Name} and was "
        + $"asked for a {typeof(T).Name}: a position keeps one kind of state.");
}
