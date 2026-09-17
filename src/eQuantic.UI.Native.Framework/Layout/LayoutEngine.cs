using System.Collections.ObjectModel;
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
    /// zero. Null (tests, one-shot layouts) simply concatenates. A keyed segment (see
    /// <see cref="ChildPath(string, int, VisualNode)"/>) caches under its key, an indexed one under
    /// null.
    /// </summary>
    public Dictionary<(string Parent, int Index, string? Key), string>? PathCache { get; init; }

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
        if (!cache.TryGetValue((parent, index, null), out var cached))
            cache[(parent, index, null)] = cached = parent + "/" + index;
        return cached;
    }

    /// <summary>
    /// The path of a child AMONG SIBLINGS. A child that says <see cref="VisualNode.Key"/> takes the
    /// key as its segment, spelled <c>[key]</c> so it can never collide with a position; every other
    /// child takes its position. This is the web's keyed reconciliation arriving on Photon, where
    /// the path IS identity — focus, hover, scroll offsets, a drag in flight are all remembered by
    /// it. A virtualized list that materializes rows 2..20 one frame and 3..21 the next shifts every
    /// positional path by one: the ring that sat on row 17 pointed at row 18, and when the index ran
    /// past the window it pointed at nothing and Tab restarted from the first control. A keyed row
    /// keeps its path whatever its neighbours do.
    /// </summary>
    internal string ChildPath(string parent, int index, VisualNode child)
    {
        if (child.Key is not { Length: > 0 } key) return ChildPath(parent, index);
        if (PathCache is not { } cache) return parent + "/[" + Escape(key) + "]";
        if (!cache.TryGetValue((parent, 0, key), out var cached))
            cache[(parent, 0, key)] = cached = parent + "/[" + Escape(key) + "]";
        return cached;
    }

    /// <summary>
    /// A key made safe to be ONE segment. Keys are frequently generated — a row id, a heading slug,
    /// a URL — and a '/' inside one would read as a path boundary, quietly turning a single child
    /// into a nested pair and pointing focus, hover and scroll state at a node that does not exist.
    /// The escape doubles '~' and spells '/' as "~s", which is injective: two different keys can
    /// never collapse onto one segment, so the identity the path carries stays the key's own. A key
    /// with neither character — every key anyone has written so far — allocates nothing.
    /// </summary>
    private static string Escape(string key) =>
        key.IndexOf('~') < 0 && key.IndexOf('/') < 0
            ? key
            : key.Replace("~", "~~").Replace("/", "~s");

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
    public LayoutNode(VisualNode source)
    {
        Source = source;
        _view = _children.AsReadOnly();
    }

    public VisualNode Source { get; private set; }
    public Rect Bounds { get; internal set; }

    /// <summary>
    /// The node that placed this one, or null at the root. The composite read the other way.
    ///
    /// <para>
    /// This is the RENDER tree, which is the tree Flutter makes bidirectional too — its
    /// <c>RenderObject</c> carries a <c>parent</c> while its <c>Widget</c> carries none, and the
    /// asymmetry is the point: Flutter's `Widget` is rebuilt constantly and a back-reference on it means
    /// nothing, while the laid-out tree is walked top-down and the parent is literally in hand.
    /// <see cref="VisualNode"/> has no Parent for exactly that reason and should not grow one.
    /// </para>
    ///
    /// <para>
    /// Flutter's other half, <c>parentData</c>, has no equivalent here and needs none: it exists to
    /// carry what a parent assigned — an offset, a flex factor — and this engine resolves all of
    /// that into <see cref="Bounds"/> during the same pass. A second slot would hold a copy.
    /// </para>
    /// </summary>
    public LayoutNode? Parent { get; private set; }

    /// <summary>
    /// The children, placed. Read-only by design — see <see cref="Adopt"/>.
    ///
    /// <para>
    /// A WRAPPER rather than the backing list typed as <c>IReadOnlyList</c>, because that interface
    /// only hides the mutators from the compiler: <c>(List&lt;LayoutNode&gt;)node.Children</c> would
    /// succeed and <c>Add</c> would attach a child whose <see cref="Parent"/> nobody set — a tree
    /// that lies about itself, silently. Through this, that cast fails and an <c>IList</c> cast
    /// throws on <c>Add</c>, which is the failure this repo prefers to a quiet wrong answer.
    /// </para>
    ///
    /// <para>
    /// It costs one object per node EVER CREATED, not per frame: a pooled node is reused, so the
    /// wrapper outlives every recycle and the steady-state cost is zero. Measured on the allocation
    /// harness rather than argued — the pooled path had 278 bytes of headroom under its ceiling
    /// when this was written, which is too little to assume anything with.
    /// </para>
    /// </summary>
    public IReadOnlyList<LayoutNode> Children => _view;

    /// <summary>
    /// Iterate the node ITSELF — <c>foreach (var child in node)</c> — on any path that runs per
    /// frame. It hands back the list's own struct enumerator, so nothing is allocated.
    ///
    /// <para>
    /// Both doors exist because they answer different needs and the first attempt tried to make one
    /// do both. Typed only as <c>IReadOnlyList</c>, every <c>foreach</c> in the layout and emit
    /// passes boxed an enumerator and the frame allocated 18% more — the perf harness caught it.
    /// Typed only as a <c>ReadOnlySpan</c> the allocation went away and so did LINQ, assertions and
    /// any use inside an iterator, because a ref struct cannot cross a <c>yield</c>: a public tree
    /// consumers cannot query is not a public tree.
    /// </para>
    /// </summary>
    public List<LayoutNode>.Enumerator GetEnumerator() => _children.GetEnumerator();

    private readonly List<LayoutNode> _children = new();
    private readonly ReadOnlyCollection<LayoutNode> _view;

    /// <summary>
    /// Attaches a child AND links it back. One door, because the link is an invariant and an
    /// invariant maintained by remembering is one that breaks at the fourteenth call site — the
    /// defect this repo has already paid for in a semantics switch nobody rechecked.
    /// </summary>
    internal void Adopt(LayoutNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>The same for a placed batch — reordering a row's children, most often.</summary>
    internal void AdoptAll(IEnumerable<LayoutNode> children)
    {
        foreach (var child in children) Adopt(child);
    }

    /// <summary>Forgets the node that placed this one — the other end of
    /// <see cref="ReleaseChildren"/>, for a node being let go by a parent that is itself going.</summary>
    internal void Orphan() => Parent = null;

    /// <summary>Detaches every child, clearing the back-links with them: a node left pointing at a
    /// parent that no longer holds it is the stale half of a two-way link.</summary>
    internal void ReleaseChildren()
    {
        foreach (var child in _children) child.Parent = null;
        _children.Clear();
    }
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
        Parent = null;
        ReleaseChildren();
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

    /// <summary>
    /// Returns a whole laid-out tree. The caller asserts nobody else holds it.
    /// <para>
    /// The BACK-LINKS are cut here rather than left to the next <see cref="Rent"/>. A node waiting
    /// in the pool still pointing at the parent that let it go is a tree that lies about itself —
    /// and, worse, holds that parent alive through the pool for as long as the node is free. Whoever
    /// takes a two-way link apart owns both ends of it.
    /// </para>
    /// </summary>
    public void RecycleTree(LayoutNode root)
    {
        for (var i = root.Children.Count - 1; i >= 0; i--) RecycleTree(root.Children[i]);
        root.ReleaseChildren();
        root.Orphan();
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
    /// <param name="root">The tree to lay out.</param>
    /// <param name="viewportWidth">The available width in dp — the window, or the layer's box.</param>
    /// <param name="viewportHeight">The available height in dp.</param>
    /// <param name="context">The pass's shared state: theme, text service, retention.</param>
    /// <param name="rootPath">The path the root is addressed by, which every child extends.</param>
    /// <param name="rootStretch">
    /// What the caller has already decided about the ROOT's own size. The base layer is the page's
    /// body — an auto-sized root stretches to the window in WIDTH the way a CSS block does — while
    /// an overlay layer keeps shrink-to-fit on both axes, which is what lets a dropdown hug its
    /// options over a page that fills the viewport. It is a PARAMETER because a root has no parent
    /// to have told it: it used to be a one-shot field set on the context by the realizer and read
    /// on the way past, which is the last of the mutable layout state this engine carried.
    /// </param>
    public static LayoutNode Layout(VisualNode root, float viewportWidth, float viewportHeight, LayoutContext context,
        string rootPath = "r", StretchKind rootStretch = StretchKind.None)
    {
        // NOTE: the reconciler pass is ENDED BY THE CALLER (PhotonRealizer) — one frame may run
        // several Layout calls (the page plus each Overlay subtree) sharing one retention pass.
        context.WindowWidth = viewportWidth;
        context.WindowHeight = viewportHeight;
        var node = Measure(
            root,
            LayoutConstraints.Of(viewportWidth, viewportHeight).Stretched(rootStretch, StretchKind.None),
            context,
            rootPath);
        Absolutize(node, 0, 0);
        return node;
    }

    // ---- measurement (bounds are PARENT-RELATIVE until Absolutize) ------------------------------

    /// <summary>Measures, then stamps the node with WHERE it is. Every node gets the path, so any
    /// gesture that has to survive a frame can key on it rather than on an object identity the next
    /// Build will not share.</summary>
    private static LayoutNode Measure(VisualNode node, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var measured = MeasureCore(node, constraints, ctx, path);
        measured.Path ??= path;
        return measured;
    }

    private static LayoutNode MeasureCore(VisualNode node, LayoutConstraints constraints, LayoutContext ctx, string path) => node switch
    {
        // Only the nodes that can HAVE an auto size worth stretching take the flags; for the rest
        // (text, images, fixed primitives) the parent's decision changes nothing.
        Box box => MeasureBox(box, constraints, ctx, path),
        FlexNode flex => MeasureFlex(flex, constraints, ctx, path),
        Stack stack => MeasureStack(stack, constraints, ctx, path),
        Grid grid => MeasureGrid(grid, constraints, ctx, path),
        // Spec S6: an AdaptiveNode IS its resolved variant on native — the other variants never
        // measure, never paint (the web keeps them, CSS-gated).
        AdaptiveNode adaptive => Measure(adaptive.Resolve(ctx.SizeClass), constraints, ctx, ctx.ChildPath(path, 0)),
        // Spec S7: Pinned renders IN FLOW on native until engine scrolling lands (correct at scroll
        // offset 0); the pinning joins the scroll compositor (fence on the node's doc).
        Pinned pinned => MeasureWrapper(pinned, pinned.Child, constraints, ctx, path),
        // The system's own margins. A desktop window has no cutouts, so the host reports zero and
        // the node measures as its child plus whatever padding the caller asked for on top — the
        // SAME tree an iPhone insets, with the numbers coming from the host rather than the app.
        SafeArea safeArea => MeasureSafeArea(safeArea, constraints, ctx, path),
        // Wave 3: the anchor owns layout; the panel realizes in the realizer's overlay pass.
        Anchored anchored => MeasureWrapper(anchored, anchored.Anchor, constraints, ctx, path),
        ScrollView scroll => MeasureScrollView(scroll, constraints, ctx, path),
        // A Positioned outside a Stack has no anchor frame — degrade to a transparent wrapper.
        // A continuous gesture is layout-transparent: the offset is a PAINT translate, exactly like
        // the sheet's. Re-laying out under a finger would fight the scroll it usually lives in.
        Draggable draggable => MeasureDraggable(draggable, constraints, ctx, path),
        Positioned positioned => MeasureWrapper(positioned, positioned.Child, constraints, ctx, path),
        Text text => MeasureText(text, constraints.MaxWidth, ctx),
        TextEntry entry => MeasureTextEntry(entry, constraints.MaxWidth, ctx),
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
            ResolveSelf(canvas.Width, constraints.MaxWidth, 0, ctx.WindowWidth, constraints.Width.Indeterminate),
            ResolveSelf(canvas.Height, constraints.MaxHeight, 0, ctx.WindowHeight, constraints.Height.Indeterminate))),
        // The INLINE-BLOCK barrier (CSS twin): a button, a link and an input are not block-level —
        // a BLOCK container does not stretch them (they hug), while a FLEX stretch reaches
        // through (align-items: stretch stretches any item, buttons included).
        Pressable pressable => MeasureWrapper(pressable, pressable.Child, constraints.Inline(), ctx, path),
        // Transparent to layout: the surface adds a caret and a selection, never a box.
        CodeSurface surface => MeasureWrapper(surface, surface.Child, constraints, ctx, path),
        SheetSurface sheet => MeasureWrapper(sheet, sheet.Child, constraints, ctx, path),
        // Pointer presence is layout-transparent (S5 programmable hover — the child owns visuals).
        Hoverable hoverable => MeasureWrapper(hoverable, hoverable.Child, constraints, ctx, path),
        // A simulated state changes only what is DRAWN, so it takes no space of its own.
        Simulated simulated => MeasureWrapper(simulated, simulated.Child, constraints, ctx, path),
        // Reports presence; it neither takes space nor draws.
        InView inView => MeasureWrapper(inView, inView.Child, constraints, ctx, path),
        // The intent has to be armed while the child BUILDS — by layout time the tree already has
        // the scrim in it, and there is nothing left to decide.
        InFlow inFlow => MeasureInFlow(inFlow, constraints, ctx, path),
        // Spec S8: a Shortcut is layout-transparent — the binding rides the realizer's walk.
        Shortcut shortcut => MeasureWrapper(shortcut, shortcut.Child, constraints, ctx, path),
        Adjustable adjustable => MeasureWrapper(adjustable, adjustable.Child, constraints.Inline(), ctx, path),
        // A Link is layout-transparent (semantics + interaction only — the child owns visuals).
        Link link => MeasureWrapper(link, link.Child, constraints.Inline(), ctx, path),
        Flexible flexible => MeasureWrapper(flexible, flexible.Child, constraints, ctx, path),
        // Loop motion is layout-transparent: the offset is a REALIZE-time transform (spec §06 —
        // transform-only frames never re-lay-out).
        LoopMotion motion => MeasureWrapper(motion, motion.Child, constraints, ctx, path),
        // Enter motion is layout-transparent too (opacity layer + paint-only translate) — but the
        // progress is resolved HERE, where the stable path exists, and stamped on the node.
        Presence presence => MeasurePresence(presence, constraints, ctx, path),
        // Drag-to-dismiss follows the same pattern: transparent for layout, offset stamped by path.
        DragDismiss drag => MeasureDragDismiss(drag, constraints, ctx, path),
        // An Overlay is ZERO in the page flow — the realizer lays its child out against the
        // VIEWPORT in the overlay pass (path "ov<i>", stable for the reconciler).
        Overlay => ctx.Node(node),
        // A component expands INLINE: Build produces its subtree (pure, mode-free), which is measured
        // in place — the component wraps it in the layout tree, drawing nothing itself.
        // Components RECONCILE by position first: the retained instance (state alive) replaces the
        // fresh one the parent just built, adopting its config; then it expands inline via Build.
        UiComponent component => MeasureComponent(component, constraints, ctx, path),
        Spacer => ctx.Node(node), // zero outside a flex container (layout-only)
        _ => ctx.Node(node),
    };

    /// <summary>
    /// Insets from the HOST (notch, status bar, home indicator) plus the caller's own padding. The
    /// host reports them; on a desktop window they are zero, which is the correct answer there.
    /// </summary>
    private static LayoutNode MeasureSafeArea(SafeArea safeArea, LayoutConstraints constraints,
        LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
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
        var child = Measure(safeArea.Child, constraints.WithMax(
            MathF.Max(0, maxW - start - end), MathF.Max(0, maxH - top - bottom)), ctx, ctx.ChildPath(path, 0));
        child.Bounds = child.Bounds with { X = start, Y = top };

        var node = ctx.Node(safeArea,
            new Rect(0, 0, child.Bounds.Width + start + end, child.Bounds.Height + top + bottom));
        node.Adopt(child);
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
        // EIGHT WRAPPERS ANSWER ZERO HERE, and they are not eight of a kind — the split below is
        // the distinction, because a reader who takes all eight for oversights "fixes" three
        // deliberate contracts.
        //
        // FIVE ARE OMISSIONS: named by CrossSizeKind and not by this list, which was kept by hand.
        // Nobody decided them, and they are load-bearing anyway — a wrapped Text is invisible to
        // the truncation contract (it finds Text among a row's children BY TYPE), so the zero floor
        // is what lets a shrinking row cut it down to where the bare text would have landed.
        // Preserved exactly as measured; #225 is where the FOUR readers are made to agree — these
        // two, `Shrinkable`, and that contract.
        DragDismiss or Draggable or LoopMotion or Pinned or SafeArea => 0,
        // THREE ARE PRINCIPLED, and were in NEITHER list: a scroller's floor is not its content's
        // (it scrolls instead of growing), an Overlay is a viewport layer that takes no space in the
        // page flow at all, and a Positioned is a contract with a Stack rather than a child of the
        // row. These answer zero because zero is right, not because nobody wrote them down.
        Overlay or Positioned or ScrollView => 0,
        // Wrappers are transparent to the floor exactly as they are to layout.
        SingleChildNode wrapper => MinContentWidth(wrapper.Child, ctx),
        UiComponent component => component.ExpandContained(ctx.Components, ctx,
            static (built, context) => MinContentWidth(built, context)),
        _ => 0,
    };

    private static float RowMinContent(Row row, LayoutContext ctx)
    {
        if (row.Width.Kind == SizeKind.Fixed) return row.Width.Value;
        var total = row.Padding.Horizontal + row.Gap * MathF.Max(0, row.Children.Count - 1);
        foreach (var child in row) total += MinContentWidth(child, ctx);
        return total;
    }

    private static float ColumnMinContent(Column column, LayoutContext ctx)
    {
        if (column.Width.Kind == SizeKind.Fixed) return column.Width.Value;
        var widest = 0f;
        foreach (var child in column)
            widest = MathF.Max(widest, MinContentWidth(child, ctx));
        return widest + column.Padding.Horizontal;
    }

    /// <summary>The widest single word — text wraps between words and never inside one.</summary>
    private static float LongestWordWidth(Text text, LayoutContext ctx)
    {
        var style = text.Resolve(ctx.Theme);
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

    /// <summary>
    /// Measures an <see cref="InFlow"/> with the intent armed, so the overlay inside it builds its
    /// panel rather than its layer. Restored after — a sibling dialog opened for real must still
    /// take the viewport.
    /// </summary>
    private static LayoutNode MeasureInFlow(InFlow inFlow, LayoutConstraints constraints, LayoutContext ctx,
        string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
        var previous = InFlow.Current;
        InFlow.Current = true;
        try
        {
            return MeasureWrapper(inFlow, inFlow.Child, constraints, ctx, path);
        }
        finally
        {
            InFlow.Current = previous;
        }
    }

    private static LayoutNode MeasureWrapper(VisualNode node, VisualNode child, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
        var result = ctx.Node(node);
        // Layout-transparent means transparent to STRETCH too: whatever the parent would stretch,
        // it stretches through the wrapper — a Pressable around a tab cell (or the component node
        // around a page) must not eat the size the parent granted.
        var inner = Measure(child, constraints, ctx, ctx.ChildPath(path, 0));
        result.Adopt(inner);
        result.Bounds = new Rect(0, 0, inner.Bounds.Width, inner.Bounds.Height);
        return result;
    }

    private static LayoutNode MeasureComponent(UiComponent component, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var resolved = ctx.Instances?.Reconcile(path, component) ?? component;
        // Through the BOUNDARY, never Build: a throw here used to reach the host and cost the FRAME —
        // the window stops presenting and the app is gone, for one component's null reference. And
        // ExpandContained rather than BuildContained, because this recurses: measuring what a
        // component built reaches MeasureComponent again, so a component that builds itself would
        // otherwise take the frame down by a stack overflow instead of a throw.
        return resolved.ExpandContained(ctx.Components, (resolved, constraints, ctx, path),
            static (built, state) =>
                MeasureWrapper(state.resolved, built, state.constraints, state.ctx, state.path));
    }

    /// <summary>A transparent wrapper that also resolves the ENTRANCE progress against the host's
    /// presence clock, keyed by this stable path — the emit pass applies the paint-only effect and
    /// snapshots the subtree's commands by the same path (the exit replay source).</summary>
    private static LayoutNode MeasurePresence(Presence presence, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = MeasureWrapper(presence, presence.Child, constraints, ctx, path);
        result.Presence = ctx.Presences?.Progress(path, ctx.TimeMs, ctx.ReducedMotion) ?? 1f;
        result.PresencePath = path;
        return result;
    }

    /// <summary>A transparent wrapper that resolves the current DRAG offset against the host's drag
    /// clock (active follow or glide-back), keyed by this stable path — the emit pass paints the
    /// translate and registers the drag region the host routes input by.</summary>
    private static LayoutNode MeasureDragDismiss(DragDismiss drag, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = MeasureWrapper(drag, drag.Child, constraints, ctx, path);
        result.DragOffset = ctx.Drags?.Resolve(path, ctx.TimeMs) ?? 0f;
        result.DragPath = path;
        return result;
    }

    /// <summary>The live offset for this gesture — the finger while it is down, the glide after it
    /// lifts, and the caller's RestOffset when neither is happening.</summary>
    private static LayoutNode MeasureDraggable(Draggable draggable, LayoutConstraints constraints,
        LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = MeasureWrapper(draggable, draggable.Child, constraints, ctx, path);
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
    private static LayoutNode MeasureStack(Stack stack, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = ctx.Node(stack);
        // The cell IS the stack's available space — the contract the web's grid keeps (LowerStack):
        // a Fill child covers a stack that has a size of its own. Measured against the INCOMING
        // constraints instead, a Fill canvas under a scroll view's unbounded axis measured zero
        // inside a stack standing 240dp tall — the first chart drew nothing on Photon while the
        // browser drew it. An explicit axis decides for the children, and is determinate for them
        // whatever the parent said; the other axes pass the question through unchanged.
        var childMaxW = stack.Width.Kind == SizeKind.Fixed ? stack.Width.Value : maxW;
        var childMaxH = stack.Height.Kind == SizeKind.Fixed ? stack.Height.Value : maxH;
        var outerIndeterminateW = constraints.Width.Indeterminate;
        var outerIndeterminateH = constraints.Height.Indeterminate;
        // An explicit axis DECIDES for the children and is determinate for them whatever the parent
        // said; the other axes pass the question through unchanged. Computed for the child's own
        // constraint rather than written onto the context and undone afterwards.
        var stackIndetW = stack.Width.Kind != SizeKind.Fixed && outerIndeterminateW;
        var stackIndetH = stack.Height.Kind != SizeKind.Fixed && outerIndeterminateH;
        var contentW = 0f;
        var contentH = 0f;

        for (var stackIndex = 0; stackIndex < stack.Children.Count; stackIndex++)
        {
            var child = stack.Children[stackIndex];
            var measured = Measure(
                child,
                constraints.ForChild(childMaxW, childMaxH).DecidedByContent(stackIndetW, stackIndetH),
                ctx, ctx.ChildPath(path, stackIndex, child));
            result.Adopt(measured);
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
        if (AnyLayered(result, stack))
        {
            var ordered = new List<(LayoutNode Node, int Z, int I)>(result.Children.Count);
            for (var i = 0; i < result.Children.Count; i++)
                ordered.Add((result.Children[i],
                    PositionedOf(stack.Children[i], result.Children[i]) is { } p ? p.Layer : 0, i));
            ordered.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.I.CompareTo(b.I));

            var sorted = new LayoutNode[ordered.Count];
            for (var i = 0; i < ordered.Count; i++) sorted[i] = ordered[i].Node;
            result.ReleaseChildren();
            result.AdoptAll(sorted);
        }

        return result;
    }

    /// <summary>Whether any child asked for a layer — the cheap question, asked before the sort that
    /// answering it would otherwise pay for on every Stack in the tree.</summary>
    private static bool AnyLayered(LayoutNode result, Stack stack)
    {
        for (var i = 0; i < result.Children.Count; i++)
            if (PositionedOf(stack.Children[i], result.Children[i]) is { Layer: not 0 })
                return true;
        return false;
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

    private static LayoutNode MeasureScrollView(ScrollView scroll, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = ctx.Node(scroll);
        var horizontal = scroll.Axis == ScrollAxis.Horizontal;

        var child = Measure(scroll.Child, constraints.ForChild(
            horizontal ? float.PositiveInfinity : maxW,
            horizontal ? maxH : float.PositiveInfinity), ctx, ctx.ChildPath(path, 0));
        result.Adopt(child);

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
        foreach (var child in node)
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
        // The SAME resolver the realizers use. This built the merge by hand and therefore measured
        // without the theme's code face while PhotonRealizer rasterized with it — wrapping, widths
        // and a caret column computed against one face and drawn in another.
        var style = text.Resolve(ctx.Theme);
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
            var runStyle = run.Resolve(paragraph, ctx.Theme);

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

                fragments.Add(new TextFragment(word, runStyle, x, line * lineHeight, width, line,
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

    private static LayoutNode MeasureBox(Box box, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
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
        var inheritedIndeterminateW = constraints.Width.Indeterminate;
        var inheritedIndeterminateH = constraints.Height.Indeterminate;

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
            var boxIndetW = style.Width.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchW != StretchKind.None && !inheritedIndeterminateW && !float.IsPositiveInfinity(selfMaxW)),
                _ => inheritedIndeterminateW,
            };
            var boxIndetH = style.Height.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchH != StretchKind.None && !inheritedIndeterminateH && !float.IsPositiveInfinity(selfMaxH)),
                _ => inheritedIndeterminateH,
            };
            // CSS block semantics on the WIDTH: a box whose width is determined stretches an
            // auto-sized child across it — a div's child div is full-width without asking. This is
            // what keeps a hugging Column inside a Fill card at the card's width on native, the way
            // the SAME tree already behaves on the web, where a Column realizes as width:auto.
            var boxStretchW = boxIndetW ? StretchKind.None : StretchKind.Block;
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
            var boxStretchH = boxIndetH ? StretchKind.None : StretchKind.Block;
            child = Measure(
                box.Child,
                constraints.ForChild(MathF.Max(0, childMaxW), MathF.Max(0, childMaxH))
                    .DecidedByContent(boxIndetW, boxIndetH)
                    .Stretched(boxStretchW, boxStretchH),
                ctx, ctx.ChildPath(path, 0));
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Adopt(child);
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
            var outerH2 = constraints.Height.Indeterminate;
            var clampedIndetH = style.Height.Kind == SizeKind.Hug
                && style.MinHeight <= 0 && maxHeightDp < 0 && outerH2;
            // The clamped width is a DECIDED size — block semantics again: the child stretches
            // across it, which is what centres a Stepper's reading inside its MinWidth cell.
            result.ReleaseChildren();
            child = Measure(
                box.Child!,
                constraints.ForChild(MathF.Max(0, width - style.Padding.Horizontal),
                        MathF.Max(0, height - style.Padding.Vertical))
                    .DecidedByContent(false, clampedIndetH)
                    .Stretched(StretchKind.Block, StretchKind.None),
                ctx, ctx.ChildPath(path, 0));
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Adopt(child);
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

    private static LayoutNode MeasureFlex(FlexNode flex, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
        if (flex.Wrap) return MeasureFlexWrapped(flex, constraints, ctx, path);

        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (constraints.MaxWidth, constraints.MaxHeight) : (constraints.MaxHeight, constraints.MaxWidth);
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
        foreach (var c in flex)
            if (c is Flexible or Spacer { Flex: > 0 }) { hasFlexibles = true; break; }
        // On an INDETERMINATE main axis the available maximum is not a size anyone granted — it is
        // the measuring parent's upper bound. Distributing leftover against it made a Flexible
        // spacer swallow the viewport: an option row's Fill (inherited-indeterminate) inside a
        // hugging Select panel measured its spacer against 1180dp of "available" and dragged every
        // hugging ancestor to the width of the window.
        var mainIndeterminateNow = horizontal ? constraints.Width.Indeterminate : constraints.Height.Indeterminate;
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
        var crossIndeterminateNow = horizontal ? constraints.Height.Indeterminate : constraints.Width.Indeterminate;
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

        // The CROSS stretch this container grants a child, answered rather than written onto a
        // context for `Measure` to pick up on the way past.
        (StretchKind W, StretchKind H) CrossStretch(VisualNode child) =>
            !StretchesCross(child) ? (StretchKind.None, StretchKind.None)
            : horizontal ? (StretchKind.None, StretchKind.Flex)
            : (StretchKind.Flex, StretchKind.None);

        // Every child measures under the flags THIS container restated. A flexible's share is a
        // size the container genuinely granted, so the main axis is determinate inside the slot
        // regardless of how the container itself is sized.
        LayoutNode MeasureChild(VisualNode child, float w, float h, string childPath,
            bool mainGranted = false, StretchKind stretchW = StretchKind.None,
            StretchKind stretchH = StretchKind.None)
        {
            return Measure(
                child,
                constraints.ForChild(w, h)
                    .DecidedByContent(
                        childIndetW && !(mainGranted && horizontal),
                        childIndetH && !(mainGranted && !horizontal))
                    .Stretched(stretchW, stretchH),
                ctx, childPath);
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
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i, f), f.Flex, ctx.TimeMs,
                        f.AnimateChanges, ctx.ReducedMotion) ?? f.Flex;
                    continue;
                case Spacer { Flex: > 0 } s:
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i, s), s.Flex, ctx.TimeMs,
                        s.AnimateChanges, ctx.ReducedMotion) ?? s.Flex;
                    continue;
                case Spacer fixedSpacer:
                    mains[i] = fixedSpacer.FixedLength;
                    rigidSum += mains[i];
                    continue;
            }

            var childMaxW = horizontal ? mainAvail : crossAvail;
            var childMaxH = horizontal ? crossAvail : mainAvail;
            var (csW, csH) = CrossStretch(children[i]);
            var child = MeasureChild(children[i], childMaxW, childMaxH, ctx.ChildPath(path, i, children[i]),
                stretchW: csW, stretchH: csH);
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
                    // Truncation RE-measures, so it has to re-measure in the same face: this was the
                    // last hand-built merge, and a text that shrinks to an ellipsis against one face
                    // and draws in another ellipsizes at the wrong word.
                    var style = text.Resolve(ctx.Theme);
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
                            var (rsW, rsH) = CrossStretch(children[i]);
                            var reflowed = MeasureChild(children[i], childMaxW2, childMaxH2,
                                ctx.ChildPath(path, i, children[i]), stretchW: rsW, stretchH: rsH);
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
                var (usW, usH) = CrossStretch(unbounded);
                var intrinsic = MeasureChild(unbounded.Child, horizontal ? mainAvail : crossAvail,
                    horizontal ? crossAvail : mainAvail, ctx.ChildPath(ctx.ChildPath(path, i, unbounded), 0),
                    stretchW: usW, stretchH: usH);
                mains[i] = horizontal ? intrinsic.Bounds.Width : intrinsic.Bounds.Height;
                rigidSum += mains[i];
                var grown = ctx.Node(unbounded, intrinsic.Bounds);
                grown.Adopt(intrinsic);
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
                var (fsW, fsH) = CrossStretch(flexible);
                // The share IS the slot's main size (the bounds are pinned to it below), so the
                // child is stretched on the main axis too: an auto-sized cell takes the share and
                // lays out inside it — a flex-grow item's autos fill the cell, per CSS.
                // The share IS the slot's main size, so the child is stretched on the main axis
                // too, on top of whatever the cross axis granted.
                var child = MeasureChild(flexible.Child, childMaxW, childMaxH,
                    ctx.ChildPath(ctx.ChildPath(path, i, flexible), 0), mainGranted: true,
                    stretchW: horizontal ? StretchKind.Flex : fsW,
                    stretchH: horizontal ? fsH : StretchKind.Flex);
                // The flexible slot IS the share on the main axis (the child fills it).
                child.Bounds = horizontal
                    ? child.Bounds with { Width = share }
                    : child.Bounds with { Height = share };
                var wrapper = ctx.Node(flexible, child.Bounds);
                wrapper.Adopt(child);
                laid[i] = wrapper;
            }
            else
            {
                laid[i] = ctx.Node(children[i]); // flexible Spacer: pure space
            }
        }

        // Container size. Fill on an axis the PARENT is sizing from its content has nothing to fill
        // — see AxisConstraint.Indeterminate. Without this a `Centered()` wrapper (Fill on both
        // axes, which is what makes centring possible at all) dragged its hugging parent to the
        // full width of the window: a 16dp badge came out 600dp wide, shoving its neighbours off.
        var mainIndeterminate = horizontal ? constraints.Width.Indeterminate : constraints.Height.Indeterminate;
        var crossIndeterminate = horizontal ? constraints.Height.Indeterminate : constraints.Width.Indeterminate;

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
            result.Adopt(child);
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
    private static LayoutNode MeasureFlexWrapped(FlexNode flex, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (constraints.MaxWidth, constraints.MaxHeight) : (constraints.MaxHeight, constraints.MaxWidth);
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
            var node = Measure(child, constraints.ForChild(constraint, crossMax - padCross), ctx, ctx.ChildPath(path, i, source));

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
                        var remeasured = Measure(child, constraints.ForChild(horizontal ? size : crossMax - padCross,
                            horizontal ? crossMax - padCross : size), ctx, ctx.ChildPath(path, i, sources[i]));
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
                result.Adopt(child);
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
    private static LayoutNode MeasureGrid(Grid grid, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
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
                    widest = MathF.Max(widest, Measure(pl.Node, constraints.ForChild(float.PositiveInfinity, maxH), ctx, path + "/probe").Bounds.Width);
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
            var child = Measure(node, constraints.ForChild(cellW, maxH), ctx, ctx.ChildPath(path, i, node));
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
                result.Adopt(laid[i]);
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

        // Always-explicit nodes: their constructors demand a size — stretch must never override.
        Image => SizeKind.Fixed,
        Icon => SizeKind.Fixed,
        Vector => SizeKind.Fixed,
        Drawing => SizeKind.Fixed,
        Spinner => SizeKind.Fixed,
        Grid grid => (horizontal ? grid.Height : grid.Width).Kind,
        // SIX WRAPPERS ANSWER HUG, split the same way as the floor above and for the same reason.
        // THREE ARE OMISSIONS: named by MinContentWidth and not here, the other half of the
        // eight-place disagreement between two lists kept by hand.
        InFlow or InView or Simulated => SizeKind.Hug,
        // THREE ARE PRINCIPLED, the same three, in neither list and deliberate in both: a scroller,
        // a viewport layer, and a Stack's contract do not take their cross size from a child.
        // Preserved exactly as measured; reconciling the omissions moves pixels, which is #225.
        Overlay or Positioned or ScrollView => SizeKind.Hug,
        Anchored anchored => CrossSizeKind(anchored.Anchor, horizontal),
        // Layout-transparent wrappers delegate to what they wrap.
        SingleChildNode wrapper => CrossSizeKind(wrapper.Child, horizontal),
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
        foreach (var child in node)
            Absolutize(child, node.Bounds.X, node.Bounds.Y);
    }
}
