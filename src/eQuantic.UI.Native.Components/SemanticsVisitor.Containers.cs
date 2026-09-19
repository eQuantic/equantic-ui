using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Containers and layout — fourteen words the engine has already resolved into geometry by the time
/// a screen reader asks. Thirteen of them are pure layout; the fourteenth, <see cref="Overlay"/>, is
/// the gap.
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
    /// A dialog is a GROUP with a name: the reader says which wall appeared and then walks into it.
    /// Gated on modal AND open exactly as the web realizer gates its `role="dialog"` — a toast layer
    /// is not a dialog and a closed one is not one right now, so the two targets agree on when this
    /// is a stop at all. This declined until <see cref="SemanticRole.Group"/> existed (#187).
    /// </summary>
    public bool Visit(Overlay node, LayoutNode laidOut) =>
        node is { Modal: true, Open: true }
            ? AnnounceGroup(new(SemanticRole.Group, laidOut.Path ?? "", laidOut.Bounds,
                node.Label ?? "", null, false))
            : Descend;

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
