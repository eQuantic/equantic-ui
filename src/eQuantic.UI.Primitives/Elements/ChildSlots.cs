namespace eQuantic.UI.Primitives;

/// <summary>
/// What each word of the vocabulary holds as children, slot by slot: the one place the element
/// engine learns a node's shape.
/// <para>
/// One switch over <see cref="VisualNode.NodeKind"/>, the wire discriminator the browser dispatches by
/// too. The engine runs on both sides (docs/ELEMENT-TREE-PLAN.md 3.9), and the visitor this replaces,
/// one <c>Visit</c> overload per kind, could not cross: a JavaScript class has one member per name
/// (EQ1007). A string switch is a hash jump in .NET and a <c>switch</c> in JavaScript, where a switch
/// over the types would test them one by one for every node every build reads.
/// </para>
/// <para>
/// What the visitor gave, that a kind added to the vocabulary cannot be forgotten here, is held by a
/// test that EXECUTES: it mounts one instance of every concrete node through an owner, and the default
/// throws naming the kind, so a forgotten one fails it by name rather than answering "no children".
/// </para>
/// <para>
/// The slots are the ones the layout measures (a wrapper's child at 0, a code surface's options at 1, a
/// list's members by index), which is what lets the layout read the position of a child from the slot
/// it measures. The size class is the frame's, because an <see cref="AdaptiveNode"/> holds ONE child
/// here, the arm the window resolves, exactly as Photon lays it out: the other arms are never built.
/// </para>
/// </summary>
internal static class ChildSlots
{
    internal static NodeSlots Of(VisualNode node, WindowSizeClass sizeClass) => node.NodeKind switch
    {
        // A component builds its one child; its element never asks this.
        "component" => NodeSlots.None,
        "adaptive" => NodeSlots.One(((AdaptiveNode)node).Resolve(sizeClass)),
        "adjustable" => NodeSlots.One(((Adjustable)node).Child),
        // The panel is not a child of the anchor: the realizer shows it in a layer of its own, made when
        // the anchored node is painted and only while it is open, so that slot is built when it is shown.
        "anchored" => NodeSlots.WithDeferred(NodeSlots.Two(((Anchored)node).Anchor, null), 1),
        "box" => NodeSlots.One(((Box)node).Child),
        "cameraPreview" => NodeSlots.None,
        "canvas" => NodeSlots.None,
        "codeSurface" => CodeSurfaceSlots((CodeSurface)node),
        "column" => NodeSlots.Listed(((Column)node).Children),
        "dragDismiss" => NodeSlots.One(((DragDismiss)node).Child),
        "draggable" => NodeSlots.One(((Draggable)node).Child),
        "drawing" => NodeSlots.None,
        "dropTarget" => NodeSlots.One(((DropTarget)node).Child),
        "flexible" => NodeSlots.One(((Flexible)node).Child),
        "grid" => NodeSlots.Listed(((Grid)node).Children),
        "hoverable" => NodeSlots.One(((Hoverable)node).Child),
        "icon" => NodeSlots.None,
        "image" => NodeSlots.None,
        "inFlow" => NodeSlots.One(((InFlow)node).Child),
        "inView" => NodeSlots.One(((InView)node).Child),
        "link" => NodeSlots.One(((Link)node).Child),
        "loopMotion" => NodeSlots.One(((LoopMotion)node).Child),
        "navigable" => NodeSlots.Listed(((Navigable)node).Rows),
        // An overlay takes no place in the flow; its child is laid out in the realizer's overlay pass,
        // and not at all while it is closed with motion, so it is built when its layer is shown.
        "overlay" => NodeSlots.WithDeferred(NodeSlots.One(null), 0),
        "pinned" => NodeSlots.One(((Pinned)node).Child),
        "liveRegion" => NodeSlots.One(((LiveRegion)node).Child),
        "positioned" => NodeSlots.One(((Positioned)node).Child),
        "presence" => NodeSlots.One(((Presence)node).Child),
        "progress" => NodeSlots.One(((Progress)node).Child),
        "pressable" => NodeSlots.One(((Pressable)node).Child),
        "row" => NodeSlots.Listed(((Row)node).Children),
        "safeArea" => NodeSlots.One(((SafeArea)node).Child),
        "scrollView" => NodeSlots.One(((ScrollView)node).Child),
        "sheetSurface" => NodeSlots.One(((SheetSurface)node).Child),
        "shortcut" => NodeSlots.One(((Shortcut)node).Child),
        "simulated" => NodeSlots.One(((Simulated)node).Child),
        "spacer" => NodeSlots.None,
        "spinner" => NodeSlots.None,
        "stack" => NodeSlots.Listed(((Stack)node).Children),
        "tableViewCell" => NodeSlots.One(((TableViewCell)node).Child),
        "tableViewport" => NodeSlots.Listed(((TableViewport)node).Cells),
        "text" => NodeSlots.None,
        "textEntry" => NodeSlots.None,
        "vector" => NodeSlots.None,
        "webFrame" => NodeSlots.None,
        _ => throw new InvalidOperationException(
            $"The element engine knows no child slots for a '{node.NodeKind}' node: ChildSlots needs an arm for its kind."),
    };

    // The code, what it offers at its caret when it offers anything, then each note it explains with:
    // the order the layout measures them in.
    private static NodeSlots CodeSurfaceSlots(CodeSurface node) => node.Notes.Count == 0
        ? NodeSlots.Two(node.Child, node.Options)
        : node.Options is { } options
            ? NodeSlots.Listed([node.Child, options, .. node.Notes])
            : NodeSlots.Listed([node.Child, .. node.Notes]);
}
