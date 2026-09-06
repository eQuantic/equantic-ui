using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// WHO is stretching an auto-sized child — because CSS gives the two origins different reach:
/// a FLEX stretch (align-items: stretch) stretches any item, buttons included; a BLOCK stretch
/// (a div's child div is full-width) stops at inline-block boundaries — a button, a link, an
/// input hug inside a block container.
/// </summary>
public enum StretchKind
{
    None,
    Flex,
    Block,
}

/// <summary>Everything a layout pass needs besides the tree: theme (type styles), text metrics, Dynamic Type factor.</summary>
public sealed class LayoutContext
{
    /// <summary>
    /// Cross-frame cache of child path strings. A path is IDENTITY — "0/1/2" this frame must be
    /// the very same string next frame — and the tree's shape barely changes between frames, so
    /// the host lends a dictionary that survives them and steady-state path allocation drops to
    /// zero. Null (tests, one-shot layouts) simply concatenates.
    /// </summary>
    public Dictionary<(string Parent, int Index), string>? PathCache { get; init; }

    /// <summary>
    /// The WINDOW this pass is laying out into, in dp. Set by <see cref="LayoutEngine.Layout"/>
    /// from the viewport it is handed, because a window-relative cap
    /// (<see cref="SizeKind.WindowMinus"/>) cannot be resolved from the available space: an
    /// overlay deep in a tree is offered whatever its parent has left, which is not the window.
    /// </summary>
    internal float WindowWidth { get; set; }

    /// <inheritdoc cref="WindowWidth"/>
    internal float WindowHeight { get; set; }

    /// <summary>The node pool the OWNING host lends for this pass — null (tests, one-shot
    /// layouts) allocates fresh. The ownership story lives on <see cref="LayoutNodePool"/>.</summary>
    public LayoutNodePool? Pool { get; init; }

    // The single factory every laid-out node passes through.
    internal LayoutNode Node(VisualNode source) => Pool?.Rent(source) ?? new(source);

    internal LayoutNode Node(VisualNode source, Rect bounds)
    {
        var node = Node(source);
        node.Bounds = bounds;
        return node;
    }

    internal string ChildPath(string parent, int index)
    {
        if (PathCache is not { } cache) return parent + "/" + index;
        if (!cache.TryGetValue((parent, index), out var cached))
            cache[(parent, index)] = cached = parent + "/" + index;
        return cached;
    }

    public LayoutContext(IAppTheme theme, ITextMeasurer measurer, float typeScale = 1f,
        Density density = Density.Comfortable)
    {
        Theme = theme;
        Measurer = measurer;
        TypeScale = typeScale;
        Density = density;
        // The measurer the layout already holds, handed to components as a SEAM — a code editor
        // maps a click to a column with it, and nothing else in the tree pays for it.
        Components = new ComponentContext(theme, typeScale, density,
            (text, style) => measurer.Measure(text, style, typeScale, float.PositiveInfinity, 1).Width);
    }

    /// <summary>How tight this target's controls are (see <see cref="Primitives.Density"/>).</summary>
    public Density Density { get; }

    /// <summary>
    /// Whether the parent's own size on this axis is being decided BY the content — a Hug. Fill has
    /// nothing to fill against there: a child that takes the maximum available would decide the
    /// size of the very thing that was supposed to measure it, which is how a 16dp badge came out
    /// as wide as the window. On an indeterminate axis, Fill resolves to the intrinsic size, the
    /// same rule CSS shrink-to-fit and Flutter's min-size rows follow.
    /// </summary>
    public bool IndeterminateWidth { get; set; }

    public bool IndeterminateHeight { get; set; }

    /// <summary>
    /// The MIRROR of the indeterminate flags: an axis the parent has already decided FOR this child,
    /// because it aligns stretch. There, a Hug is not a hug — a stretched flex item with an auto
    /// cross size IS the line's size, which is what CSS does and what makes `Main = Center` inside
    /// it have any room to centre in. Without this the box was stretched after its contents had
    /// already been laid out inside the smaller one, so a centred tab label sat against the left
    /// edge of its cell while the indicator underneath spanned the whole cell.
    /// <para>One-shot: consumed by the child it was set for, never inherited by that child's own
    /// children — the same save/restore discipline the indeterminate flags use.</para>
    /// </summary>
    public StretchKind StretchWidth { get; set; }

    public StretchKind StretchHeight { get; set; }

    public IAppTheme Theme { get; }

    /// <summary>Spec S6: the window size class layout resolves AdaptiveNodes against — derived from
    /// the viewport width by the realizer (re-layout happens naturally when a resize crosses a
    /// threshold, because the class is a pure function of the width).</summary>
    public WindowSizeClass SizeClass { get; init; }

    /// <summary>
    /// The margins the SYSTEM owns — notch, status bar, home indicator. The HOST reports them; a
    /// desktop window has no cutouts, so the default of zero is the correct answer there, and the
    /// same tree insets properly the moment a phone shell fills them in.
    /// </summary>
    public EdgeInsets SafeAreaInsets { get; init; }

    /// <summary>
    /// The room the system's WINDOW CONTROLS take out of the app's top strip under a unified desktop
    /// chrome — Start on a Mac (the traffic lights), End on Windows (the caption buttons), Top the
    /// strip's height. Zero wherever the window has a bar of its own. Consumed only by a
    /// <see cref="SafeArea"/> that asks for <see cref="SafeEdges.WindowControls"/>: the toolbar that
    /// lives in the strip, and nothing below it.
    /// </summary>
    public EdgeInsets WindowControlsInsets { get; init; }

    /// <summary>Spec B14: the host's value-transition animator (null = values snap).</summary>
    public TransitionStore? Transitions { get; init; }

    /// <summary>Spec §06: the host's enter-motion clock behind <see cref="Presence"/> (null = subtrees
    /// appear settled — SSR-like single-shot renders and layout-only tests need no entrance).</summary>
    public PresenceStore? Presences { get; init; }

    /// <summary>Scroll compositor v1: the host's scroll offsets (null = programmatic Offset only).</summary>
    public ScrollStore? ScrollOffsets { get; init; }

    /// <summary>Gestures v2: the host's drag offsets behind <see cref="DragDismiss"/> (null = at rest).</summary>
    public DragStore? Drags { get; init; }

    /// <summary>Per-frame bridge from a laid-out ScrollView to its path/max — the realizer reads it
    /// while emitting to register scroll regions for the host's input routing.</summary>
    public Dictionary<ScrollView, (string Path, float MaxOffset)>? ScrollMeta { get; init; }

    /// <summary>The frame clock the transitions resolve against (same clock as loop motion).</summary>
    public float TimeMs { get; init; }

    /// <summary>Reduce Motion: transitions snap statically (spec §06 parity with loop motion).</summary>
    public bool ReducedMotion { get; init; }
    public ITextMeasurer Measurer { get; }
    public float TypeScale { get; }

    /// <summary>The host's positional reconciler — null keeps the v1 rebuild-everything behavior.</summary>
    public ComponentInstanceStore? Instances { get; init; }

    /// <summary>The mode-free context handed to <see cref="UiComponent.Build"/> during expansion.</summary>
    public ComponentContext Components { get; }
}

/// <summary>A laid-out node: source, ABSOLUTE bounds (after the layout pass), children, text metrics.</summary>
public sealed class LayoutNode
{
    public LayoutNode(VisualNode source) => Source = source;

    public VisualNode Source { get; private set; }
    public Rect Bounds { get; internal set; }
    public List<LayoutNode> Children { get; } = new();
    public TextMeasurement? Text { get; internal set; }

    /// <summary>
    /// The pieces of a RICH paragraph (<see cref="Primitives.Text.Spans"/>), placed: one per run per
    /// line, in reading order, positioned relative to this node's own box. Null for a plain
    /// paragraph, which is one piece by definition and needs no list to say so.
    /// </summary>
    public IReadOnlyList<TextFragment>? TextRuns { get; internal set; }

    /// <summary>Entrance progress (0..1) stamped at MEASURE time when <see cref="Source"/> is a
    /// <see cref="Presence"/> — the emit pass reads it (paths exist only during layout). 1 = settled
    /// (no presence clock in the context, or the entrance finished).</summary>
    public float Presence { get; internal set; } = 1f;

    /// <summary>The stable layout path of a <see cref="Presence"/> node, stamped at measure time —
    /// the emit pass keys its exit snapshot by it (paths exist only during layout).</summary>
    public string? PresencePath { get; internal set; }

    /// <summary>
    /// Where this node sits in the tree, in the same stable form the scroll and drag stores key on.
    /// A gesture outlives the frame it began in — the press repaints, the next Build makes fresh
    /// nodes, and a target remembered by REFERENCE is gone by the time the finger lifts. The path
    /// is what stays the same.
    /// </summary>
    public string? Path { get; internal set; }

    /// <summary>The drag offset (dp downward) of a <see cref="DragDismiss"/> node, resolved at
    /// measure time against the host's drag clock — the emit pass paints the translate.</summary>
    public float DragOffset { get; internal set; }

    /// <summary>The stable layout path of a <see cref="DragDismiss"/> node — the host routes drag
    /// input by it (registered with the frame's drag regions at emit).</summary>
    public string? DragPath { get; internal set; }

    /// <summary>Back to factory state for the POOL: every field a layout pass stamps, cleared —
    /// the child list is kept (emptied), which is the allocation the pool exists to save.</summary>
    internal void Reset(VisualNode source)
    {
        Source = source;
        Bounds = default;
        Children.Clear();
        Text = null;
        TextRuns = null;
        Presence = 1f;
        PresencePath = null;
        Path = null;
        DragOffset = 0f;
        DragPath = null;
    }
}

/// <summary>
/// Recycler for <see cref="LayoutNode"/>s — the frame's dominant allocation. Ownership is the
/// whole design (a double-buffer once rewrote trees under 155 retained results): nodes come back
/// ONLY through <see cref="RecycleTree"/>, and the only caller is a host that OWNS the frame it
/// is discarding — opt-in via <c>PhotonHost.RecycleFrames</c>, default off, so anything that
/// retains a <c>RealizeResult</c> (tests, tools) keeps a tree nobody rewrites.
/// </summary>
public sealed class LayoutNodePool
{
    private readonly Stack<LayoutNode> _free = new();

    /// <summary>How many nodes are parked — the reuse assertion's window into the pool.</summary>
    public int FreeCount => _free.Count;

    internal LayoutNode Rent(VisualNode source)
    {
        if (_free.Count == 0) return new LayoutNode(source);
        var node = _free.Pop();
        node.Reset(source);
        return node;
    }

    /// <summary>Returns a whole laid-out tree. The caller asserts nobody else holds it.</summary>
    public void RecycleTree(LayoutNode root)
    {
        for (var i = 0; i < root.Children.Count; i++) RecycleTree(root.Children[i]);
        _free.Push(root);
    }
}

/// <summary>
/// The Photon flex layout engine (spec A2 — "own C# flex implementation"): single main axis, token
/// gaps that never collapse, <c>Flexible(n)</c> children sharing LEFTOVER space by weight, and the
/// truncation contract — TEXT children shrink to ellipsis before any sibling is pushed out; fixed
/// children never shrink. Size resolution per node: explicit &gt; Fill &gt; Hug (spec A1). The web
/// realizer does NOT use this engine (the browser lays out CSS flex); parity between the two is the
/// job of the cross-target layout harness (docs/SHARED-COMPONENTS-PLAN.md).
/// </summary>
public static class LayoutEngine
{
    public static LayoutNode Layout(VisualNode root, float viewportWidth, float viewportHeight, LayoutContext context,
        string rootPath = "r")
    {
        // NOTE: the reconciler pass is ENDED BY THE CALLER (PhotonRealizer) — one frame may run
        // several Layout calls (the page plus each Overlay subtree) sharing one retention pass.
        context.WindowWidth = viewportWidth;
        context.WindowHeight = viewportHeight;
        var node = Measure(root, viewportWidth, viewportHeight, context, rootPath);
        Absolutize(node, 0, 0);
        return node;
    }

    // ---- measurement (bounds are PARENT-RELATIVE until Absolutize) ------------------------------

    /// <summary>Measures, then stamps the node with WHERE it is. Every node gets the path, so any
    /// gesture that has to survive a frame can key on it rather than on an object identity the next
    /// Build will not share.</summary>
    private static LayoutNode Measure(VisualNode node, float maxW, float maxH, LayoutContext ctx, string path)
    {
        // The stretch flags belong to THIS node and to nothing under it: read and cleared here, so a
        // stretched row does not go on stretching every box inside it.
        var stretchW = ctx.StretchWidth;
        var stretchH = ctx.StretchHeight;
        ctx.StretchWidth = StretchKind.None;
        ctx.StretchHeight = StretchKind.None;

        var measured = MeasureCore(node, maxW, maxH, ctx, path, stretchW, stretchH);
        measured.Path ??= path;
        return measured;
    }

    private static LayoutNode MeasureCore(VisualNode node, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None) => node switch
    {
        // Only the nodes that can HAVE an auto size worth stretching take the flags; for the rest
        // (text, images, fixed primitives) the parent's decision changes nothing.
        Box box => MeasureBox(box, maxW, maxH, ctx, path, stretchW, stretchH),
        FlexNode flex => MeasureFlex(flex, maxW, maxH, ctx, path, stretchW, stretchH),
        Stack stack => MeasureStack(stack, maxW, maxH, ctx, path),
        Grid grid => MeasureGrid(grid, maxW, maxH, ctx, path),
        // Spec S6: an AdaptiveNode IS its resolved variant on native — the other variants never
        // measure, never paint (the web keeps them, CSS-gated).
        AdaptiveNode adaptive => MeasureRearmed(adaptive.Resolve(ctx.SizeClass), maxW, maxH, ctx, ctx.ChildPath(path, 0), stretchW, stretchH),
        // Spec S7: Pinned renders IN FLOW on native until engine scrolling lands (correct at scroll
        // offset 0); the pinning joins the scroll compositor (fence on the node's doc).
        Pinned pinned => MeasureWrapper(pinned, pinned.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // The system's own margins. A desktop window has no cutouts, so the host reports zero and
        // the node measures as its child plus whatever padding the caller asked for on top — the
        // SAME tree an iPhone insets, with the numbers coming from the host rather than the app.
        SafeArea safeArea => MeasureSafeArea(safeArea, maxW, maxH, ctx, path, stretchW, stretchH),
        // Wave 3: the anchor owns layout; the panel realizes in the realizer's overlay pass.
        Anchored anchored => MeasureWrapper(anchored, anchored.Anchor, maxW, maxH, ctx, path, stretchW, stretchH),
        ScrollView scroll => MeasureScrollView(scroll, maxW, maxH, ctx, path),
        // A Positioned outside a Stack has no anchor frame — degrade to a transparent wrapper.
        // A continuous gesture is layout-transparent: the offset is a PAINT translate, exactly like
        // the sheet's. Re-laying out under a finger would fight the scroll it usually lives in.
        Draggable draggable => MeasureDraggable(draggable, maxW, maxH, ctx, path, stretchW, stretchH),
        Positioned positioned => MeasureWrapper(positioned, positioned.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        Text text => MeasureText(text, maxW, ctx),
        TextEntry entry => MeasureTextEntry(entry, maxW, ctx),
        // Images are an explicitly sized slot - layout can't infer extent from undecoded sources (A11).
        Image image => ctx.Node(image, new Rect(0, 0, image.Width, image.Height)),
        CameraPreview camera => ctx.Node(camera, new Rect(0, 0, camera.Width, camera.Height)),
        // Icons are a fixed square em-box (§07 whitelist) and ignore Dynamic Type (spec A10).
        Icon icon => ctx.Node(icon, new Rect(0, 0, icon.Size, icon.Size)),
        // A vector is the same em-box at the size the author asked for — square unless they gave
        // it an aspect, which is what a diagram's connector or a banner rule needs.
        Vector vector => ctx.Node(vector, new Rect(0, 0, vector.Size, vector.Height)),
        // Artwork is an explicitly placed box too — its height comes from the drawing's own aspect
        // when the author gave only a width, which the node already resolved.
        Drawing drawing => ctx.Node(drawing, new Rect(0, 0, drawing.Width, drawing.Height)),
        // The Spinner shares the icon em-box contract (spec B15: sizes = the §07 whitelist).
        Spinner spinner => ctx.Node(spinner, new Rect(0, 0, spinner.Size, spinner.Size)),
        // A canvas takes the box its SizeValue asks for — Fill by default, because a visualization
        // wants the room it is offered. It has no content to hug: what it draws is arithmetic over
        // whatever box it ends up with, which is the whole point of it.
        Canvas canvas => ctx.Node(canvas, new Rect(0, 0,
            ResolveSelf(canvas.Width, maxW, 0, ctx.WindowWidth, ctx.IndeterminateWidth),
            ResolveSelf(canvas.Height, maxH, 0, ctx.WindowHeight, ctx.IndeterminateHeight))),
        // The INLINE-BLOCK barrier (CSS twin): a button, a link and an input are not block-level —
        // a BLOCK container does not stretch them (they hug), while a FLEX stretch reaches
        // through (align-items: stretch stretches any item, buttons included).
        Pressable pressable => MeasureWrapper(pressable, pressable.Child, maxW, maxH, ctx, path, Inline(stretchW), Inline(stretchH)),
        // Transparent to layout: the surface adds a caret and a selection, never a box.
        CodeSurface surface => MeasureWrapper(surface, surface.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        SheetSurface sheet => MeasureWrapper(sheet, sheet.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // Pointer presence is layout-transparent (S5 programmable hover — the child owns visuals).
        Hoverable hoverable => MeasureWrapper(hoverable, hoverable.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // A simulated state changes only what is DRAWN, so it takes no space of its own.
        Simulated simulated => MeasureWrapper(simulated, simulated.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // Reports presence; it neither takes space nor draws.
        InView inView => MeasureWrapper(inView, inView.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // The intent has to be armed while the child BUILDS — by layout time the tree already has
        // the scrim in it, and there is nothing left to decide.
        InFlow inFlow => MeasureInFlow(inFlow, maxW, maxH, ctx, path, stretchW, stretchH),
        // Spec S8: a Shortcut is layout-transparent — the binding rides the realizer's walk.
        Shortcut shortcut => MeasureWrapper(shortcut, shortcut.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        Adjustable adjustable => MeasureWrapper(adjustable, adjustable.Child, maxW, maxH, ctx, path, Inline(stretchW), Inline(stretchH)),
        // A Link is layout-transparent (semantics + interaction only — the child owns visuals).
        Link link => MeasureWrapper(link, link.Child, maxW, maxH, ctx, path, Inline(stretchW), Inline(stretchH)),
        Flexible flexible => MeasureWrapper(flexible, flexible.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // Loop motion is layout-transparent: the offset is a REALIZE-time transform (spec §06 —
        // transform-only frames never re-lay-out).
        LoopMotion motion => MeasureWrapper(motion, motion.Child, maxW, maxH, ctx, path, stretchW, stretchH),
        // Enter motion is layout-transparent too (opacity layer + paint-only translate) — but the
        // progress is resolved HERE, where the stable path exists, and stamped on the node.
        Presence presence => MeasurePresence(presence, maxW, maxH, ctx, path, stretchW, stretchH),
        // Drag-to-dismiss follows the same pattern: transparent for layout, offset stamped by path.
        DragDismiss drag => MeasureDragDismiss(drag, maxW, maxH, ctx, path, stretchW, stretchH),
        // An Overlay is ZERO in the page flow — the realizer lays its child out against the
        // VIEWPORT in the overlay pass (path "ov<i>", stable for the reconciler).
        Overlay => ctx.Node(node),
        // A component expands INLINE: Build produces its subtree (pure, mode-free), which is measured
        // in place — the component wraps it in the layout tree, drawing nothing itself.
        // Components RECONCILE by position first: the retained instance (state alive) replaces the
        // fresh one the parent just built, adopting its config; then it expands inline via Build.
        UiComponent component => MeasureComponent(component, maxW, maxH, ctx, path, stretchW, stretchH),
        Spacer => ctx.Node(node), // zero outside a flex container (layout-only)
        _ => ctx.Node(node),
    };

    /// <summary>
    /// Insets from the HOST (notch, status bar, home indicator) plus the caller's own padding. The
    /// host reports them; on a desktop window they are zero, which is the correct answer there.
    /// </summary>
    private static LayoutNode MeasureSafeArea(SafeArea safeArea, float maxW, float maxH,
        LayoutContext ctx, string path, StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var host = ctx.SafeAreaInsets;
        var top = (safeArea.Edges.HasFlag(SafeEdges.Top) ? host.Top : 0) + safeArea.Extra.Top;
        var bottom = (safeArea.Edges.HasFlag(SafeEdges.Bottom) ? host.Bottom : 0) + safeArea.Extra.Bottom;
        var start = (safeArea.Edges.HasFlag(SafeEdges.Start) ? host.Start : 0) + safeArea.Extra.Start;
        var end = (safeArea.Edges.HasFlag(SafeEdges.End) ? host.End : 0) + safeArea.Extra.End;
        // The window controls' corner is HORIZONTAL room only: the toolbar that asks for it IS the
        // strip they float over, so their height is its height, not a margin above it.
        if (safeArea.Edges.HasFlag(SafeEdges.WindowControls))
        {
            start += ctx.WindowControlsInsets.Start;
            end += ctx.WindowControlsInsets.End;
        }

        // Transparent to stretch, like every wrapper: the system's margins shrink the box, they
        // do not change who decides the size.
        ctx.StretchWidth = stretchW;
        ctx.StretchHeight = stretchH;
        var child = Measure(safeArea.Child, MathF.Max(0, maxW - start - end),
            MathF.Max(0, maxH - top - bottom), ctx, ctx.ChildPath(path, 0));
        child.Bounds = child.Bounds with { X = start, Y = top };

        var node = ctx.Node(safeArea,
            new Rect(0, 0, child.Bounds.Width + start + end, child.Bounds.Height + top + bottom));
        node.Children.Add(child);
        return node;
    }

    /// <summary>
    /// MIN-CONTENT width — the floor CSS puts under every flex item (<c>min-width: auto</c>), and
    /// the reason a browser shrinks the stretchy item rather than squashing the word next to it.
    /// For text it is the longest WORD (a word never breaks); for a row it is the sum of its
    /// children's floors plus the gaps; for a column, the widest of them. Computed only when a
    /// line actually overflows, so the common path never pays for it.
    /// </summary>
    private static float MinContentWidth(VisualNode node, LayoutContext ctx) => node switch
    {
        Text text => LongestWordWidth(text, ctx),
        Box box => box.Style.Width.Kind == SizeKind.Fixed
            ? box.Style.Width.Value
            : (box.Child is null ? 0 : MinContentWidth(box.Child, ctx)) + box.Style.Padding.Horizontal,
        Row row => RowMinContent(row, ctx),
        Column column => ColumnMinContent(column, ctx),
        Image image => image.Width,
        Icon icon => icon.Size,
        Vector vector => vector.Size,
        Drawing drawing => drawing.Width,
        // A canvas's floor is whatever it was told to be, or nothing when it fills.
        Canvas canvas => canvas.Width.Kind == SizeKind.Fixed ? canvas.Width.Value : 0,
        Spinner spinner => spinner.Size,
        CameraPreview camera => camera.Width,
        Spacer spacer => spacer.FixedLength,
        // Wrappers are transparent to the floor exactly as they are to layout.
        Pressable pressable => MinContentWidth(pressable.Child, ctx),
        CodeSurface surface => MinContentWidth(surface.Child, ctx),
        SheetSurface sheet => MinContentWidth(sheet.Child, ctx),
        Link link => MinContentWidth(link.Child, ctx),
        Adjustable adjustable => MinContentWidth(adjustable.Child, ctx),
        Hoverable hoverable => MinContentWidth(hoverable.Child, ctx),
        Simulated simulated => MinContentWidth(simulated.Child, ctx),
        InView inView => MinContentWidth(inView.Child, ctx),
        InFlow inFlow => MinContentWidth(inFlow.Child, ctx),
        Shortcut shortcut => MinContentWidth(shortcut.Child, ctx),
        Flexible flexible => MinContentWidth(flexible.Child, ctx),
        Presence presence => MinContentWidth(presence.Child, ctx),
        UiComponent component => MinContentWidth(component.BuildContained(ctx.Components), ctx),
        _ => 0,
    };

    private static float RowMinContent(Row row, LayoutContext ctx)
    {
        if (row.Width.Kind == SizeKind.Fixed) return row.Width.Value;
        var total = row.Padding.Horizontal + row.Gap * MathF.Max(0, row.Children.Count - 1);
        foreach (var child in row.Children) total += MinContentWidth(child, ctx);
        return total;
    }

    private static float ColumnMinContent(Column column, LayoutContext ctx)
    {
        if (column.Width.Kind == SizeKind.Fixed) return column.Width.Value;
        var widest = 0f;
        foreach (var child in column.Children)
            widest = MathF.Max(widest, MinContentWidth(child, ctx));
        return widest + column.Padding.Horizontal;
    }

    /// <summary>The widest single word — text wraps between words and never inside one.</summary>
    private static float LongestWordWidth(Text text, LayoutContext ctx)
    {
        var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
        if (text.Mono) style = style with { Mono = true };
        if (text.Italic) style = style with { Italic = true };
        var widest = 0f;
        foreach (var word in text.PlainContent.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            widest = MathF.Max(widest,
                ctx.Measurer.Measure(word, style, ctx.TypeScale, float.PositiveInfinity, 1).Width);
        return widest;
    }

    /// <summary>
    /// Whether a flex item gives up width when the line overflows. CSS shrinks every item by
    /// default; what it never shrinks is a size the AUTHOR pinned — so a Fixed extent, a Spacer
    /// and a Flexible (which is sized from leftover, not from content) all stay put.
    /// </summary>
    private static bool Shrinkable(VisualNode child) => child switch
    {
        Text => false,                       // already asked, above
        Spacer or Flexible => false,
        Box box => box.Style.Width.Kind != SizeKind.Fixed,
        FlexNode flex => flex.Width.Kind != SizeKind.Fixed,
        _ => true,
    };

    /// <summary>The inline-block boundary: BLOCK stretch stops here; FLEX stretch passes through.</summary>
    private static StretchKind Inline(StretchKind kind) => kind == StretchKind.Block ? StretchKind.None : kind;

    /// <summary>Re-arms the one-shot stretch flags and measures — for nodes that RESOLVE to a
    /// substitute (Adaptive) rather than wrapping a child.</summary>
    private static LayoutNode MeasureRearmed(VisualNode node, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW, StretchKind stretchH)
    {
        ctx.StretchWidth = stretchW;
        ctx.StretchHeight = stretchH;
        return Measure(node, maxW, maxH, ctx, path);
    }

    /// <summary>
    /// Measures an <see cref="InFlow"/> with the intent armed, so the overlay inside it builds its
    /// panel rather than its layer. Restored after — a sibling dialog opened for real must still
    /// take the viewport.
    /// </summary>
    private static LayoutNode MeasureInFlow(InFlow inFlow, float maxW, float maxH, LayoutContext ctx,
        string path, StretchKind stretchW, StretchKind stretchH)
    {
        var previous = InFlow.Current;
        InFlow.Current = true;
        try
        {
            return MeasureWrapper(inFlow, inFlow.Child, maxW, maxH, ctx, path, stretchW, stretchH);
        }
        finally
        {
            InFlow.Current = previous;
        }
    }

    private static LayoutNode MeasureWrapper(VisualNode node, VisualNode child, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var result = ctx.Node(node);
        // Layout-transparent means transparent to STRETCH too: whatever the parent would stretch,
        // it stretches through the wrapper — a Pressable around a tab cell (or the component node
        // around a page) must not eat the size the parent granted.
        ctx.StretchWidth = stretchW;
        ctx.StretchHeight = stretchH;
        var inner = Measure(child, maxW, maxH, ctx, ctx.ChildPath(path, 0));
        result.Children.Add(inner);
        result.Bounds = new Rect(0, 0, inner.Bounds.Width, inner.Bounds.Height);
        return result;
    }

    private static LayoutNode MeasureComponent(UiComponent component, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var resolved = ctx.Instances?.Reconcile(path, component) ?? component;
        // BuildContained, never Build: a throw here used to reach the host and cost the FRAME — the
        // window stops presenting and the app is gone, for one component's null reference.
        return MeasureWrapper(resolved, resolved.BuildContained(ctx.Components), maxW, maxH, ctx, path, stretchW, stretchH);
    }

    /// <summary>A transparent wrapper that also resolves the ENTRANCE progress against the host's
    /// presence clock, keyed by this stable path — the emit pass applies the paint-only effect and
    /// snapshots the subtree's commands by the same path (the exit replay source).</summary>
    private static LayoutNode MeasurePresence(Presence presence, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var result = MeasureWrapper(presence, presence.Child, maxW, maxH, ctx, path, stretchW, stretchH);
        result.Presence = ctx.Presences?.Progress(path, ctx.TimeMs, ctx.ReducedMotion) ?? 1f;
        result.PresencePath = path;
        return result;
    }

    /// <summary>A transparent wrapper that resolves the current DRAG offset against the host's drag
    /// clock (active follow or glide-back), keyed by this stable path — the emit pass paints the
    /// translate and registers the drag region the host routes input by.</summary>
    private static LayoutNode MeasureDragDismiss(DragDismiss drag, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var result = MeasureWrapper(drag, drag.Child, maxW, maxH, ctx, path, stretchW, stretchH);
        result.DragOffset = ctx.Drags?.Resolve(path, ctx.TimeMs) ?? 0f;
        result.DragPath = path;
        return result;
    }

    /// <summary>The live offset for this gesture — the finger while it is down, the glide after it
    /// lifts, and the caller's RestOffset when neither is happening.</summary>
    private static LayoutNode MeasureDraggable(Draggable draggable, float maxW, float maxH,
        LayoutContext ctx, string path, StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var result = MeasureWrapper(draggable, draggable.Child, maxW, maxH, ctx, path, stretchW, stretchH);
        // A gesture the caller paints itself never translates — its offset lives in the caller's
        // state and has already moved the subtree by the time this frame is measured.
        result.DragOffset = draggable.Follows
            ? ctx.Drags?.Resolve(path, ctx.TimeMs, draggable.RestOffset) ?? draggable.RestOffset
            : 0f;
        result.DragPath = path;
        return result;
    }

    /// <summary>Spec A3: sizes to the largest NON-positioned child (explicit Width/Height override);
    /// non-positioned children align by <see cref="Stack.Align"/>; Positioned children anchor to the
    /// resolved frame with signed offsets (unset axes fall back to the alignment). Paint order is
    /// child order — the LayoutNode children keep it.</summary>
    private static LayoutNode MeasureStack(Stack stack, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = ctx.Node(stack);
        var contentW = 0f;
        var contentH = 0f;

        for (var stackIndex = 0; stackIndex < stack.Children.Count; stackIndex++)
        {
            var child = stack.Children[stackIndex];
            var measured = Measure(child, maxW, maxH, ctx, ctx.ChildPath(path, stackIndex));
            result.Children.Add(measured);
            if (PositionedOf(child, measured) is not null) continue;
            contentW = MathF.Max(contentW, measured.Bounds.Width);
            contentH = MathF.Max(contentH, measured.Bounds.Height);
        }

        var width = ResolveSelf(stack.Width, maxW, contentW);
        var height = ResolveSelf(stack.Height, maxH, contentH);
        result.Bounds = new Rect(0, 0, width, height);

        for (var i = 0; i < stack.Children.Count; i++)
        {
            var child = stack.Children[i];
            var measured = result.Children[i];
            var cw = measured.Bounds.Width;
            var ch = measured.Bounds.Height;
            var (alignX, alignY) = AlignOffset(stack.Align, width - cw, height - ch);

            if (PositionedOf(child, measured) is { } positioned)
            {
                var x = positioned.Start ?? (positioned.End is { } end ? width - cw - end : alignX);
                var y = positioned.Top ?? (positioned.Bottom is { } bottom ? height - ch - bottom : alignY);
                measured.Bounds = measured.Bounds with { X = x, Y = y };
            }
            else
            {
                measured.Bounds = measured.Bounds with { X = alignX, Y = alignY };
            }
        }

        // Spec S7 z-order: children paint (and hit-test, topmost-last) in Layer order — a stable
        // sort keeps declaration order for equal values (flow order = the painter's default).
        if (result.Children.Where((node, i) => PositionedOf(stack.Children[i], node) is { Layer: not 0 }).Any())
        {
            var ordered = result.Children
                .Select((node, i) => (Node: node,
                    Z: PositionedOf(stack.Children[i], node) is { } p ? p.Layer : 0, I: i))
                .OrderBy(e => e.Z).ThenBy(e => e.I)
                .Select(e => e.Node)
                .ToList();
            result.Children.Clear();
            result.Children.AddRange(ordered);
        }

        return result;
    }

    /// <summary>Spec A6: the child lays out UNBOUNDED on the scroll axis (bounded content measures its
    /// natural extent) and is offset by the programmatic scroll position; the viewport itself resolves
    /// explicit &gt; Fill &gt; hug-the-child (capped by the available space). Clipping happens at the
    /// realizer via the engine clip primitive.</summary>
    /// <summary>
    /// The <see cref="Positioned"/> a Stack child resolves to, THROUGH any components in between.
    /// <para>
    /// `Positioned` is a contract with the parent — like a flex weight, it means nothing anywhere
    /// else — and asking `child is Positioned` missed the moment one came out of a component. The
    /// child then took the ordinary aligned path: a corner button laid out at the stack's origin
    /// instead of its corner, silently, on both targets.
    /// </para>
    /// <para>
    /// Read from the MEASURED tree rather than by rebuilding: the build already happened, through
    /// the instance store, and building a second time would hand back a different instance.
    /// </para>
    /// </summary>
    private static Positioned? PositionedOf(VisualNode child, LayoutNode measured)
    {
        if (child is Positioned direct) return direct;

        var node = measured;
        // Bounded: a component wrapping a component wrapping one is ordinary; a cycle is not.
        for (var hops = 0; hops < 8 && node.Source is UiComponent && node.Children.Count == 1; hops++)
        {
            node = node.Children[0];
            if (node.Source is Positioned found) return found;
        }
        return null;
    }

    private static LayoutNode MeasureScrollView(ScrollView scroll, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = ctx.Node(scroll);
        var horizontal = scroll.Axis == ScrollAxis.Horizontal;

        var child = Measure(scroll.Child,
            horizontal ? float.PositiveInfinity : maxW,
            horizontal ? maxH : float.PositiveInfinity, ctx, ctx.ChildPath(path, 0));
        result.Children.Add(child);

        var width = ResolveSelf(scroll.Width, maxW, MathF.Min(child.Bounds.Width, maxW));
        var height = ResolveSelf(scroll.Height, maxH, MathF.Min(child.Bounds.Height, maxH));
        result.Bounds = new Rect(0, 0, width, height);

        var maxOffset = MathF.Max(0, horizontal ? child.Bounds.Width - width : child.Bounds.Height - height);
        // Scroll compositor v1: the host's stored offset wins; the node's programmatic Offset is the
        // default until the user scrolls. The realizer registers the region via ScrollMeta.
        var offset = Math.Clamp(ctx.ScrollOffsets?.Get(path) ?? scroll.Offset, 0, maxOffset);
        ctx.ScrollMeta?.TryAdd(scroll, (path, maxOffset));
        child.Bounds = child.Bounds with
        {
            X = horizontal ? -offset : 0,
            Y = horizontal ? 0 : -offset,
        };

        // Spec S7 — Pinned PINNING (vertical v1): a Pinned at content-y y0 shows at y0 - offset;
        // once that would pass its own Offset from the viewport start, it pins there instead.
        if (!horizontal && offset > 0)
            PinSticky(child, accumulatedY: child.Bounds.Y);

        return result;
    }

    /// <summary>Walks the scrolled content for Pinned wrappers and clamps their viewport-relative Y
    /// (v1: vertical, no end-of-container release — that fence joins the compositor polish). Nested
    /// ScrollViews own their own pinning pass.</summary>
    private static void PinSticky(LayoutNode node, float accumulatedY)
    {
        foreach (var child in node.Children)
        {
            if (child.Source is ScrollView) continue;
            var viewportY = accumulatedY + child.Bounds.Y;
            if (child.Source is Pinned pinned && viewportY < pinned.Offset)
            {
                child.Bounds = child.Bounds with { Y = child.Bounds.Y + (pinned.Offset - viewportY) };
                continue; // the pinned subtree moves as one — no need to descend
            }
            PinSticky(child, accumulatedY + child.Bounds.Y);
        }
    }

    private static (float X, float Y) AlignOffset(Alignment align, float slackW, float slackH)
    {
        var x = ((int)align % 3) switch { 1 => slackW / 2, 2 => slackW, _ => 0f };
        var y = ((int)align / 3) switch { 1 => slackH / 2, 2 => slackH, _ => 0f };
        return (x, y);
    }

    private static LayoutNode MeasureText(Text text, float maxW, LayoutContext ctx)
    {
        var result = ctx.Node(text);
        var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
        if (text.Mono) style = style with { Mono = true };
        if (text.Italic) style = style with { Italic = true };
        if (text.Spans is { Count: > 0 } spans) return MeasureRuns(result, text, spans, style, maxW, ctx);
        var measurement = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, maxW, text.MaxLines);
        result.Text = measurement;
        result.Bounds = new Rect(0, 0, measurement.Width, measurement.Height);
        return result;
    }

    /// <summary>
    /// A RICH paragraph: the runs are laid out as one flowing line of text, breaking between WORDS
    /// and never between runs. That distinction is the whole reason runs exist — a Row of Texts
    /// breaks at the run boundaries, so a sentence with three code spans wraps at the spans.
    /// <para>
    /// Each word is measured in its OWN run's style, because that is what decides its advance: bold
    /// is wider, mono is wider still, and a paragraph measured in the base style and drawn in five
    /// overflows its box by however much the emphasis added.
    /// </para>
    /// <para>
    /// The line BOX stays the paragraph's throughout, so a smaller inline code span does not reopen
    /// the leading — the same rule the web's `font-size`-without-`line-height` follows.
    /// </para>
    /// </summary>
    private static LayoutNode MeasureRuns(LayoutNode result, Text text, IReadOnlyList<TextRun> spans,
        TypeStyle paragraph, float maxW, LayoutContext ctx)
    {
        var lineHeight = paragraph.ScaledLineHeight(ctx.TypeScale);
        var limit = float.IsPositiveInfinity(maxW) || maxW <= 0 ? float.PositiveInfinity : maxW;
        var fragments = new List<TextFragment>();
        var lines = new List<MeasuredLine>();

        float x = 0;
        var line = 0;
        float widest = 0;

        foreach (var run in spans)
        {
            var runStyle = paragraph;
            if (run.StyleOverride is { } over) runStyle = runStyle.WithSize(over.Size);
            if (run.Mono) runStyle = runStyle with { Mono = true };
            if (run.Italic) runStyle = runStyle with { Italic = true };
            if (run.Weight is { } weight) runStyle = runStyle with { Weight = weight };

            foreach (var word in Words(run.Content))
            {
                // A space that lands at a break is DROPPED rather than carried to the next line,
                // which is what keeps a wrapped paragraph's left edge straight.
                var width = ctx.Measurer.Measure(word, runStyle, ctx.TypeScale, float.PositiveInfinity, 1).Width;
                if (x > 0 && x + width > limit && word != " ")
                {
                    lines.Add(new MeasuredLine(x, false));
                    if (x > widest) widest = x;
                    line++;
                    x = 0;
                }
                if (x == 0 && word == " ") continue;

                fragments.Add(new TextFragment(word, runStyle, x, line * lineHeight, width,
                    run.Color, run.Destination is { Length: > 0 } ? run.Destination : null));
                x += width;
            }
        }

        lines.Add(new MeasuredLine(x, false));
        if (x > widest) widest = x;

        result.TextRuns = fragments;
        result.Text = new TextMeasurement(widest, lines.Count * lineHeight, lineHeight, lines);
        result.Bounds = new Rect(0, 0, widest, lines.Count * lineHeight);
        return result;
    }

    /// <summary>
    /// A run split into the pieces a line can break between: words, with each separating space as a
    /// piece of its own so the break can drop it. Deliberately not a tokenizer — the subset that
    /// matters is "space breaks, everything else does not", the same rule the measurers use.
    /// </summary>
    private static List<string> Words(string content)
    {
        var pieces = new List<string>();
        var start = 0;
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != ' ') continue;
            if (i > start) pieces.Add(content[start..i]);
            pieces.Add(" ");
            start = i + 1;
        }
        if (start < content.Length) pieces.Add(content[start..]);
        return pieces;
    }

    /// <summary>A text entry is <see cref="TextEntry.Lines"/> lines of its role (1 by default),
    /// filling the available width (the field's editable area) — height from the type scale so forms
    /// lay out identically before and after the real caret/IME land (spec B9's fixed contract). A
    /// multi-line field is exactly that many lines TALL whatever it currently holds: its box must not
    /// grow and shrink as the user types.</summary>
    private static LayoutNode MeasureTextEntry(TextEntry entry, float maxW, LayoutContext ctx)
    {
        var result = ctx.Node(entry);
        var style = ctx.Theme.Type(entry.Role);
        var shown = entry.Value.Length > 0 ? entry.Value : entry.Placeholder ?? string.Empty;
        var lines = Math.Max(1, entry.Lines);
        var measurement = ctx.Measurer.Measure(shown, style, ctx.TypeScale, maxW, maxLines: lines);
        result.Text = measurement;
        var width = float.IsFinite(maxW) ? maxW : measurement.Width;
        var height = lines == 1 ? measurement.Height : measurement.LineHeight * lines;
        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }

    private static LayoutNode MeasureBox(Box box, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        var result = ctx.Node(box);
        var style = box.Style;

        // Spec S6, the Size channel: a FIXED width or height that changed glides to its new value
        // under the box's own Transition — measured at the interpolated size, so everything that
        // depends on it (the child's wrap, the siblings' share of the row) follows the glide too,
        // which is what `transition: width` does in a browser. Hug/Fill have no number to glide.
        if (ctx.Transitions is { } glide
            && TransitionStore.Only(style.Transition, StyleChannels.Size) is { } sizeSpec)
        {
            if (style.Width.Kind == SizeKind.Fixed)
                style = style with { Width = glide.Resolve(path + ":w", style.Width.Value, ctx.TimeMs, sizeSpec, ctx.ReducedMotion) };
            if (style.Height.Kind == SizeKind.Fixed)
                style = style with { Height = glide.Resolve(path + ":h", style.Height.Value, ctx.TimeMs, sizeSpec, ctx.ReducedMotion) };
        }

        // The indeterminate flags AS INHERITED — what the PARENT said about this axis, before this
        // box restates them for its own child below. A Fill on an axis the parent is sizing from
        // content has nothing to fill; the flex container has honoured that from the start, but a
        // BOX child read its available maximum anyway — which is how every option row of a hugging
        // Select panel came out the full width of the window.
        var inheritedIndeterminateW = ctx.IndeterminateWidth;
        var inheritedIndeterminateH = ctx.IndeterminateHeight;

        var maxWidthDp = CapDp(style.MaxWidth, ctx.WindowWidth);
        var maxHeightDp = CapDp(style.MaxHeight, ctx.WindowHeight);
        var selfMaxW = CapMax(maxW, maxWidthDp);
        var selfMaxH = CapMax(maxH, maxHeightDp);

        // Content box the child may use (explicit/Fill pin it; Hug passes the available through).
        var childMaxW = (style.Width.Kind == SizeKind.WindowMinus
            ? WindowSize(style.Width, ctx.WindowWidth)
            : ResolveForChild(style.Width, selfMaxW)) - style.Padding.Horizontal;
        var childMaxH = (style.Height.Kind == SizeKind.WindowMinus
            ? WindowSize(style.Height, ctx.WindowHeight)
            : ResolveForChild(style.Height, selfMaxH)) - style.Padding.Vertical;

        LayoutNode? child = null;
        if (box.Child is not null)
        {
            // The available space still bounds the child (text has to wrap somewhere), but on an
            // axis this box HUGS there is nothing to fill: say so, and a Fill child measures itself.
            var outerW = ctx.IndeterminateWidth;
            var outerH = ctx.IndeterminateHeight;
            // What the CHILD may fill against: Fixed decides the axis, Hug decides nothing — and
            // FILL passes the question through, because a Fill in a context that hands out no
            // width hands out none either. Treating Fill as determinate was the leak: an option
            // row's Fill box told ITS Fill row the width was known, the row took the viewport's
            // maximum, and every hugging ancestor up to the Select's panel followed it — one
            // dropdown as wide as the window.
            // A STRETCHED auto axis is determined after all: the parent already decided the size
            // this box will take (ResolveSelf below returns the available maximum), so the child
            // may fill it — this is what lets a page whose root box hugs still hand the window's
            // width to a Fill child, the way a CSS body does.
            ctx.IndeterminateWidth = style.Width.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchW != StretchKind.None && !inheritedIndeterminateW && !float.IsPositiveInfinity(selfMaxW)),
                _ => inheritedIndeterminateW,
            };
            ctx.IndeterminateHeight = style.Height.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchH != StretchKind.None && !inheritedIndeterminateH && !float.IsPositiveInfinity(selfMaxH)),
                _ => inheritedIndeterminateH,
            };
            // CSS block semantics on the WIDTH: a box whose width is determined stretches an
            // auto-sized child across it — a div's child div is full-width without asking. This is
            // what keeps a hugging Column inside a Fill card at the card's width on native, the way
            // the SAME tree already behaves on the web, where a Column realizes as width:auto.
            if (!ctx.IndeterminateWidth) ctx.StretchWidth = StretchKind.Block;
            // And on the HEIGHT, where CSS gives nothing and the author paid for it: a box with a
            // decided height hands that height to its child. A 56dp bar holding a Row got a Row as
            // tall as its tallest label, so `Cross = Center` centred inside THAT — and the toolbar
            // sat against the top of its own bar. The fix used to be `Height = Fill` on the child,
            // written by hand, in every bar, remembered every time.
            //
            // It reaches exactly what an auto-sized CONTAINER is: a Box, a Row, a Column. Text,
            // images and icons never took these flags (they size themselves), and a button, a link
            // or an input hugs, because a Block stretch stops at an inline-block — the same fence
            // the width has always respected.
            if (!ctx.IndeterminateHeight) ctx.StretchHeight = StretchKind.Block;
            child = Measure(box.Child, MathF.Max(0, childMaxW), MathF.Max(0, childMaxH), ctx, ctx.ChildPath(path, 0));
            ctx.IndeterminateWidth = outerW;
            ctx.IndeterminateHeight = outerH;
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Children.Add(child);
        }

        var width = ResolveSelf(style.Width, selfMaxW, (child?.Bounds.Width ?? 0) + style.Padding.Horizontal, ctx.WindowWidth,
            indeterminate: inheritedIndeterminateW, stretched: stretchW != StretchKind.None);
        var height = ResolveSelf(style.Height, selfMaxH, (child?.Bounds.Height ?? 0) + style.Padding.Vertical, ctx.WindowHeight,
            indeterminate: inheritedIndeterminateH, stretched: stretchH != StretchKind.None);
        width = Clamp(width, style.MinWidth, maxWidthDp);
        height = Clamp(height, style.MinHeight, maxHeightDp);

        // Min/Max REFLOW (the CSS twin): when the clamp changed the box's extent after the child
        // was already measured, the child measured against a lie. Re-measure it against the final
        // size, which is now DETERMINATE on that axis — this is what lets a MatchAnchorWidth panel
        // hand its MinWidth down to the option rows instead of keeping them at their intrinsic
        // width inside a wider frame. Runs only when a Min/Max actually bit, so the common path
        // stays single-pass.
        if (child is not null
            && ((style.MinWidth > 0 || maxWidthDp >= 0)
                && MathF.Abs(width - style.Padding.Horizontal - child.Bounds.Width) > 0.5f
                || (style.MinHeight > 0 || maxHeightDp >= 0)
                && MathF.Abs(height - style.Padding.Vertical - child.Bounds.Height) > 0.5f))
        {
            var outerW2 = ctx.IndeterminateWidth;
            var outerH2 = ctx.IndeterminateHeight;
            ctx.IndeterminateWidth = false;
            ctx.IndeterminateHeight = style.Height.Kind == SizeKind.Hug
                && style.MinHeight <= 0 && maxHeightDp < 0 && outerH2;
            // The clamped width is a DECIDED size — block semantics again: the child stretches
            // across it, which is what centres a Stepper's reading inside its MinWidth cell.
            ctx.StretchWidth = StretchKind.Block;
            result.Children.Clear();
            child = Measure(box.Child!, MathF.Max(0, width - style.Padding.Horizontal),
                MathF.Max(0, height - style.Padding.Vertical), ctx, ctx.ChildPath(path, 0));
            ctx.IndeterminateWidth = outerW2;
            ctx.IndeterminateHeight = outerH2;
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Children.Add(child);
        }

        // Spec S1 aspect-ratio (CSS twin): when exactly one axis is author-determined, the other
        // derives from it; two explicit axes win over the ratio (no constraint fight).
        if (style.AspectRatio > 0)
        {
            var widthSet = style.Width.Kind != SizeKind.Hug;
            var heightSet = style.Height.Kind != SizeKind.Hug;
            if (widthSet && !heightSet)
                height = Clamp(width / style.AspectRatio, style.MinHeight, maxHeightDp);
            else if (heightSet && !widthSet)
                width = Clamp(height * style.AspectRatio, style.MinWidth, maxWidthDp);
        }

        // A Fill child stretches to the resolved content box (its own measurement saw the max already;
        // pin the bounds so realizers paint the full extent).
        if (child?.Source is Box { Style.Width.Kind: SizeKind.Fill })
            child.Bounds = child.Bounds with { Width = MathF.Max(0, width - style.Padding.Horizontal) };
        if (child?.Source is Box { Style.Height.Kind: SizeKind.Fill })
            child.Bounds = child.Bounds with { Height = MathF.Max(0, height - style.Padding.Vertical) };

        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }

    // ---- flex ------------------------------------------------------------------------------------

    private static LayoutNode MeasureFlex(FlexNode flex, float maxW, float maxH, LayoutContext ctx, string path,
        StretchKind stretchW = StretchKind.None, StretchKind stretchH = StretchKind.None)
    {
        if (flex.Wrap) return MeasureFlexWrapped(flex, maxW, maxH, ctx, path);

        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (maxW, maxH) : (maxH, maxW);
        var mainSize = horizontal ? flex.Width : flex.Height;
        var crossSize = horizontal ? flex.Height : flex.Width;
        var padMain = horizontal ? flex.Padding.Horizontal : flex.Padding.Vertical;
        var padCross = horizontal ? flex.Padding.Vertical : flex.Padding.Horizontal;

        var mainAvail = mainSize.Kind == SizeKind.Fixed ? mainSize.Value - padMain
            : !float.IsPositiveInfinity(mainMax) ? mainMax - padMain
            : float.PositiveInfinity;

        // "Leftover" for Flexible/Spacer children exists when the main extent is pinned (Fixed, or Fill
        // in bounded space) — AND when a Hug container holding flexible children sits in FINITE space:
        // flexibles declare the intent to fill, so the container takes the available extent (CSS parity —
        // a stretched row with a flex-grow child distributes over the stretched width). Flexibles
        // collapse to 0 only in genuinely unbounded space (e.g. inside scroll content), and Spacers
        // additionally "lose to content" when space is tight (leftover floors at 0).
        var hasFlexibles = false;
        foreach (var c in flex.Children)
            if (c is Flexible or Spacer { Flex: > 0 }) { hasFlexibles = true; break; }
        // On an INDETERMINATE main axis the available maximum is not a size anyone granted — it is
        // the measuring parent's upper bound. Distributing leftover against it made a Flexible
        // spacer swallow the viewport: an option row's Fill (inherited-indeterminate) inside a
        // hugging Select panel measured its spacer against 1180dp of "available" and dragged every
        // hugging ancestor to the width of the window.
        var mainIndeterminateNow = horizontal ? ctx.IndeterminateWidth : ctx.IndeterminateHeight;
        var mainBounded = mainSize.Kind == SizeKind.Fixed
                          || (!float.IsPositiveInfinity(mainAvail)
                              && !mainIndeterminateNow
                              && (mainSize.Kind == SizeKind.Fill || hasFlexibles));

        var crossAvail = crossSize.Kind == SizeKind.Fixed ? crossSize.Value - padCross
            : !float.IsPositiveInfinity(crossMax) ? crossMax - padCross
            : float.PositiveInfinity;

        // The flags this container hands its CHILDREN — the same restatement MeasureBox makes, on
        // flex axes: Fixed decides the axis, Hug decides nothing (unless the parent stretched this
        // container, which makes the auto size a real one), and Fill passes the question through.
        // Without this a row with a FIXED width told its children nothing (they inherited whatever
        // the context said), and a hugging row told them the viewport was theirs to fill.
        var crossIndeterminateNow = horizontal ? ctx.IndeterminateHeight : ctx.IndeterminateWidth;
        var mainStretchedIn = horizontal ? stretchW : stretchH;
        var crossStretchedIn = horizontal ? stretchH : stretchW;
        var childIndetMain = mainSize.Kind switch
        {
            SizeKind.Fixed => false,
            SizeKind.Hug => !(mainStretchedIn != StretchKind.None && !mainIndeterminateNow && !float.IsPositiveInfinity(mainMax)),
            _ => mainIndeterminateNow,
        };
        var childIndetCross = crossSize.Kind switch
        {
            SizeKind.Fixed => false,
            SizeKind.Hug => !(crossStretchedIn != StretchKind.None && !crossIndeterminateNow && !float.IsPositiveInfinity(crossMax)),
            _ => crossIndeterminateNow,
        };
        var (childIndetW, childIndetH) = horizontal ? (childIndetMain, childIndetCross) : (childIndetCross, childIndetMain);

        // Whether THIS container decides the child's cross size for it. Only when the container's
        // own cross extent is known: a hugging container has nothing to hand out, and CSS agrees —
        // stretch there means "as wide as the widest sibling", which is a second pass, not a size.
        bool StretchesCross(VisualNode child)
        {
            if (child is Text) return false;                       // text sizes itself
            if ((child.AlignSelf ?? flex.Cross) != CrossAlign.Stretch) return false;
            if (float.IsPositiveInfinity(crossAvail)) return false;
            return !childIndetCross;
        }

        void MarkStretch(VisualNode child)
        {
            if (!StretchesCross(child)) return;
            if (horizontal) ctx.StretchHeight = StretchKind.Flex;
            else ctx.StretchWidth = StretchKind.Flex;
        }

        // Every child measures under the flags THIS container restated. A flexible's share is a
        // size the container genuinely granted, so the main axis is determinate inside the slot
        // regardless of how the container itself is sized.
        LayoutNode MeasureChild(VisualNode child, float w, float h, string childPath, bool mainGranted = false)
        {
            var outerW = ctx.IndeterminateWidth;
            var outerH = ctx.IndeterminateHeight;
            ctx.IndeterminateWidth = childIndetW && !(mainGranted && horizontal);
            ctx.IndeterminateHeight = childIndetH && !(mainGranted && !horizontal);
            var node = Measure(child, w, h, ctx, childPath);
            ctx.IndeterminateWidth = outerW;
            ctx.IndeterminateHeight = outerH;
            return node;
        }

        var children = flex.Children;
        var laid = new LayoutNode?[children.Count];
        var mains = new float[children.Count];
        var flexWeights = new float[children.Count];
        var gapTotal = flex.Gap * MathF.Max(0, children.Count - 1);

        // Pass 1 — rigid children (flexibles deferred; text measured at full availability first).
        var rigidSum = 0f;
        for (var i = 0; i < children.Count; i++)
        {
            switch (children[i])
            {
                case Flexible f:
                    // Spec B14: an AnimateChanges weight LAYS OUT at the animator's interpolated
                    // value — forward changes glide over Motion.Base, everything else snaps.
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i), f.Flex, ctx.TimeMs,
                        f.AnimateChanges, ctx.ReducedMotion) ?? f.Flex;
                    continue;
                case Spacer { Flex: > 0 } s:
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i), s.Flex, ctx.TimeMs,
                        s.AnimateChanges, ctx.ReducedMotion) ?? s.Flex;
                    continue;
                case Spacer fixedSpacer:
                    mains[i] = fixedSpacer.FixedLength;
                    rigidSum += mains[i];
                    continue;
            }

            var childMaxW = horizontal ? mainAvail : crossAvail;
            var childMaxH = horizontal ? crossAvail : mainAvail;
            MarkStretch(children[i]);
            var child = MeasureChild(children[i], childMaxW, childMaxH, ctx.ChildPath(path, i));
            laid[i] = child;
            mains[i] = horizontal ? child.Bounds.Width : child.Bounds.Height;
            rigidSum += mains[i];
        }

        // Truncation contract (spec A2): on overflow, TEXT children shrink to ellipsis before any
        // sibling is pushed out; fixed children never shrink. Applies whenever the available extent is
        // finite — a Hug row inside a bounded parent must not overflow it either.
        if (!float.IsPositiveInfinity(mainAvail) && rigidSum + gapTotal > mainAvail && horizontal)
        {
            var deficit = rigidSum + gapTotal - mainAvail;
            var textTotal = 0f;
            for (var i = 0; i < children.Count; i++)
                if (children[i] is Text) textTotal += mains[i];

            if (textTotal > 0)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is not Text text) continue;
                    var reduced = MathF.Max(0, mains[i] - deficit * (mains[i] / textTotal));
                    var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
                    if (text.Mono) style = style with { Mono = true };
                    if (text.Italic) style = style with { Italic = true };
                    var remeasured = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, reduced,
                        Math.Max(1, text.MaxLines));
                    var node = ctx.Node(text);
                    node.Text = remeasured;
                    node.Bounds = new Rect(0, 0, remeasured.Width, remeasured.Height);
                    laid[i] = node;
                    rigidSum -= mains[i] - (horizontal ? node.Bounds.Width : node.Bounds.Height);
                    mains[i] = horizontal ? node.Bounds.Width : node.Bounds.Height;
                }
            }

            // FLEX-SHRINK (the CSS twin, and the rest of the same contract): text is asked first
            // because ellipsis is the cheapest loss, but if the line STILL does not fit, every
            // item that is not pinned gives up a proportional share and re-measures inside it.
            // Without this a row of auto-sized cells simply ran off the right edge and the clip
            // ate it — silently, because a clipped press region cannot be pressed either.
            deficit = rigidSum + gapTotal - mainAvail;
            if (deficit > 0.5f)
            {
                var shrinkTotal = 0f;
                for (var i = 0; i < children.Count; i++)
                    if (Shrinkable(children[i])) shrinkTotal += mains[i];

                if (shrinkTotal > 0)
                {
                    // Two passes, because a floor one item refuses to cross is width the OTHERS
                    // have to give: pass 1 finds how much is really available to take, pass 2
                    // takes it. This is what makes a hugging button keep its word while the
                    // stretchy one beside it absorbs the overflow, exactly as a browser does.
                    var floors = new float[children.Count];
                    var yielding = 0f;
                    for (var i = 0; i < children.Count; i++)
                    {
                        if (!Shrinkable(children[i])) continue;
                        floors[i] = MathF.Min(mains[i], MinContentWidth(children[i], ctx));
                        yielding += mains[i] - floors[i];
                    }

                    var taking = MathF.Min(deficit, yielding);
                    if (taking > 0)
                    {
                        for (var i = 0; i < children.Count; i++)
                        {
                            if (!Shrinkable(children[i])) continue;
                            var room = mains[i] - floors[i];
                            if (room <= 0) continue;
                            var bound = MathF.Max(floors[i], mains[i] - taking * (room / yielding));
                            var childMaxW2 = horizontal ? bound : crossAvail;
                            var childMaxH2 = horizontal ? crossAvail : bound;
                            MarkStretch(children[i]);
                            var reflowed = MeasureChild(children[i], childMaxW2, childMaxH2, ctx.ChildPath(path, i));
                            laid[i] = reflowed;
                            var shrunk = horizontal ? reflowed.Bounds.Width : reflowed.Bounds.Height;
                            rigidSum -= mains[i] - shrunk;
                            mains[i] = shrunk;
                        }
                    }
                }
            }
        }

        // Pass 2 — distribute leftover to flexible children by weight.
        var flexTotal = 0f;
        foreach (var w in flexWeights) flexTotal += w;
        var leftover = mainBounded ? MathF.Max(0, mainAvail - rigidSum - gapTotal) : 0f;

        for (var i = 0; i < children.Count; i++)
        {
            if (flexWeights[i] == 0) continue;

            // Max-content sizing (the CSS twin): when there is no leftover to distribute — the
            // main axis is indeterminate or unbounded — a Flexible with CONTENT contributes its
            // own intrinsic size, exactly what a flex-grow item contributes to a max-content
            // container. A share of 0 here erased real content: a drawer's list rows measured
            // 0x0 because their row was hugging. (A flexible SPACER stays 0 — pure space.)
            if (!mainBounded && children[i] is Flexible unbounded)
            {
                MarkStretch(unbounded);
                var intrinsic = MeasureChild(unbounded.Child, horizontal ? mainAvail : crossAvail,
                    horizontal ? crossAvail : mainAvail, ctx.ChildPath(ctx.ChildPath(path, i), 0));
                mains[i] = horizontal ? intrinsic.Bounds.Width : intrinsic.Bounds.Height;
                rigidSum += mains[i];
                var grown = ctx.Node(unbounded, intrinsic.Bounds);
                grown.Children.Add(intrinsic);
                laid[i] = grown;
                continue;
            }

            var share = flexTotal > 0 ? leftover * flexWeights[i] / flexTotal : 0f;
            mains[i] = share;

            if (children[i] is Flexible flexible)
            {
                var childMaxW = horizontal ? share : crossAvail;
                var childMaxH = horizontal ? crossAvail : share;
                // A Flexible is layout-transparent: whatever the container would stretch, it
                // stretches THROUGH it. Without this the wrapper grew to the cell and the content
                // inside it stayed at its own width, which is exactly what the tab labels did.
                MarkStretch(flexible);
                // The share IS the slot's main size (the bounds are pinned to it below), so the
                // child is stretched on the main axis too: an auto-sized cell takes the share and
                // lays out inside it — a flex-grow item's autos fill the cell, per CSS.
                if (horizontal) ctx.StretchWidth = StretchKind.Flex;
                else ctx.StretchHeight = StretchKind.Flex;
                var child = MeasureChild(flexible.Child, childMaxW, childMaxH, ctx.ChildPath(ctx.ChildPath(path, i), 0),
                    mainGranted: true);
                // The flexible slot IS the share on the main axis (the child fills it).
                child.Bounds = horizontal
                    ? child.Bounds with { Width = share }
                    : child.Bounds with { Height = share };
                var wrapper = ctx.Node(flexible, child.Bounds);
                wrapper.Children.Add(child);
                laid[i] = wrapper;
            }
            else
            {
                laid[i] = ctx.Node(children[i]); // flexible Spacer: pure space
            }
        }

        // Container size. Fill on an axis the PARENT is sizing from its content has nothing to fill
        // — see LayoutContext.IndeterminateWidth. Without this a `Centered()` wrapper (Fill on both
        // axes, which is what makes centring possible at all) dragged its hugging parent to the
        // full width of the window: a 16dp badge came out 600dp wide, shoving its neighbours off.
        var mainIndeterminate = horizontal ? ctx.IndeterminateWidth : ctx.IndeterminateHeight;
        var crossIndeterminate = horizontal ? ctx.IndeterminateHeight : ctx.IndeterminateWidth;

        var mainStretched = horizontal ? stretchW : stretchH;
        var crossStretched = horizontal ? stretchH : stretchW;

        var contentMain = rigidSum + (flexTotal > 0 ? leftover : 0) + gapTotal;
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !mainIndeterminate && !float.IsPositiveInfinity(mainMax) => mainMax,
            // Stretched by the parent: the auto size IS the size it was given, and only then does
            // MainAlign have room to place anything — this is what makes a centred label centre.
            SizeKind.Hug when mainStretched != StretchKind.None && !mainIndeterminate && !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };

        var crossContent = 0f;
        // Indexed to children.Count: the rented array is LONGER than the child list, and the slots
        // past it belong to whoever borrowed it last.
        for (var ci = 0; ci < children.Count; ci++)
            if (laid[ci] is { } laidChild)
                crossContent = MathF.Max(crossContent, horizontal ? laidChild.Bounds.Height : laidChild.Bounds.Width);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !crossIndeterminate && !float.IsPositiveInfinity(crossMax) => crossMax,
            SizeKind.Hug when crossStretched != StretchKind.None && !crossIndeterminate && !float.IsPositiveInfinity(crossMax) => crossMax,
            _ => crossContent + padCross,
        };

        // Pass 3 — arrange along main (alignment applies when no flexible consumed the leftover).
        var free = MathF.Max(0, (main - padMain) - contentMain);
        var cursor = (horizontal ? flex.Padding.Start : flex.Padding.Top) + flex.Main switch
        {
            MainAlign.Center => free / 2,
            MainAlign.End => free,
            _ => 0,
        };
        var betweenExtra = flex.Main == MainAlign.SpaceBetween && children.Count > 1 ? free / (children.Count - 1) : 0;

        var crossExtent = cross - padCross;
        for (var i = 0; i < children.Count; i++)
        {
            var child = laid[i];
            if (child is null)
            {
                // Pure space (Spacer): no layout node, but its extent still advances the cursor.
                cursor += mains[i] + flex.Gap + betweenExtra;
                continue;
            }

            var childCross = horizontal ? child.Bounds.Height : child.Bounds.Width;
            // Spec S1 align-self: a child may override the container's Cross for itself (CSS twin).
            var alignment = children[i].AlignSelf ?? flex.Cross;
            var crossPos = (horizontal ? flex.Padding.Top : flex.Padding.Start) + alignment switch
            {
                CrossAlign.Center => (crossExtent - childCross) / 2,
                CrossAlign.End => crossExtent - childCross,
                _ => 0,
            };
            if (alignment == CrossAlign.Stretch && children[i] is not Text
                && CrossSizeKind(children[i], horizontal) != SizeKind.Fixed)
            {
                // CSS parity: stretch fills AUTO cross sizes only — an explicit cross size is kept.
                childCross = crossExtent;
                child.Bounds = horizontal
                    ? child.Bounds with { Height = crossExtent }
                    : child.Bounds with { Width = crossExtent };
            }

            child.Bounds = horizontal
                ? child.Bounds with { X = cursor, Y = crossPos }
                : child.Bounds with { X = crossPos, Y = cursor };
            result.Children.Add(child);
            cursor += mains[i] + flex.Gap + betweenExtra;
        }

        result.Bounds = horizontal ? new Rect(0, 0, main, cross) : new Rect(0, 0, cross, main);
        return result;
    }

    /// <summary>
    /// Spec S3 — the wrapping flex pass (CSS flex-wrap twin, v1 scope): children measure at their
    /// NATURAL size and break onto a new line when the next one would overflow the main extent.
    /// Each line arranges with the container's <see cref="FlexNode.Main"/>; within its line a child
    /// follows <see cref="FlexNode.Cross"/> (or its own AlignSelf); lines stack with RunGap.
    /// </summary>
    private static LayoutNode MeasureFlexWrapped(FlexNode flex, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (maxW, maxH) : (maxH, maxW);
        var mainSize = horizontal ? flex.Width : flex.Height;
        var crossSize = horizontal ? flex.Height : flex.Width;
        var padMain = horizontal ? flex.Padding.Horizontal : flex.Padding.Vertical;
        var padCross = horizontal ? flex.Padding.Vertical : flex.Padding.Horizontal;
        var runGap = flex.RunGap ?? flex.Gap;

        var mainAvail = mainSize.Kind == SizeKind.Fixed ? mainSize.Value - padMain
            : !float.IsPositiveInfinity(mainMax) ? mainMax - padMain
            : float.PositiveInfinity;

        // Measure every child at its HYPOTHETICAL main size — its basis when it declares one, its
        // natural size otherwise. This is the number the line breaker works from, exactly as CSS
        // does: a pane with a basis of 440 asks for 440 whatever its content happens to measure, so
        // two of them share a line while there is room for both and take a line each when there is
        // not. A basis of 0 (the default) reproduces the old behaviour, where a Flexible simply
        // degraded to its child.
        var measured = new List<LayoutNode>(flex.Children.Count);
        var sources = new List<VisualNode>(flex.Children.Count);
        var hypothetical = new List<float>(flex.Children.Count);
        var grow = new List<int>(flex.Children.Count);
        var shrink = new List<int>(flex.Children.Count);
        for (var i = 0; i < flex.Children.Count; i++)
        {
            var source = flex.Children[i];
            var flexible = source as Flexible;
            var child = flexible?.Child ?? source;
            if (child is Spacer) continue;

            // A declared basis also BOUNDS the measure, so text inside wraps at the width the item
            // is going to get rather than at the whole line's.
            var basis = flexible is { Basis: > 0 } ? flexible.Basis : 0f;
            var constraint = basis > 0 ? MathF.Min(basis, mainAvail) : mainAvail;
            var node = Measure(child, constraint, crossMax - padCross, ctx, ctx.ChildPath(path, i));

            measured.Add(node);
            sources.Add(source);
            hypothetical.Add(basis > 0 ? basis : horizontal ? node.Bounds.Width : node.Bounds.Height);
            grow.Add(flexible?.Flex ?? 0);
            shrink.Add(flexible?.Shrink ?? 0);
        }

        // Break into lines, measuring against the hypothetical sizes.
        var lines = new List<(int Start, int Count, float Main, float Cross)>();
        var lineStart = 0;
        var lineMain = 0f;
        var lineCross = 0f;
        for (var i = 0; i < measured.Count; i++)
        {
            var childMain = hypothetical[i];
            var childCross = horizontal ? measured[i].Bounds.Height : measured[i].Bounds.Width;
            var withGap = lineMain > 0 ? lineMain + flex.Gap + childMain : childMain;
            if (lineMain > 0 && withGap > mainAvail)
            {
                lines.Add((lineStart, i - lineStart, lineMain, lineCross));
                lineStart = i;
                lineMain = childMain;
                lineCross = childCross;
            }
            else
            {
                lineMain = withGap;
                lineCross = MathF.Max(lineCross, childCross);
            }
        }
        if (measured.Count > lineStart)
            lines.Add((lineStart, measured.Count - lineStart, lineMain, lineCross));

        // Resolve each LINE on its own — the second pass CSS makes, and the piece that was missing.
        // Leftover goes to the growers by weight; an overflowing line is taken back from the
        // shrinkers weighted by basis (as CSS scales it) and never past the min-content floor the
        // engine already computes. A child whose main size actually moved is measured again, so its
        // text re-wraps and its cross size is the one it will really occupy.
        if (!float.IsPositiveInfinity(mainAvail))
        {
            for (var l = 0; l < lines.Count; l++)
            {
                var line = lines[l];
                var slack = mainAvail - line.Main;
                var totalGrow = 0;
                var scaledShrink = 0f;
                for (var i = line.Start; i < line.Start + line.Count; i++)
                {
                    totalGrow += grow[i];
                    scaledShrink += shrink[i] * hypothetical[i];
                }

                var growing = slack > 0.01f && totalGrow > 0;
                var shrinking = slack < -0.01f && scaledShrink > 0;
                if (!growing && !shrinking) continue;

                var resolvedMain = 0f;
                var resolvedCross = 0f;
                for (var i = line.Start; i < line.Start + line.Count; i++)
                {
                    var size = hypothetical[i];
                    if (growing && grow[i] > 0)
                        size += slack * grow[i] / totalGrow;
                    else if (shrinking && shrink[i] > 0)
                    {
                        size += slack * (shrink[i] * hypothetical[i]) / scaledShrink;
                        size = MathF.Max(size, horizontal ? MinContentWidth(sources[i], ctx) : 0);
                    }

                    if (MathF.Abs(size - hypothetical[i]) > 0.01f)
                    {
                        var child = sources[i] is Flexible f ? f.Child : sources[i];
                        var remeasured = Measure(child, horizontal ? size : crossMax - padCross,
                            horizontal ? crossMax - padCross : size, ctx, ctx.ChildPath(path, i));
                        // A flex item OCCUPIES the size it resolved to, even when its content is
                        // shorter — otherwise the ones after it slide left and the line no longer
                        // fills what it was given.
                        remeasured.Bounds = horizontal
                            ? remeasured.Bounds with { Width = size }
                            : remeasured.Bounds with { Height = size };
                        measured[i] = remeasured;
                    }

                    resolvedMain += size + (i > line.Start ? flex.Gap : 0);
                    resolvedCross = MathF.Max(resolvedCross,
                        horizontal ? measured[i].Bounds.Height : measured[i].Bounds.Width);
                }

                lines[l] = (line.Start, line.Count, resolvedMain, resolvedCross);
            }
        }

        // Container extents.
        var contentMain = 0f;
        foreach (var line in lines) contentMain = MathF.Max(contentMain, line.Main);
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };
        var contentCross = 0f;
        foreach (var line in lines) contentCross += line.Cross;
        if (lines.Count > 1) contentCross += runGap * (lines.Count - 1);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(crossMax) => crossMax,
            _ => contentCross + padCross,
        };

        // Arrange line by line.
        var crossCursor = horizontal ? flex.Padding.Top : flex.Padding.Start;
        foreach (var line in lines)
        {
            var free = MathF.Max(0, (main - padMain) - line.Main);
            var mainCursor = (horizontal ? flex.Padding.Start : flex.Padding.Top) + flex.Main switch
            {
                MainAlign.Center => free / 2,
                MainAlign.End => free,
                _ => 0,
            };
            var betweenExtra = flex.Main == MainAlign.SpaceBetween && line.Count > 1 ? free / (line.Count - 1) : 0;

            for (var i = line.Start; i < line.Start + line.Count; i++)
            {
                var child = measured[i];
                var childMain = horizontal ? child.Bounds.Width : child.Bounds.Height;
                var childCross = horizontal ? child.Bounds.Height : child.Bounds.Width;
                var alignment = sources[i].AlignSelf ?? flex.Cross;
                var within = alignment switch
                {
                    CrossAlign.Center => (line.Cross - childCross) / 2,
                    CrossAlign.End => line.Cross - childCross,
                    _ => 0,
                };
                if (alignment == CrossAlign.Stretch && sources[i] is not Text)
                {
                    child.Bounds = horizontal
                        ? child.Bounds with { Height = line.Cross }
                        : child.Bounds with { Width = line.Cross };
                    within = 0;
                }

                child.Bounds = horizontal
                    ? child.Bounds with { X = mainCursor, Y = crossCursor + within }
                    : child.Bounds with { X = crossCursor + within, Y = mainCursor };
                result.Children.Add(child);
                mainCursor += childMain + flex.Gap + betweenExtra;
            }

            crossCursor += line.Cross + runGap;
        }

        result.Bounds = horizontal ? new Rect(0, 0, main, cross) : new Rect(0, 0, cross, main);
        return result;
    }

    /// <summary>
    /// Spec S4 — the grid track-sizing pass (CSS Grid twin, v1 auto-flow): Fixed tracks take their
    /// dp; Auto tracks size to their widest starting single-span item; Flex tracks share the
    /// remaining width by weight (collapsing to 0 in unbounded space). Children flow left→right,
    /// wrapping to a new row; a span clamps to the row's remainder. Rows size to their tallest cell.
    /// </summary>
    private static LayoutNode MeasureGrid(Grid grid, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = ctx.Node(grid);
        var columns = grid.Columns;
        var count = columns.Count;
        var rowGap = grid.RowGap ?? grid.Gap;
        var padH = grid.Padding.Horizontal;

        var avail = grid.Width.Kind == SizeKind.Fixed ? grid.Width.Value - padH
            : !float.IsPositiveInfinity(maxW) ? maxW - padH
            : float.PositiveInfinity;
        var gapTotal = grid.Gap * MathF.Max(0, count - 1);

        // Place children into (column, span) slots — auto-flow with span clamping.
        var placements = new (VisualNode Node, int Column, int Span, int Row)[grid.Children.Count];
        var col = 0;
        var row = 0;
        for (var i = 0; i < grid.Children.Count; i++)
        {
            var span = Math.Clamp(grid.Children[i].GridSpan < 1 ? 1 : grid.Children[i].GridSpan, 1, count);
            if (col + span > count) { col = 0; row++; }
            placements[i] = (grid.Children[i], col, span, row);
            col += span;
            if (col >= count) { col = 0; row++; }
        }

        // Track sizing: fixed → value; auto → widest starting single-span item; flex → weighted rest.
        var widths = new float[count];
        var flexTotal = 0f;
        var used = gapTotal;
        for (var c = 0; c < count; c++)
        {
            if (columns[c].Kind == SizeKind.Fixed) { widths[c] = columns[c].Value; used += widths[c]; }
            else if (columns[c].Kind == SizeKind.Fill) flexTotal += MathF.Max(0, columns[c].Value);
        }
        for (var c = 0; c < count; c++)
        {
            if (columns[c].Kind != SizeKind.Hug) continue;
            var widest = 0f;
            foreach (var pl in placements)
                if (pl.Column == c && pl.Span == 1)
                    widest = MathF.Max(widest, Measure(pl.Node, float.PositiveInfinity, maxH, ctx, path + "/probe").Bounds.Width);
            widths[c] = widest;
            used += widest;
        }
        var leftover = float.IsPositiveInfinity(avail) ? 0 : MathF.Max(0, avail - used);
        for (var c = 0; c < count; c++)
            if (columns[c].Kind == SizeKind.Fill && flexTotal > 0)
                widths[c] = leftover * (MathF.Max(0, columns[c].Value) / flexTotal);

        // Measure each child at its cell width; rows size to the tallest cell.
        var rowCount = placements.Length > 0 ? placements[^1].Row + 1 : 0;
        var rowHeights = new float[rowCount];
        var laid = new LayoutNode[placements.Length];
        for (var i = 0; i < placements.Length; i++)
        {
            var (node, c, span, r) = placements[i];
            var cellW = grid.Gap * (span - 1);
            for (var k = c; k < c + span; k++) cellW += widths[k];
            var child = Measure(node, cellW, maxH, ctx, ctx.ChildPath(path, i));
            // A Fill-width child pins to the cell (the realizer paints the full extent).
            if (CrossSizeKind(node, horizontal: false) == SizeKind.Fill || WidthKind(node) == SizeKind.Fill)
                child.Bounds = child.Bounds with { Width = cellW };
            laid[i] = child;
            rowHeights[r] = MathF.Max(rowHeights[r], child.Bounds.Height);
        }

        // Arrange.
        var xStarts = new float[count];
        var x = grid.Padding.Start;
        for (var c = 0; c < count; c++) { xStarts[c] = x; x += widths[c] + grid.Gap; }
        var y = grid.Padding.Top;
        for (var r = 0; r < rowCount; r++)
        {
            for (var i = 0; i < placements.Length; i++)
            {
                if (placements[i].Row != r) continue;
                laid[i].Bounds = laid[i].Bounds with { X = xStarts[placements[i].Column], Y = y };
                result.Children.Add(laid[i]);
            }
            y += rowHeights[r] + rowGap;
        }

        var contentW = gapTotal + grid.Padding.Horizontal;
        foreach (var w in widths) contentW += w;
        var width = grid.Width.Kind switch
        {
            SizeKind.Fixed => grid.Width.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(maxW) => maxW,
            _ => contentW,
        };
        var height = grid.Height.Kind switch
        {
            SizeKind.Fixed => grid.Height.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(maxH) => maxH,
            _ => (rowCount > 0 ? y - rowGap : y) + grid.Padding.Bottom,
        };
        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }

    private static SizeKind WidthKind(VisualNode node) => node switch
    {
        Box box => box.Style.Width.Kind,
        FlexNode flex => flex.Width.Kind,
        Grid grid => grid.Width.Kind,
        _ => SizeKind.Hug,
    };

    // ---- helpers ----------------------------------------------------------------------------------

    /// <summary>The child's declared size KIND on the flex cross axis (wrappers look through to their
    /// content; components can't be known without building — treated as Hug, i.e. stretchable).</summary>
    private static SizeKind CrossSizeKind(VisualNode node, bool horizontal) => node switch
    {
        Box box => (horizontal ? box.Style.Height : box.Style.Width).Kind,
        FlexNode flex => (horizontal ? flex.Height : flex.Width).Kind,
        Pressable pressable => CrossSizeKind(pressable.Child, horizontal),
        CodeSurface surface => CrossSizeKind(surface.Child, horizontal),
        SheetSurface sheet => CrossSizeKind(sheet.Child, horizontal),
        Hoverable hoverable => CrossSizeKind(hoverable.Child, horizontal),
        Shortcut shortcut => CrossSizeKind(shortcut.Child, horizontal),
        Adjustable adjustable => CrossSizeKind(adjustable.Child, horizontal),
        Flexible flexible => CrossSizeKind(flexible.Child, horizontal),
        // Always-explicit nodes: their constructors demand a size — stretch must never override.
        Image => SizeKind.Fixed,
        Icon => SizeKind.Fixed,
        Vector => SizeKind.Fixed,
        Drawing => SizeKind.Fixed,
        Spinner => SizeKind.Fixed,
        Grid grid => (horizontal ? grid.Height : grid.Width).Kind,
        // Layout-transparent wrappers delegate to what they wrap.
        Pinned pinned => CrossSizeKind(pinned.Child, horizontal),
        Draggable draggable => CrossSizeKind(draggable.Child, horizontal),
        SafeArea safeArea => CrossSizeKind(safeArea.Child, horizontal),
        Presence presence => CrossSizeKind(presence.Child, horizontal),
        LoopMotion loop => CrossSizeKind(loop.Child, horizontal),
        DragDismiss drag => CrossSizeKind(drag.Child, horizontal),
        Link link => CrossSizeKind(link.Child, horizontal),
        Anchored anchored => CrossSizeKind(anchored.Anchor, horizontal),
        _ => SizeKind.Hug,
    };

    /// <summary>A cap as a NUMBER, or 0 for unbounded — the one place a window-relative cap turns
    /// into dp, from the window the pass was handed rather than the space the parent had left.</summary>
    private static float CapDp(SizeValue cap, float window) => cap.Kind switch
    {
        SizeKind.Fixed => cap.Value,
        // A window smaller than the inset caps at ZERO, and zero has to survive: downstream a
        // non-positive max used to mean "no cap", so a phone-sized window did not clamp the panel,
        // it FREED it — the opposite of what the declaration asks for, and only in the window
        // where it matters most. Unbounded is -1 from here on, and zero is a real cap of zero.
        SizeKind.WindowMinus => MathF.Max(0, window - cap.Value),
        SizeKind.Fill => window,
        _ => Unbounded,
    };

    /// <summary>What a resolved cap says when there is none — NOT zero, which is a cap of zero.</summary>
    private const float Unbounded = -1f;

    private static float CapMax(float available, float styleMax) =>
        styleMax >= 0 ? MathF.Min(available, styleMax) : available;

    /// <summary>Max extent a child may use, given the node's own size request.</summary>
    private static float ResolveForChild(SizeValue size, float available) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        _ => available,
    };

    /// <summary>A window-relative SIZE — the same arithmetic the caps do, for a node that asks to
    /// BE the window less an inset rather than to be capped by it. Without this the kind resolved
    /// only as a cap, and `Width = WindowMinus(24)` hugged natively while the web sized it.</summary>
    private static float WindowSize(SizeValue size, float window) => MathF.Max(0, window - size.Value);

    /// <summary>Own size: explicit &gt; Fill &gt; Hug (spec A1). On an INDETERMINATE axis — one the
    /// parent is sizing from its content — Fill has nothing to fill and falls back to Hug.</summary>
    /// <summary>The size a node asks to BE, with the window in hand — the window-relative kind is
    /// the one that cannot be answered from the available space alone.</summary>
    private static float ResolveSelf(SizeValue size, float available, float hug, float window,
        bool indeterminate = false, bool stretched = false) => size.Kind == SizeKind.WindowMinus
        ? WindowSize(size, window)
        : ResolveSelf(size, available, hug, indeterminate, stretched);

    private static float ResolveSelf(SizeValue size, float available, float hug, bool indeterminate = false,
        bool stretched = false) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        SizeKind.Fill when !indeterminate && !float.IsPositiveInfinity(available) => available,
        // Stretched by the parent: an auto size becomes the size the parent decided.
        SizeKind.Hug when stretched && !indeterminate && !float.IsPositiveInfinity(available) => available,
        _ => hug,
    };

    /// <summary>Min is a dp (0 = none); MAX follows the resolved-cap convention — negative is
    /// unbounded, and zero is a real cap of zero. See <see cref="Unbounded"/>.</summary>
    private static float Clamp(float value, float min, float max)
    {
        if (min > 0) value = MathF.Max(value, min);
        if (max >= 0) value = MathF.Min(value, max);
        return value;
    }

    private static void Absolutize(LayoutNode node, float originX, float originY)
    {
        node.Bounds = node.Bounds with { X = node.Bounds.X + originX, Y = node.Bounds.Y + originY };
        foreach (var child in node.Children)
            Absolutize(child, node.Bounds.X, node.Bounds.Y);
    }
}
