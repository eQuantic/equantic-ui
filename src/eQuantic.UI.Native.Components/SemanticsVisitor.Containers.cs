using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Containers and layout — fourteen words the engine has already resolved into geometry by the time
/// a screen reader asks. All fourteen descend here, and that is no longer a gap: a modal
/// <see cref="Overlay"/> DOES announce a group now (#187), at its overlay ROOT rather than at the
/// placeholder this file sees — <see cref="SemanticsVisitor.AnnounceOverlay"/>, called by
/// <c>SemanticsTree.Collect</c>. The arm below carries the reason, because "it descends" and "it has
/// nothing to say" stopped being the same sentence.
/// </summary>
internal sealed partial class SemanticsVisitor
{
    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(AdaptiveNode node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Anchored node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Box node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Column node, LayoutNode laidOut) => PureLayout;

    /// <inheritdoc cref="PureLayout"/>
    public bool Visit(Flexible node, LayoutNode laidOut) => PureLayout;

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
    /// The group is announced at the overlay ROOT instead (<c>SemanticsTree.Collect</c> →
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
}
