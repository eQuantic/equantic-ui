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
    /// <summary>
    /// The measurement pass for this context, made once and kept.
    /// <para>
    /// A frame is not ONE <see cref="LayoutEngine.Layout"/> call: the realizer lays the page out and
    /// then each Overlay against the viewport, sharing this context. Building the visitor at the call
    /// site therefore allocated one per LAYER, which the dense-scene harness could not see because it
    /// has no overlays — and the pooled budget it guards is 0.1 KB under its ceiling, so a handful of
    /// layers would have crossed it with every test still green. Found in review.
    /// </para>
    /// <para>
    /// Holding it here rather than passing one in keeps <see cref="LayoutEngine.Layout"/>'s signature,
    /// and the visitor reads nothing but this context, so a context outliving a frame reuses it.
    /// </para>
    /// <para>
    /// WHAT THAT COSTS IN PRODUCTION, measured rather than claimed: `PhotonRealizer.Realize` builds a
    /// NEW context per frame, so this is one object per frame — not one per layer, which it was, and
    /// not one for the life of the host, which an earlier version of this sentence said. The static
    /// switch it replaced allocated none. Comparing the pooled steady state on this branch against
    /// `main`, byte for byte rather than at the harness's KB resolution: 75,714 against 75,682, so
    /// THIRTY-TWO BYTES a frame — one object, 0.04% of a 74 KB budget.
    /// </para>
    /// <para>
    /// Removing even that means the realizer reusing its context across frames, which is its
    /// decision and not the engine's: the context carries per-frame state and is built with
    /// `init` properties that vary per frame. Worth doing if the budget ever needs the 32 bytes —
    /// it has 0.1 KB of headroom, so this is a third of what is left.
    /// </para>
    /// </summary>
    internal MeasureVisitor MeasurePass => _measurePass ??= new MeasureVisitor(this);

    private MeasureVisitor? _measurePass;

    /// <summary>
    /// How many measurement passes were built AGAINST THIS CONTEXT — one, for as long as the host
    /// keeps it, however many layers a frame lays out.
    /// <para>
    /// It exists because nothing else can see the difference. Reading <see cref="MeasurePass"/> is a
    /// tautology (the property caches whatever it makes, whether or not `Layout` used it), and the
    /// allocation harness cannot resolve one small object inside a frame — the eight-layer scene
    /// allocates 78.1 KB either way. Counting per CONTEXT rather than globally keeps it honest under
    /// xUnit's parallel classes, which is where a static tally would have gone flaky.
    /// </para>
    /// </summary>
    internal int PassesBuilt { get; private set; }

    internal void CountPass() => PassesBuilt++;

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
        // ONE visitor per CONTEXT, which is what lets the context be a field on it and only the
        // constraints and the path travel down. A frame runs several Layout calls (the page plus
        // each Overlay subtree) and they share this context, so they share its visitor.
        var node = context.MeasurePass.Measure(
            root,
            LayoutConstraints.Of(viewportWidth, viewportHeight).Stretched(rootStretch, StretchKind.None),
            context,
            rootPath);
        Absolutize(node, 0, 0);
        return node;
    }

    // ---- measurement (bounds are PARENT-RELATIVE until Absolutize) ------------------------------

    private static void Absolutize(LayoutNode node, float originX, float originY)
    {
        node.Bounds = node.Bounds with { X = node.Bounds.X + originX, Y = node.Bounds.Y + originY };
        foreach (var child in node)
            Absolutize(child, node.Bounds.X, node.Bounds.Y);
    }
}
