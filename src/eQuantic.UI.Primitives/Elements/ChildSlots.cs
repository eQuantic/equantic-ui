namespace eQuantic.UI.Primitives;

/// <summary>
/// What each word of the vocabulary holds as children, slot by slot: the one place the element
/// engine learns a node's shape.
/// <para>
/// A visitor rather than a switch, so a node kind added to <see cref="IVisualNodeVisitor{TState,TResult}"/>
/// cannot be forgotten here: the build fails until it has a door. The slots are the ones the layout
/// measures (a wrapper's child at 0, a code surface's options at 1, a list's members by index), which
/// is what lets the layout read the position of a child from the slot it measures.
/// </para>
/// <para>
/// The state is the frame's window size class, because an <see cref="AdaptiveNode"/> holds ONE child
/// here, the arm the window resolves, exactly as Photon lays it out: the other arms are never built.
/// </para>
/// </summary>
[ServerOnly]
internal sealed class ChildSlots : IVisualNodeVisitor<WindowSizeClass, NodeSlots>
{
    private static readonly ChildSlots Instance = new();

    private ChildSlots()
    {
    }

    internal static NodeSlots Of(VisualNode node, WindowSizeClass sizeClass) => node.Accept(Instance, sizeClass);

    // A component builds its one child; its element never asks this.
    public NodeSlots Visit(UiComponent node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(AdaptiveNode node, WindowSizeClass state) => NodeSlots.One(node.Resolve(state));

    public NodeSlots Visit(Adjustable node, WindowSizeClass state) => NodeSlots.One(node.Child);

    // The panel is not a child of the anchor: the realizer shows it in a layer of its own, made when
    // the anchored node is painted and only while it is open, so that slot is built when it is shown.
    public NodeSlots Visit(Anchored node, WindowSizeClass state) =>
        NodeSlots.WithDeferred(NodeSlots.Two(node.Anchor, null), 1);

    public NodeSlots Visit(Box node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(CameraPreview node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Canvas node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(CodeSurface node, WindowSizeClass state) => NodeSlots.Two(node.Child, node.Options);

    public NodeSlots Visit(Column node, WindowSizeClass state) => NodeSlots.Listed(node.Children);

    public NodeSlots Visit(DragDismiss node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Draggable node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Drawing node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Flexible node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Grid node, WindowSizeClass state) => NodeSlots.Listed(node.Children);

    public NodeSlots Visit(Hoverable node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Icon node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Image node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(InFlow node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(InView node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Link node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(LoopMotion node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Navigable node, WindowSizeClass state) => NodeSlots.Listed(node.Rows);

    // An overlay takes no place in the flow; its child is laid out in the realizer's overlay pass,
    // and not at all while it is closed with motion, so it is built when its layer is shown.
    public NodeSlots Visit(Overlay node, WindowSizeClass state) =>
        NodeSlots.WithDeferred(NodeSlots.One(null), 0);

    public NodeSlots Visit(Pinned node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(LiveRegion node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Positioned node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Presence node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Progress node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Pressable node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Row node, WindowSizeClass state) => NodeSlots.Listed(node.Children);

    public NodeSlots Visit(SafeArea node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(ScrollView node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(SheetSurface node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Shortcut node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Simulated node, WindowSizeClass state) => NodeSlots.One(node.Child);

    public NodeSlots Visit(Spacer node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Spinner node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Stack node, WindowSizeClass state) => NodeSlots.Listed(node.Children);

    public NodeSlots Visit(Text node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(TextEntry node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(Vector node, WindowSizeClass state) => NodeSlots.None;

    public NodeSlots Visit(WebFrame node, WindowSizeClass state) => NodeSlots.None;
}
