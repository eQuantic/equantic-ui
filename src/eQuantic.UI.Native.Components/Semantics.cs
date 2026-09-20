using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Derives the SEMANTICS TREE from a realized frame: the page layout plus every overlay layout
/// (dialogs are exactly what a screen reader must not miss), walked in paint order. A pure
/// function of the frame — the bridges query it whenever assistive tech asks, and the
/// <c>SemanticsMatchFocusStops</c> parity test keeps this walk and the input walk honest against
/// each other.
/// <para>
/// Like <see cref="FocusStop"/> — and deliberately unlike pointer hit regions — content scrolled
/// out of the viewport is INCLUDED: linear navigation reaches the row below the fold and the view
/// scrolls to it; clipping exists so nobody can click what they cannot see, and a screen reader is
/// the opposite of a pointer.
/// </para>
/// <para>
/// THE FAÇADE OWNS THE RECURSION, <see cref="SemanticsVisitor"/> owns what each word means. The
/// split is what lets the vocabulary hold the dispatch to account: this class knows a frame has a
/// root and overlays, and the visitor has one method per node with no default arm to fall through.
/// </para>
/// </summary>
public static class SemanticsTree
{
    public static IReadOnlyList<SemanticNode> Collect(RealizeResult frame)
    {
        var nodes = new List<SemanticNode>();
        var visitor = new SemanticsVisitor(nodes);
        Walk(frame.Root, visitor);
        for (var i = 0; i < frame.OverlayRoots.Count; i++)
        {
            // The LAYER's own stop comes first and belongs to the layer, not to the placeholder the
            // page flow keeps: announced here it carries the overlay root's bounds and sits
            // immediately before its own descendants, which is the whole of what a group means.
            if (i < frame.OverlayLayers.Count) visitor.AnnounceOverlay(frame.OverlayLayers[i], frame.OverlayRoots[i]);
            Walk(frame.OverlayRoots[i], visitor);
        }
        return nodes;
    }

    /// <summary>
    /// What a reader would say for the CHILDREN of one node — the announcement of a live region,
    /// and nothing else's business. Its own group is skipped on purpose: that node carries the
    /// region's NAME, which does not change when the content does, and a diff that included it
    /// would still be a diff of the content with a constant glued to the front.
    /// <para>
    /// Over ONE subtree rather than the frame. A live region is small by construction — a status
    /// line, a banner, a toast — and walking the whole semantics tree every frame to find out
    /// whether one of them changed would cost the entire page to answer a question about a corner
    /// of it.
    /// </para>
    /// </summary>
    internal static void CollectChildren(LayoutNode node, List<SemanticNode> into)
    {
        var visitor = new SemanticsVisitor(into);
        foreach (var child in node) Walk(child, visitor);
    }

    /// <summary>
    /// Tree order, which is reading order and the order Tab walks. A node that announced CONSUMED
    /// its subtree — its inner text is its name — so the descent is what the visitor declined to
    /// answer for.
    /// </summary>
    private static void Walk(LayoutNode node, SemanticsVisitor visitor)
    {
        if (!node.Source.Accept(visitor, node)) return;
        foreach (var child in node)
            Walk(child, visitor);
    }
}
