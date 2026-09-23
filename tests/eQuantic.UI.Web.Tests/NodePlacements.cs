using System.Reflection;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every place a node is reachable from a root, WITHOUT building a component — the instrument behind
/// "a node belongs to ONE tree". It matters most under an <see cref="AdaptiveNode"/>: the web mounts
/// every arm, so an instance reachable from two arms is ONE component mounted twice, one state
/// driving two copies.
/// <para>
/// A component's own node-typed properties — a Drawer's content, a Menu's trigger — are exactly how
/// one instance gets handed to two places, so they are walked like a container's children.
/// Reflection rather than a list of kinds, because a list would silently stop at the first kind a
/// tree starts using and nobody added.
/// </para>
/// </summary>
internal static class NodePlacements
{
    /// <summary>One position: the node, the property path that reaches it, and whether an
    /// <see cref="AdaptiveNode"/> arm lies between it and the root.</summary>
    internal readonly record struct Placement(VisualNode Node, string Path, bool InArm);

    internal static IEnumerable<Placement> Of(VisualNode root)
    {
        var pending = new Stack<Placement>();
        pending.Push(new Placement(root, root.GetType().Name, InArm: false));
        while (pending.Count > 0)
        {
            var placement = pending.Pop();
            yield return placement;

            var node = placement.Node;
            var inArm = placement.InArm || node is AdaptiveNode;
            foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                var path = $"{placement.Path}.{property.Name}";
                switch (property.GetValue(node))
                {
                    case VisualNode child:
                        pending.Push(new Placement(child, $"{path}:{child.GetType().Name}", inArm));
                        break;
                    case IEnumerable<VisualNode> children:
                        var index = 0;
                        foreach (var child in children)
                            pending.Push(new Placement(child,
                                $"{path}[{index++}]:{child.GetType().Name}", inArm));
                        break;
                }
            }
        }
    }

    /// <summary>Every instance reachable by more than one path, as its paths joined — empty when each
    /// node belongs to one place.</summary>
    internal static IReadOnlyList<string> Shared(VisualNode root) =>
        Of(root)
            .GroupBy(placement => (object)placement.Node, ReferenceEqualityComparer.Instance)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" and ", group.Select(placement => placement.Path)))
            .ToList();
}
