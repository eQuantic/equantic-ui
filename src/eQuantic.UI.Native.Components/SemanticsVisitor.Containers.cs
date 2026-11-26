using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Containers and layout — fifteen words the engine has already resolved into geometry by the time
/// a screen reader asks. All fifteen descend here, and that is no longer a gap: a modal
/// <see cref="Overlay"/> DOES announce itself now (#187), as a dialog since #501, at its overlay
/// ROOT rather than at the placeholder this file sees — <see cref="SemanticsVisitor.AnnounceOverlay"/>,
/// called by <c>SemanticsTree.Collect</c>. The arm below carries the reason, because "it descends"
/// and "it has nothing to say" stopped being the same sentence.
/// </summary>
internal sealed partial class SemanticsVisitor
{
    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(AdaptiveNode node, LayoutNode laidOut) => PureLayout;

    /// <summary>
    /// An anchored node is pure layout, and its PANEL is read IN PLACE, right after the anchor, when its
    /// layer is shown (#1868). The web lowers the panel inside the anchor's markup, and Flutter attaches an
    /// <c>OverlayPortal</c>'s child to the portal's semantics rather than to the overlay's; Photon lays the
    /// panel out in a layer of its own, which this walk read after the page, so whatever followed the anchor
    /// on the page was read before the panel. The layer holds the scrim's dismiss button first, then the
    /// panel, and a DIALOG panel's stop is the panel, after that button, as the web's <c>role="dialog"</c>
    /// is the panel and its scrim stands outside it: it was the whole layer, so the button was read inside
    /// the dialog.
    /// </summary>
    public bool Visit(Anchored node, LayoutNode laidOut)
    {
        if (laidOut.Element is not { } at || !_panels.TryGetValue(at, out var layer)) return PureLayout;
        foreach (var anchor in laidOut) SemanticsTree.Walk(anchor, this);
        _readInPlace.Add(layer);
        foreach (var part in layer)
        {
            if (node.PanelRole == AnchorPanelRole.Dialog && part.Source is Positioned)
                AnnounceGroup(new(SemanticRole.Dialog, IdOf(part), part.Bounds, "", null, false));
            SemanticsTree.Walk(part, this);
        }
        return Consumed;
    }

    /// <summary>
    /// Pure layout, which says nothing of its own; its children are walked HERE rather than by the
    /// tree's descent, because the look the box states (<see cref="BoxStyle.Content"/>, #1577) is in
    /// effect for them and only for them, as on the paint walk: a linked run's place in a paragraph
    /// follows the alignment the paragraph takes from it.
    /// </summary>
    public bool Visit(Box node, LayoutNode laidOut)
    {
        var around = _content;
        _content = ContentStyle.Merge(around, node.Style.Content);
        try
        {
            foreach (var child in laidOut) SemanticsTree.Walk(child, this);
        }
        finally
        {
            _content = around;
        }
        return Consumed;
    }

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Column node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Flexible node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(LayoutBuilder node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Grid node, LayoutNode laidOut) => PureLayout;

    /// <summary>
    /// NOT HERE, and the reason is where an overlay actually lives. This node is the PAGE-FLOW
    /// PLACEHOLDER: <c>MeasureVisitor</c> gives it the node and none of its subtree, and the
    /// realizer lays the layer out separately against the viewport as an overlay root
    /// (<c>ov&lt;i&gt;</c>). So announcing the dialog here gives it the placeholder's bounds —
    /// measured at 400x0 in the middle of a column — and puts the stop between the page content
    /// before and after it, while its own elements arrive later as unrelated siblings.
    /// <para>
    /// The dialog is announced at the overlay ROOT instead (<c>SemanticsTree.Collect</c> →
    /// <see cref="SemanticsVisitor.AnnounceOverlay"/>), where it has the LAYER's bounds and sits
    /// immediately before its descendants. That is also what the web does: <c>role="dialog"</c> and
    /// <c>aria-modal</c> go on the `eq-overlay` element, which is `position: fixed; inset: 0` — the
    /// whole viewport, because for assistive tech a modal layer IS the screen.
    /// </para>
    /// <para>
    /// Found in review after the first version announced here and a probe with the Overlay at the
    /// ROOT of the tree passed: there the placeholder happens to be the whole viewport and there is
    /// no page content to be interleaved with, so the case the finding describes was invisible to it.
    /// </para>
    /// </summary>
    public bool Visit(Overlay node, LayoutNode laidOut) => Descend;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Pinned node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Positioned node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Row node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(SafeArea node, LayoutNode laidOut) => PureLayout;

    /// <summary>
    /// Pure layout, and the one word here where that costs something a reader should know about: a
    /// viewport is invisible to the bridges, because <see cref="SemanticRole"/> has no scrollable
    /// region. What the walk DOES get right is the half that matters most — content scrolled out of
    /// the viewport stays in the tree, so linear navigation reaches the row below the fold.
    /// </summary>
    public bool Visit(ScrollView node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Spacer node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Stack node, LayoutNode laidOut) => PureLayout;

    /// <summary>A group's cell is pure layout (<see cref="PureLayout"/>); a table's or a grid's is announced by
    /// its viewport, which never descends to here (<c>Visit(TableViewport)</c>). A MARK is drawn over the
    /// cells and is never pressed: nothing under it is an object to reach, by a reader or by Tab, whose route
    /// the emit takes from it too (<c>InputSink.WithoutFocusStops</c>), so the walk stops there.</summary>
    public bool Visit(TableViewCell node, LayoutNode laidOut) => node.Mark ? Consumed : PureLayout;
}
