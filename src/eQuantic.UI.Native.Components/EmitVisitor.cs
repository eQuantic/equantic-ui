using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What the GPU draws for a laid-out node — the sixth and LAST dispatch over the vocabulary to
/// answer to the compiler instead of to a reader's memory (S6; see <c>docs/VOCABULARY-DISPATCH-PLAN.md</c>).
///
/// <para>
/// It replaced a pair: an eighteen-arm chrome switch and, after it, a nine-branch <c>is</c> chain
/// every falling-through node reached. They looked like two dispatches and were not: NOTHING that
/// fell through ever touched its children — the only three arms that descended were the only three
/// that returned. So the second half was never a second decision, it was what to do with the
/// children, which is the same node's business. One door per node says both.
/// </para>
///
/// <para>
/// <c>Box</c> is why this matters. It was the one node in both halves, four hundred lines apart:
/// the arm painted its chrome and the branch decided whether to confine its children. A single
/// visitor with 18 + 9 doors would have had to drop one of them. <see cref="Visit(Box, EmitState)"/>
/// has both, in the order the old comment asserted across that distance — "chrome already drew
/// unclipped above".
/// </para>
///
/// <para>
/// ONE PER FRAME. <see cref="PhotonRealizer"/> builds it once and walks the page and every overlay
/// layer with it; what is per-layer rides in <see cref="EmitState"/> instead. #229 measured what the
/// other arrangement costs.
/// </para>
/// </summary>
internal sealed partial class EmitVisitor : IVisualNodeVisitor<EmitState, Nothing>
{
    /// <summary>The one instance there needs to be. It holds nothing, so it is not per frame,
    /// per layer or per anything — <see cref="EmitState"/> carries all of it.</summary>
    internal static readonly EmitVisitor Shared = new();

    private EmitVisitor() { }

    /// <summary>
    /// The entry, and the group-opacity/transform wrapper that has to sit OUTSIDE the dispatch
    /// because it wraps the whole box — chrome and children alike.
    /// </summary>
    internal void Emit(in EmitState s)
    {
        var node = s.Node;
        // Spec S1 — group opacity + static transform wrap the WHOLE box (chrome and children):
        // opacity is one PushLayer composite (overlaps never double-blend); the transform is the
        // center-anchored Matrix2D twin of the CSS list, paint-only (layout already ran).
        // Spec S6: when the box declares a Transition, the layer's alpha and the transform's
        // components GLIDE to their new values under the box's own spec (colours and shadow glide
        // in the Box door). The guard reads the INTERPOLATED values, not the declared ones: a box
        // whose opacity just went 0.4 → 1 still needs its layer while it is still fading in.
        // A box that declares NO Transition pays nothing here — not even the track-key strings: the
        // allocation harness pins steady frames under a ceiling, and the first version of this
        // built keys for every box in every frame. Only a Transition opens the gliding path.
        var opacityNow = node.Source is Box ob ? ob.Style.Opacity ?? 1f : 1f;
        var transformNow = node.Source is Box tb ? tb.Style.Transform ?? IdentityTransform : IdentityTransform;
        if (node.Source is Box gliding && gliding.Style.Transition is { } glide && s.Motion.Transitions is { } store)
        {
            var glidePath = node.Path ?? "";
            if ((glide.Channels & StyleChannels.Opacity) != 0)
                opacityNow = store.Resolve(glidePath + ":opacity", opacityNow, s.Motion.TimeMs, glide, s.Motion.Reduced);
            if ((glide.Channels & StyleChannels.Transform) != 0)
                transformNow = GlideTransform(store, glidePath, transformNow, s.Motion.TimeMs, glide, s.Motion.Reduced);
        }
        if (node.Source is Box && (opacityNow < 1f || !transformNow.IsIdentity))
        {
            var opacity = opacityNow < 1f ? opacityNow : (float?)null;
            if (opacity is { } layerAlpha) s.Builder.PushLayer(layerAlpha);
            var transformed = !transformNow.IsIdentity;
            if (transformed)
                s.Builder.PushTransform(CenterAnchored(transformNow, node.Bounds.Center));

            Dispatch(s);

            if (transformed) s.Builder.Pop();
            if (opacity is not null) s.Builder.PopLayer();
            return;
        }

        Dispatch(s);
    }

    /// <summary>
    /// The press and focus reads every node makes before its own door runs: a Pressable arms the
    /// token swap and the first descendant Box consumes it, so this cannot live in any one door.
    /// </summary>
    private void Dispatch(in EmitState s)
    {
        var node = s.Node;
        var press = s.Press;
        if (press.IsTracked(node, press.Pressed, press.PressedPath) && press.Pressed?.PressedBackground is { } pressedFill)
            press.PendingFill = pressedFill;
        // A SIMULATED press takes the fill from the Pressable being pictured — the host is tracking
        // nothing, so there is no tracked node to read it from.
        else if ((press.Simulated & SimulatedState.Pressed) != 0
            && node.Source is Pressable { PressedBackground: { } simulatedFill })
            press.PendingFill = simulatedFill;
        if ((press.Focused is not null || press.FocusedPath is not null)
            && press.IsTracked(node, press.Focused, press.FocusedPath))
            press.PendingFocusRing = true;
        else if ((press.Simulated & SimulatedState.Focused) != 0 && node.Source is Pressable)
            press.PendingFocusRing = true;

        node.Source.Accept(this, s);
    }

    /// <summary>Paint the children where they were laid out. The default arm of the old dispatch,
    /// and still what most nodes want after their own chrome.</summary>
    private void Descend(EmitState s)
    {
        foreach (var child in s.Node)
            Emit(s with { Node = child });
    }

    // ---- the box, and the one node that was in both halves ----------------------------------

    /// <summary>Chrome first, unclipped; then the children, confined to the rrect only if the box
    /// says so. Two halves of one decision that used to sit four hundred lines apart.</summary>
    public Nothing Visit(Box node, EmitState s)
    {
        EmitBoxChrome(node, s);
        if (node.Style.Clip) EmitClippedBox(node, s); else Descend(s);
        return Nothing.Value;
    }

    // ---- chrome, then the children ----------------------------------------------------------

    /// <summary>A flex container is chrome only when it declares a background; otherwise it is pure
    /// geometry that layout already resolved.</summary>
    public Nothing Visit(Row node, EmitState s) => FlexThenChildren(node, s);

    /// <inheritdoc cref="Visit(Row, EmitState)"/>
    public Nothing Visit(Column node, EmitState s) => FlexThenChildren(node, s);

    private Nothing FlexThenChildren(FlexNode node, EmitState s)
    {
        if (node.Background is not null) EmitFlexBackground(node, s);
        Descend(s);
        return Nothing.Value;
    }

    public Nothing Visit(Text node, EmitState s) { EmitTextNode(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(TextEntry node, EmitState s) { EmitTextEntry(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(CodeSurface node, EmitState s) { EmitCode(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Image node, EmitState s) { EmitImageNode(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(CameraPreview node, EmitState s) { EmitCamera(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Icon node, EmitState s) { EmitIcon(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Vector node, EmitState s) { EmitVector(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Drawing node, EmitState s) { EmitDrawing(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Canvas node, EmitState s) { EmitCanvas(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Spinner node, EmitState s) { EmitSpinnerNode(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Pressable node, EmitState s) { EmitPressable(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Hoverable node, EmitState s) { EmitHoverable(node, s); Descend(s); return Nothing.Value; }
    public Nothing Visit(Shortcut node, EmitState s) { EmitShortcut(node, s); Descend(s); return Nothing.Value; }

    // ---- nodes that descend themselves, because HOW they descend is the point -----------------

    public Nothing Visit(Simulated node, EmitState s) { EmitSimulated(node, s); return Nothing.Value; }
    public Nothing Visit(Primitives.InView node, EmitState s) { EmitInView(node, s); return Nothing.Value; }
    public Nothing Visit(Adjustable node, EmitState s) { EmitAdjustable(node, s); return Nothing.Value; }
    public Nothing Visit(ScrollView node, EmitState s) { EmitScrollView(node, s); return Nothing.Value; }
    public Nothing Visit(SheetSurface node, EmitState s) { EmitSheet(node, s); return Nothing.Value; }
    public Nothing Visit(Overlay node, EmitState s) { EmitOverlay(node, s); return Nothing.Value; }
    public Nothing Visit(Anchored node, EmitState s) { EmitAnchored(node, s); return Nothing.Value; }
    public Nothing Visit(LoopMotion node, EmitState s) { EmitLoopMotion(node, s); return Nothing.Value; }
    public Nothing Visit(Link node, EmitState s) { EmitLink(node, s); return Nothing.Value; }
    public Nothing Visit(Presence node, EmitState s) { EmitPresence(node, s); return Nothing.Value; }

    /// <summary>A drag surface only registers once layout has given it a path; without one there is
    /// nothing for the host to route to, and the node is just its children.</summary>
    public Nothing Visit(Draggable node, EmitState s) => DragThenChildren(s);

    /// <inheritdoc cref="Visit(Draggable, EmitState)"/>
    public Nothing Visit(DragDismiss node, EmitState s) => DragThenChildren(s);

    private Nothing DragThenChildren(EmitState s)
    {
        if (s.Node.DragPath is { } dragPath) EmitDrag(s, dragPath); else Descend(s);
        return Nothing.Value;
    }

    // ---- nothing left to draw ------------------------------------------------------------------
    //
    // The eleven below were an array of exemption STRINGS in `VocabularyCoverageTests` until this
    // slice, with one sentence covering all of them. They have three different standings, which an
    // array of names could not show and a door can:
    //
    //   * resolved into geometry — layout turned the positioner into the bounds its child now
    //     carries, so there is nothing of the node itself left to paint. Its children are the
    //     picture, which is what `Descend` says.
    //   * cannot cross — a WebFrame embeds a document, and there is no browser behind a Photon
    //     surface. `SemanticsVisitor` says the same thing in one word (`EscapeHatch`).
    //   * not written — Navigable is a real two-dimensional composite the web realizer lays out as
    //     a grid, and Photon has never painted it. #229 gave it the same standing in the layout
    //     pass; this is the other half.

    /// <summary>Resolved into geometry: the variant IS this node by the time paint runs.</summary>
    public Nothing Visit(AdaptiveNode node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: a weight the flex pass already spent.</summary>
    public Nothing Visit(Flexible node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: the cells carry their own bounds.</summary>
    public Nothing Visit(Grid node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: in-flow is where the child already is.</summary>
    public Nothing Visit(InFlow node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: the pin is a position layout applied.</summary>
    public Nothing Visit(Pinned node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: a contract with a Stack, settled before paint.</summary>
    public Nothing Visit(Positioned node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: the insets are already in the child's bounds.</summary>
    public Nothing Visit(SafeArea node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: space, which is drawn by not drawing.</summary>
    public Nothing Visit(Spacer node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>Resolved into geometry: layering is the order the children are walked in.</summary>
    public Nothing Visit(Stack node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>NOT WRITTEN. The web realizer lays its declared rows out as a grid; Photon never
    /// has. A real node with real semantics, and this is the gap, named where the behaviour is.</summary>
    public Nothing Visit(Navigable node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>CANNOT CROSS. It embeds a document, and there is no browser behind a Photon
    /// surface — the DOM escape hatch, settled rather than pending.</summary>
    public Nothing Visit(WebFrame node, EmitState s) { Descend(s); return Nothing.Value; }

    /// <summary>
    /// CANNOT ARRIVE, and it was never in the exemption array — the twelfth absence, invisible to
    /// that instrument because it asks only about non-abstract nodes and this one is abstract.
    /// Components are expanded during the LAYOUT pass (<c>MeasureVisitor</c> via
    /// <c>ExpandContained</c>), so what reaches paint is the built subtree, never the component.
    /// It descends rather than refusing, because this slice moved the dispatch and did not change
    /// what it does; turning an impossible state into a loud one is its own decision.
    /// </summary>
    public Nothing Visit(UiComponent node, EmitState s) { Descend(s); return Nothing.Value; }
}
