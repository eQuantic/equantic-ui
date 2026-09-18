using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// What DOM the server writes for each word of the vocabulary — the web realizer's dispatch, moved
/// off a switch with a default arm and onto the visitor the compiler checks.
/// </summary>
internal sealed partial class WebLoweringVisitor(ComponentContext context)
    : IVisualNodeVisitor<bool?, HtmlElement?>
{
    /// <summary>The theme and type scale this lowering runs against — fixed for the pass.</summary>
    private readonly ComponentContext _context = context;


    /// <summary>
    /// The states this subtree is being DRAWN as, if any.
    /// <para>
    /// It was an <c>AsyncLocal</c> for as long as the realizer was a static class: SSR renders
    /// concurrent requests, and a static field would have leaked one page's preview into another's.
    /// The visitor is built once per lowering, so the instance IS that scope and a plain field is
    /// the whole of it — the mechanism the language already had, where the ceremony used to be.
    /// </para>
    /// </summary>
    private SimulatedState _simulated;

    /// <summary>
    /// The single dispatch every node passes through — and therefore the one place the design-mode
    /// origin is attached, once, rather than in each of the branches below.
    /// <para>
    /// <see cref="VisualNode.Origin"/> is set only by a design-mode compilation, so in every shipped
    /// build this is one null check per node and the DOM is byte-identical. It has to stay the exact
    /// twin of the same step in <c>lowering.ts</c>: the two producers render the same tree on either
    /// side of hydration, and one attribute of disagreement is a diverged subtree.
    /// </para>
    /// </summary>
    internal HtmlElement? Lower(VisualNode node, bool? horizontalAxis)
    {
        var lowered = node.Accept(this, horizontalAxis);

        // A node's Key is its identity among siblings — the same one property Photon reads into a
        // keyed path segment — and here it becomes the reconciler's key, so a keyed row that moved
        // from the third position to the first is a MOVED row, not a rewritten one. Written in the
        // funnel because it belongs to every node; SSR emits no attribute for it, so the DOM is
        // unchanged and the twin in lowering.ts writes exactly the same field.
        if (lowered is not null && node.Key is { Length: > 0 } key) lowered.Key = key;

        // The one funnel every node passes through, which is where a property that belongs to ALL
        // of them has to be written — an in-page link may point at any node, not at a chosen few.
        if (lowered is not null && node.Bookmark is { Length: > 0 } bookmark)
        {
            lowered.Id = bookmark;
            // ROOM TO LAND. A browser scrolls a target to the very top, so under a pinned header
            // the anchor arrives hidden behind it — the link "works" and still looks broken, which
            // is the more expensive bug of the two. The offset is a variable the pinned itself
            // publishes (see the runtime's pinned measurement), never a number the author repeats:
            // one page here has a 60dp nav and another a 56dp topbar, and neither states either.
            lowered.Style ??= new HtmlStyle();
            lowered.Style.ScrollMarginTop = "var(--eq-anchor-offset, 0px)";
        }

        if (lowered is not null && node.Origin is { Length: > 0 } origin)
        {
            // DataAttributes and not RawAttributes: RawAttributes lives on RealizedElement (it exists
            // so SVG can emit viewBox/d verbatim), and not every lowered node is one. DataAttributes
            // is on HtmlElement itself and prefixes `data-`, so this reaches every node there is.
            var data = lowered.DataAttributes ??= new Dictionary<string, string>();
            data["eq-origin"] = origin;
            if (node.OriginLabel is { Length: > 0 } label) data["eq-component"] = label;
        }
        return lowered;
    }

    // ---- containers and layout ----------------------------------------------------------------

    public HtmlElement? Visit(Box box, bool? horizontalAxis) => LowerBox(box);

    // Row and Column were ONE arm (`FlexNode flex =>`), because a flex container is a flex
    // container whichever way it runs. They are two methods and one helper now: the vocabulary has
    // two words, so the visitor has two doors, and what they mean is still written once.
    public HtmlElement? Visit(Row row, bool? horizontalAxis) => LowerFlex(row);

    public HtmlElement? Visit(Column column, bool? horizontalAxis) => LowerFlex(column);

    public HtmlElement? Visit(Stack stack, bool? horizontalAxis) => LowerStack(stack);

    public HtmlElement? Visit(Grid grid, bool? horizontalAxis) => LowerGrid(grid);

    public HtmlElement? Visit(AdaptiveNode adaptive, bool? horizontalAxis) => LowerAdaptive(adaptive);

    public HtmlElement? Visit(Pinned pinned, bool? horizontalAxis) => LowerPinned(pinned);

    public HtmlElement? Visit(SafeArea safeArea, bool? horizontalAxis) => LowerSafeArea(safeArea);

    public HtmlElement? Visit(Anchored anchored, bool? horizontalAxis) => LowerAnchored(anchored);

    public HtmlElement? Visit(ScrollView scroll, bool? horizontalAxis) => LowerScrollView(scroll);

    public HtmlElement? Visit(Overlay overlay, bool? horizontalAxis) => LowerOverlay(overlay);

    /// <summary>A Positioned outside a Stack has no anchor frame — degrade to its child (parity
    /// with native).</summary>
    public HtmlElement? Visit(Positioned positioned, bool? horizontalAxis) =>
        Lower(positioned.Child, horizontalAxis);

    public HtmlElement? Visit(Flexible flexible, bool? horizontalAxis) =>
        LowerFlexible(flexible, horizontalAxis);

    public HtmlElement? Visit(Spacer spacer, bool? horizontalAxis) => LowerSpacer(spacer, horizontalAxis);

    // ---- text and the editable surfaces ---------------------------------------------------------

    public HtmlElement? Visit(Text text, bool? horizontalAxis) => LowerText(text);

    public HtmlElement? Visit(TextEntry entry, bool? horizontalAxis) => LowerTextEntry(entry);

    public HtmlElement? Visit(SheetSurface sheet, bool? horizontalAxis) =>
        LowerSheetSurface(sheet, horizontalAxis);

    /// <summary>
    /// THE ONE WORD THIS REALIZER STILL DOES NOT WRITE, and the reason is not "nobody noticed" — it
    /// was that once, when `SheetSurface` sat here beside it and both lowered to an empty
    /// <c>&lt;span&gt;</c>. The client appends a caret to every code surface and the server has no
    /// business rendering a caret; emitting only the child would hand hydration a tree one element
    /// short, which the reconciler records as a failed adoption. Settling the shape needs a running
    /// page rather than a guess. <c>SurfaceSsrTests</c> holds the half that is done.
    /// <para>
    /// It returns null exactly as the old default arm did. What is new is not that null is rare —
    /// <see cref="Visit(Spacer, bool?)"/> returns it outside a flex axis, and every wrapper
    /// propagates a null child — but that this is the only node with NO lowering at all, the only
    /// one that answers null for every instance, and that the answer is written where the node is
    /// instead of in an exemption list. (Review caught the overstatement in the first draft of this
    /// comment, which claimed it was the only node that could return null.)
    /// </para>
    /// </summary>
    public HtmlElement? Visit(CodeSurface code, bool? horizontalAxis) => null;

    // ---- graphics --------------------------------------------------------------------------------

    public HtmlElement? Visit(Icon icon, bool? horizontalAxis) => LowerIcon(icon);

    public HtmlElement? Visit(Vector vector, bool? horizontalAxis) => LowerVector(vector);

    public HtmlElement? Visit(Drawing drawing, bool? horizontalAxis) => LowerDrawing(drawing);

    public HtmlElement? Visit(Canvas canvas, bool? horizontalAxis) => LowerCanvas(canvas);

    public HtmlElement? Visit(Spinner spinner, bool? horizontalAxis) => LowerSpinner(spinner);

    public HtmlElement? Visit(Primitives.Image image, bool? horizontalAxis) => LowerImage(image);

    public HtmlElement? Visit(CameraPreview camera, bool? horizontalAxis) => LowerCameraPreview(camera);

    public HtmlElement? Visit(WebFrame frame, bool? horizontalAxis) => LowerWebFrame(frame);

    // ---- interaction and motion -------------------------------------------------------------------

    public HtmlElement? Visit(Pressable pressable, bool? horizontalAxis) => LowerPressable(pressable);

    public HtmlElement? Visit(Link link, bool? horizontalAxis) => LowerLink(link);

    public HtmlElement? Visit(Hoverable hoverable, bool? horizontalAxis) => LowerHoverable(hoverable);

    public HtmlElement? Visit(Adjustable adjustable, bool? horizontalAxis) => LowerAdjustable(adjustable);

    public HtmlElement? Visit(Progress progress, bool? horizontalAxis) => LowerProgress(progress);

    public HtmlElement? Visit(Navigable navigable, bool? horizontalAxis) => LowerNavigable(navigable);

    public HtmlElement? Visit(Shortcut shortcut, bool? horizontalAxis) =>
        LowerShortcut(shortcut, horizontalAxis);

    public HtmlElement? Visit(Draggable draggable, bool? horizontalAxis) =>
        LowerDraggable(draggable, horizontalAxis);

    public HtmlElement? Visit(DragDismiss drag, bool? horizontalAxis) =>
        LowerDragDismiss(drag, horizontalAxis);

    public HtmlElement? Visit(Presence presence, bool? horizontalAxis) =>
        LowerPresence(presence, horizontalAxis);

    public HtmlElement? Visit(LoopMotion motion, bool? horizontalAxis) => LowerLoopMotion(motion);

    public HtmlElement? Visit(InView inView, bool? horizontalAxis) => LowerInView(inView, horizontalAxis);

    public HtmlElement? Visit(InFlow inFlow, bool? horizontalAxis) => LowerInFlow(inFlow, horizontalAxis);

    public HtmlElement? Visit(Simulated simulated, bool? horizontalAxis) =>
        LowerSimulated(simulated, horizontalAxis);

    // ---- the seam ----------------------------------------------------------------------------------

    /// <summary>
    /// Through the BOUNDARY, never `Build`: one component's failure must cost that component's
    /// subtree and nothing else — on the server it used to cost the whole request (a 500 for a
    /// card). `ExpandContained` rather than `BuildContained` because the expansion RECURSES here,
    /// and a component that builds itself would otherwise walk back in forever.
    /// </summary>
    public HtmlElement? Visit(UiComponent component, bool? horizontalAxis) =>
        component.ExpandContained(_context, (Visitor: this, Axis: horizontalAxis),
            static (built, state) => state.Visitor.Lower(built, state.Axis));

    // ---- what more than one family reaches ---------------------------------------------

    /// <summary>Whether a node requests Fill on each axis — wrappers (Pressable's button) must
    /// stretch for the 100% chain to reach it (the native MeasureWrapper sizes to the child).</summary>
    private (bool Width, bool Height) Fills(VisualNode node) => node switch
    {
        Box box => (box.Style.Width.Kind == SizeKind.Fill, box.Style.Height.Kind == SizeKind.Fill),
        FlexNode flex => (flex.Width.Kind == SizeKind.Fill, flex.Height.Kind == SizeKind.Fill),
        Stack stack => (stack.Width.Kind == SizeKind.Fill, stack.Height.Kind == SizeKind.Fill),
        Pressable pressable => Fills(pressable.Child),
        Hoverable hoverable => Fills(hoverable.Child),
        Adjustable adjustable => Fills(adjustable.Child),
        Flexible flexible => Fills(flexible.Child),
        LoopMotion motion => Fills(motion.Child),
        _ => (false, false),
    };
    /// <summary>
    /// A size on ONE axis. The axis matters for exactly one kind: the window is `100vh` down and
    /// `100vw` across, and every other kind reads the same either way.
    /// </summary>
    private string? Size(SizeValue value, bool vertical = false) => value.Kind switch
    {
        SizeKind.Fixed => TokenCss.Px(value.Value),
        SizeKind.Fill => "100%",
        // The window, less an inset. `calc` rather than a bare `vh` because the inset is the whole
        // point — see SizeKind.WindowMinus for why a constant cannot express this.
        SizeKind.WindowMinus => value.Value > 0
            ? $"calc(100{(vertical ? "vh" : "vw")} - {TokenCss.Px(value.Value)})"
            : $"100{(vertical ? "vh" : "vw")}",
        _ => null, // Hug = auto
    };
}
