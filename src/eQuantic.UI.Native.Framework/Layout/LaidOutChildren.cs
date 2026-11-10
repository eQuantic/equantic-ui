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
/// A component is what it builds, so one that builds a node only its parent can place (a Spacer, a
/// Flexible, an AdaptiveNode) is that node here, read from the element its position already built, so
/// the instance that built is the one that stays: asked of the component, the same questions had no
/// answer, and a Spacer a component built took no space in its column (#736) and an arm a component
/// built was not placed by its line or its grid (#738).
/// </para>
/// <para>
/// The arm is measured with the element the AdaptiveNode's element holds at the arm's slot, as the
/// AdaptiveNode's own measurement gives it, so what the arm keeps between frames stays where it was.
/// A struct over the container's own list: every container is laid out every frame, and this
/// allocates nothing.
/// </para>
/// </summary>
internal readonly struct LaidOutChildren
{
    private readonly IReadOnlyList<VisualNode> _nodes;
    private readonly LayoutContext _ctx;
    private readonly Element? _parent;

    internal LaidOutChildren(IReadOnlyList<VisualNode> nodes, LayoutContext ctx, Element? parent)
    {
        _nodes = nodes;
        _ctx = ctx;
        _parent = parent;
    }

    public int Count => _nodes.Count;

    /// <summary>The child at <paramref name="index"/>, as the container lays it out.</summary>
    public VisualNode this[int index] => Resolve(index).Node;

    /// <summary>The element the child at <paramref name="index"/> is measured with: the one the build
    /// matched to it, by its key when it says one.</summary>
    public Element? At(int index) => Resolve(index).At;

    private (VisualNode Node, Element? At) Resolve(int index)
    {
        var node = _nodes[index];
        var at = _parent?.ChildAt(index);
        while (true)
        {
            if (node is AdaptiveNode adaptive)
            {
                node = _ctx.ArmOf(adaptive);
                at = at?.ChildAt(0);
            }
            else if (node is UiComponent && at?.ChildAt(0) is { } built && BuildsAPlaceInItsParent(built))
            {
                node = built.Configuration;
                at = built;
            }
            else
            {
                return (node, at);
            }
        }
    }

    /// <summary>Whether what a component built, through any components it builds in turn, is a node
    /// only its parent can place. Any other child keeps the component as the item, which measures
    /// what it built in place.</summary>
    private static bool BuildsAPlaceInItsParent(Element built)
    {
        Element? element = built;
        while (element?.Configuration is UiComponent) element = element.ChildAt(0);
        return element?.Configuration is Spacer or Flexible or AdaptiveNode;
    }
}
