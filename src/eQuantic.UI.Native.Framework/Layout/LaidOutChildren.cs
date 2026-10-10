using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// A container's children as the container LAYS THEM OUT.
/// <para>
/// On Photon an AdaptiveNode is the arm its window resolves it to (spec S6) — the other arms never
/// measure and never paint — so whatever a line or a grid asks of a direct child, it asks of that
/// arm: whether it is a Spacer or a Flexible, how it aligns itself, how many tracks it spans. Asked
/// of the AdaptiveNode, every answer was "none of those", and a Spacer arm took no space in its
/// column (#670). A Stack asks its one question, whether a child is Positioned, of the node the
/// child measured to, which is already the arm (#671). The web's twin is every container placing
/// each arm by its own rule, behind the arm's gate.
/// </para>
/// <para>
/// The arm is measured at the path it always had — its node's, then the arm's slot, as the
/// AdaptiveNode's own measurement gives it — so nothing remembered by a path moves, a keyed node's
/// key included. A struct over the container's own list: every container is laid out every frame,
/// and this allocates nothing.
/// </para>
/// </summary>
internal readonly struct LaidOutChildren
{
    private readonly IReadOnlyList<VisualNode> _nodes;
    private readonly LayoutContext _ctx;

    internal LaidOutChildren(IReadOnlyList<VisualNode> nodes, LayoutContext ctx)
    {
        _nodes = nodes;
        _ctx = ctx;
    }

    public int Count => _nodes.Count;

    /// <summary>The child at <paramref name="index"/>, as the container lays it out.</summary>
    public VisualNode this[int index]
    {
        get
        {
            var node = _nodes[index];
            while (node is AdaptiveNode adaptive) node = _ctx.ArmOf(adaptive);
            return node;
        }
    }

    /// <summary>Where the child at <paramref name="index"/> is measured, below <paramref name="parent"/>.</summary>
    public string PathOf(string parent, int index)
    {
        var node = _nodes[index];
        var path = _ctx.ChildPath(parent, index, node);
        while (node is AdaptiveNode adaptive)
        {
            node = _ctx.ArmOf(adaptive);
            path = _ctx.ChildPath(path, 0);
        }
        return path;
    }
}
