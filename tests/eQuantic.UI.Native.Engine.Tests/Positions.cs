using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a test asks about where a node sits, now that the host addresses positions rather than paths
/// (#976): which position an id names in a frame, whether one position lies under another, and
/// whether a position sits in a layer the realizer shows over the page, which a path once said by
/// starting with <c>ov</c>.
/// </summary>
internal static class Positions
{
    /// <summary>The position the frame drew with <paramref name="id"/>, in the page or in a layer, or
    /// null when it drew none.</summary>
    public static Element? Of(RealizeResult frame, int id)
    {
        if (Find(frame.Root, id) is { } page) return page;
        foreach (var layer in frame.OverlayRoots)
            if (Find(layer, id) is { } found) return found;
        return null;
    }

    private static Element? Find(LayoutNode node, int id)
    {
        if (node.Element is { } at && at.Id == id) return at;
        foreach (var child in node)
            if (Find(child, id) is { } found) return found;
        return null;
    }

    /// <summary>Whether <paramref name="at"/> lies below <paramref name="above"/> in the tree.</summary>
    public static bool IsUnder(Element at, Element above)
    {
        for (var parent = at.Parent; parent is not null; parent = parent.Parent)
            if (ReferenceEquals(parent, above)) return true;
        return false;
    }

    /// <summary>Whether <paramref name="at"/> is held by a layer: an overlay's child, an anchored
    /// panel's layer, or anything below one.</summary>
    public static bool InALayer(Element at)
    {
        var child = at;
        for (var parent = at.Parent; parent is not null; child = parent, parent = parent.Parent)
            if (parent is NodeElement node && node.HoldsDeferred(child)) return true;
        return false;
    }

    /// <summary>Whether <paramref name="at"/>, or a position above it, says a key: a row of a keyed
    /// list, whose position its key keeps wherever the row moves.</summary>
    public static bool IsKeyed(Element at)
    {
        for (Element? position = at; position is not null; position = position.Parent)
            if (position.Configuration.Key is { Length: > 0 }) return true;
        return false;
    }

    /// <summary>Whether the node the frame drew with <paramref name="id"/> is held by a layer.</summary>
    public static bool InALayer(RealizeResult frame, int id) => Of(frame, id) is { } at && InALayer(at);
}
