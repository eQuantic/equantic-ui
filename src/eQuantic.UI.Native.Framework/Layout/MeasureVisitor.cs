using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What BOX each word of the vocabulary asks for — the layout engine's dispatch, moved off a switch
/// with a default arm and onto the visitor the compiler checks.
///
/// <para>
/// The default arm was not harmless. Thirty-seven of the forty doors THEN were named — a record of
/// that measurement, not a running count; `Column` and `Row`
/// arrived through `FlexNode`, and <see cref="Navigable"/> and <see cref="WebFrame"/> arrived
/// nowhere — they fell to <c>_ =&gt; ctx.Node(node)</c> and measured as a ZERO-SIZE box with no arm
/// saying so. Their reasons were not lost, but they lived in a coverage pin's exemption array,
/// where nothing tied them to the code that answered. They have doors now, and the doors keep the
/// distinction the array could not hold: one of the two cannot cross, and the other has simply not
/// been written.
/// </para>
///
/// <para>
/// The forty-one doors are a flat list here and the measurements live in the family files beside
/// them,
/// which is the shape <c>WebLoweringVisitor</c> settled on in S4: the list IS the exhaustive
/// surface, and a reader checking whether a node is handled reads it in one screen.
/// </para>
///
/// <para>
/// Built once per <see cref="LayoutContext"/> and held there
/// (<see cref="LayoutContext.MeasurePass"/>), which is what lets the context be a field: it is
/// fixed for the pass, so only the constraints and the path travel down, and they travel as a
/// struct. It was built per <see cref="LayoutEngine.Layout"/> CALL for one commit, which is one per
/// Overlay layer — invisible to a 74 KB per-frame budget with 0.1 KB of headroom and no overlay in
/// any harness scene. `PerfHarnessTests` counts the passes a context builds now, rather than hoping
/// to see them in bytes.
/// </para>
/// </summary>
internal sealed partial class MeasureVisitor : IVisualNodeVisitor<MeasureState, LayoutNode>
{
    /// <summary>The theme, the text metrics and the node pool this pass runs against — fixed for it.</summary>
    private readonly LayoutContext _ctx;

    internal MeasureVisitor(LayoutContext context)
    {
        _ctx = context;
        // Tallied on the context so a test can say "one pass, nine layers" — see PassesBuilt.
        context.CountPass();
    }

    /// <summary>
    /// Measures, then stamps the node with WHERE it is. Every node gets the path, so any gesture
    /// that has to survive a frame can key on it rather than on an object identity the next Build
    /// will not share.
    /// <para>
    /// It still takes the context it will not use, so that every recursive call inside the family
    /// files reads exactly as it did on the switch. The parameter is the seam's, not the funnel's:
    /// a measurement asks for what it reads, and this one hands the visitor's own field back to it.
    /// </para>
    /// </summary>
    internal LayoutNode Measure(VisualNode node, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var measured = node.Accept(this, new MeasureState(constraints, path));
        measured.Path ??= path;
        return measured;
    }

    // ---- the forty-one doors ---------------------------------------------------------------------

    // Only the nodes that can HAVE an auto size worth stretching take the flags; for the rest
    // (text, images, fixed primitives) the parent's decision changes nothing.
    public LayoutNode Visit(Box node, MeasureState s) => MeasureBox(node, s.Constraints, _ctx, s.Path);

    // Row and Column are ONE algorithm and two doors, the way S4 gave them two lowerings over one
    // LowerFlex: the switch bound them together as `FlexNode flex`, and a reader looking for where a
    // Row is measured should find a Row.
    public LayoutNode Visit(Row node, MeasureState s) => MeasureFlex(node, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(Column node, MeasureState s) => MeasureFlex(node, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(Stack node, MeasureState s) => MeasureStack(node, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(Grid node, MeasureState s) => MeasureGrid(node, s.Constraints, _ctx, s.Path);

    // Spec S6: an AdaptiveNode IS its resolved variant on native — the other variants never measure,
    // never paint (the web keeps them, CSS-gated).
    public LayoutNode Visit(AdaptiveNode node, MeasureState s) =>
        Measure(node.Resolve(_ctx.SizeClass), s.Constraints, _ctx, _ctx.ChildPath(s.Path, 0));

    // Spec S7: Pinned renders IN FLOW on native until engine scrolling lands (correct at scroll
    // offset 0); the pinning joins the scroll compositor (fence on the node's doc).
    public LayoutNode Visit(Pinned node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // The system's own margins. A desktop window has no cutouts, so the host reports zero and the
    // node measures as its child plus whatever padding the caller asked for on top — the SAME tree
    // an iPhone insets, with the numbers coming from the host rather than the app.
    public LayoutNode Visit(SafeArea node, MeasureState s) => MeasureSafeArea(node, s.Constraints, _ctx, s.Path);

    // Wave 3: the anchor owns layout; the panel realizes in the realizer's overlay pass.
    public LayoutNode Visit(Anchored node, MeasureState s) =>
        MeasureWrapper(node, node.Anchor, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(ScrollView node, MeasureState s) => MeasureScrollView(node, s.Constraints, _ctx, s.Path);

    // A continuous gesture is layout-transparent: the offset is a PAINT translate, exactly like the
    // sheet's. Re-laying out under a finger would fight the scroll it usually lives in.
    public LayoutNode Visit(Draggable node, MeasureState s) => MeasureDraggable(node, s.Constraints, _ctx, s.Path);

    // A Positioned outside a Stack has no anchor frame — degrade to a transparent wrapper.
    public LayoutNode Visit(Positioned node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(Text node, MeasureState s) => MeasureText(node, s.Constraints, _ctx);

    public LayoutNode Visit(TextEntry node, MeasureState s) => MeasureTextEntry(node, s.Constraints.MaxWidth, _ctx);

    // Images are an explicitly sized slot - layout can't infer extent from undecoded sources (A11).
    public LayoutNode Visit(Image node, MeasureState s) =>
        _ctx.Node(node, new Rect(0, 0, node.Width, node.Height));

    public LayoutNode Visit(CameraPreview node, MeasureState s) =>
        _ctx.Node(node, new Rect(0, 0, node.Width, node.Height));

    // Icons are a fixed square em-box (§07 whitelist) and ignore Dynamic Type (spec A10).
    public LayoutNode Visit(Icon node, MeasureState s) => _ctx.Node(node, new Rect(0, 0, node.Size, node.Size));

    // A vector is the same em-box at the size the author asked for — square unless they gave it an
    // aspect, which is what a diagram's connector or a banner rule needs.
    public LayoutNode Visit(Vector node, MeasureState s) =>
        _ctx.Node(node, new Rect(0, 0, node.Size, node.Height));

    // Artwork is an explicitly placed box too — its height comes from the drawing's own aspect when
    // the author gave only a width, which the node already resolved.
    public LayoutNode Visit(Drawing node, MeasureState s) =>
        _ctx.Node(node, new Rect(0, 0, node.Width, node.Height));

    // The Spinner shares the icon em-box contract (spec B15: sizes = the §07 whitelist).
    public LayoutNode Visit(Spinner node, MeasureState s) => _ctx.Node(node, new Rect(0, 0, node.Size, node.Size));

    // A canvas takes the box its SizeValue asks for — Fill by default, because a visualization wants
    // the room it is offered. It has no content to hug: what it draws is arithmetic over whatever
    // box it ends up with, which is the whole point of it.
    public LayoutNode Visit(Canvas node, MeasureState s) => _ctx.Node(node, new Rect(0, 0,
        ResolveSelf(node.Width, s.Constraints.MaxWidth, 0, _ctx.WindowWidth, s.Constraints.Width.Indeterminate),
        ResolveSelf(node.Height, s.Constraints.MaxHeight, 0, _ctx.WindowHeight, s.Constraints.Height.Indeterminate)));

    // The INLINE-BLOCK barrier (CSS twin): a button, a link and an input are not block-level — a
    // BLOCK container does not stretch them (they hug), while a FLEX stretch reaches through
    // (align-items: stretch stretches any item, buttons included).
    public LayoutNode Visit(Pressable node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints.Inline(), _ctx, s.Path);

    // Transparent to layout: the surface adds a caret and a selection, never a box.
    public LayoutNode Visit(CodeSurface node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(SheetSurface node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // Pointer presence is layout-transparent (S5 programmable hover — the child owns visuals).
    public LayoutNode Visit(Hoverable node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // A simulated state changes only what is DRAWN, so it takes no space of its own.
    public LayoutNode Visit(Simulated node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // Reports presence; it neither takes space nor draws.
    public LayoutNode Visit(InView node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // The intent has to be armed while the child BUILDS — by layout time the tree already has the
    // scrim in it, and there is nothing left to decide.
    public LayoutNode Visit(InFlow node, MeasureState s) => MeasureInFlow(node, s.Constraints, _ctx, s.Path);

    // Spec S8: a Shortcut is layout-transparent — the binding rides the realizer's walk.
    public LayoutNode Visit(Shortcut node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    public LayoutNode Visit(Adjustable node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints.Inline(), _ctx, s.Path);

    // The fifth wrapper that emits a host of its own, so it releases block stretch for the reason
    // the other four do (BlockStretch_DoesNotCrossAWrapperThatEmitsItsOwnHost): `LowerLiveRegion`
    // gives the host `fit-content` when the child does not ask to fill, and the box a reader
    // outlines has to be the box Photon lays out.
    public LayoutNode Visit(LiveRegion node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints.Inline(), _ctx, s.Path);

    // INLINED like the three wrappers above it, and the reason this comment once said otherwise is
    // worth keeping: "a progress bar reports across whatever it is given" sounds right and is not
    // what the other target does. `LowerProgress` gives the host `fit-content` when the child does
    // not ask to fill, so a hugging child leaves a hugging box on the web — and the box is the
    // announcement, because it is what a reader outlines. Passing the constraints through let
    // StretchKind.Block cross: measured, a hugging Row inside a 600-wide Box came back 600 under
    // Progress and 40 under Adjustable, Pressable and Link. A fixed-width Box child hides it
    // completely, which is why the first probe of this found nothing.
    public LayoutNode Visit(Progress node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints.Inline(), _ctx, s.Path);

    // A Link is layout-transparent (semantics + interaction only — the child owns visuals).
    public LayoutNode Visit(Link node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints.Inline(), _ctx, s.Path);

    public LayoutNode Visit(Flexible node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // Loop motion is layout-transparent: the offset is a REALIZE-time transform (spec §06 —
    // transform-only frames never re-lay-out).
    public LayoutNode Visit(LoopMotion node, MeasureState s) =>
        MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.Path);

    // Enter motion is layout-transparent too (opacity layer + paint-only translate) — but the
    // progress is resolved HERE, where the stable path exists, and stamped on the node.
    public LayoutNode Visit(Presence node, MeasureState s) => MeasurePresence(node, s.Constraints, _ctx, s.Path);

    // Drag-to-dismiss follows the same pattern: transparent for layout, offset stamped by path.
    public LayoutNode Visit(DragDismiss node, MeasureState s) => MeasureDragDismiss(node, s.Constraints, _ctx, s.Path);

    // An Overlay is ZERO in the page flow — the realizer lays its child out against the VIEWPORT in
    // the overlay pass (path "ov<i>", stable for the reconciler).
    public LayoutNode Visit(Overlay node, MeasureState s) => _ctx.Node(node);

    public LayoutNode Visit(Spacer node, MeasureState s) => _ctx.Node(node); // zero outside a flex container (layout-only)

    /// <summary>
    /// The two-dimensional composite behind a calendar, laid out — see <see cref="MeasureNavigable"/>
    /// for how and why that shape.
    /// <para>
    /// It was WEB-ONLY, and the sentence that said so stood here: "Photon measures it as nothing, so
    /// its rows never lay out at all". It was the weaker of the two exemptions the old coverage pin
    /// held — a <see cref="WebFrame"/> CANNOT cross, while this one simply had not been written, and
    /// nothing about a calendar is web-shaped. It is written now (#248).
    /// </para>
    /// </summary>
    public LayoutNode Visit(Navigable node, MeasureState s) => MeasureNavigable(node, s.Constraints, _ctx, s.Path);

    /// <summary>
    /// CANNOT CROSS, which is the stronger of the two and settled rather than pending. A WebFrame is
    /// the DOM escape hatch — an embedded document with a sandbox — and there is no browser behind a
    /// Photon surface to put one in. <c>SemanticsVisitor</c> says the same in one word
    /// (<c>EscapeHatch</c>) and the email walk says it by refusing the node outright.
    /// <para>
    /// Zero is the right box for something that cannot be drawn, so the behaviour is unchanged and
    /// the reason moves from an exemption array to the arm it describes.
    /// </para>
    /// </summary>
    public LayoutNode Visit(WebFrame node, MeasureState s) => _ctx.Node(node);

    /// <summary>
    /// The seam — an app's own component, the one node kind the interface cannot enumerate. It
    /// expands INLINE: Build produces its subtree (pure, mode-free), which is measured in place, so
    /// the component wraps it in the layout tree and draws nothing itself.
    /// </summary>
    public LayoutNode Visit(UiComponent node, MeasureState s) => MeasureComponent(node, s.Constraints, _ctx, s.Path);

    // ---- what every wrapper does ----------------------------------------------------------------

    /// <summary>
    /// The rows lay out as a COLUMN of themselves, which is exactly what the other target does: a
    /// <c>Navigable</c> lowers to a block <c>div[role=grid]</c> whose <c>role="row"</c> wrappers are
    /// <c>display: contents</c>, so the caller's own row node becomes a direct child of the grid box
    /// and the grid stacks them. No gap, no padding and no cross alignment of its own — the rows are
    /// measured against the constraints the grid was given, so every layout and styling decision
    /// stays the caller's, which is what <see cref="Navigable.Rows"/> promises in words.
    /// <para>
    /// Before this the arm was <c>_ctx.Node(node)</c>: the node measured, none of its rows did, and a
    /// calendar was a grid with no days in it — nothing laid out, nothing painted, and the group
    /// #187 gave it announced over an empty subtree (#248).
    /// </para>
    /// </summary>
    private LayoutNode MeasureNavigable(Navigable node, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = ctx.Node(node);
        var y = 0f;
        var widest = 0f;
        for (var index = 0; index < node.Rows.Count; index++)
        {
            // KEYED, like every other multi-child arm: a row that says Key takes the key as its
            // segment, so a grid whose rows are rebuilt or reordered keeps each row's identity —
            // and the path IS identity here (focus, hover, scroll offset, a drag in flight).
            var row = Measure(node.Rows[index], constraints, ctx,
                ctx.ChildPath(path, index, node.Rows[index]));
            row.Bounds = row.Bounds with { X = 0, Y = y };
            result.Adopt(row);
            y += row.Bounds.Height;
            widest = MathF.Max(widest, row.Bounds.Width);
        }
        result.Bounds = new Rect(0, 0, widest, y);
        return result;
    }

    private LayoutNode MeasureWrapper(VisualNode node, VisualNode child, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = ctx.Node(node);
        // Layout-transparent means transparent to STRETCH too: whatever the parent would stretch,
        // it stretches through the wrapper — a Pressable around a tab cell (or the component node
        // around a page) must not eat the size the parent granted.
        var inner = Measure(child, constraints, ctx, ctx.ChildPath(path, 0));
        result.Adopt(inner);
        result.Bounds = new Rect(0, 0, inner.Bounds.Width, inner.Bounds.Height);
        return result;
    }
}
