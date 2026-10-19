using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>A pressable region registered by the realizer — hit rect expanded to the §08 contract.</summary>
/// <param name="Bounds">The hit rect, already expanded to the §08 minimum target.</param>
/// <param name="Drawn">Where the pressable is drawn: its own box, without the slop that grows its
/// target. A press inside it is this pressable's before it is a neighbour's slop (#630).</param>
/// <param name="Node">The pressable this region belongs to.</param>
/// <param name="Element">The pressable's position. A press outlives the frame it began in — the
/// pressed state repaints, and the next Build makes fresh nodes — so the target is remembered by its
/// position, which a rebuild keeps while the same control stands there. Remembering the node meant
/// every press that spanned a frame quietly did nothing, and remembering its path meant a press could
/// land on whatever control took the place.</param>
public readonly record struct HitRegion(Rect Bounds, Rect Drawn, Pressable Node, Element Element);

/// <summary>Spec S5/gestures: a hover-reactive region — a Box carrying a Hover diff. The host's
/// pointer tracking resolves the TOPMOST region under the pointer (paint order = registration
/// order, so last-contains wins). <paramref name="Element"/> is the node's position, which the hover
/// chain holds across rebuilds.</summary>
public readonly record struct HoverRegion(Rect Bounds, VisualNode Node, Element Element);

/// <summary>Scroll compositor v1: a scrollable viewport — the host routes wheel/drag input to the
/// TOPMOST region under the pointer and moves the offset its <paramref name="Element"/> keeps
/// (clamped to MaxOffset).</summary>
/// <param name="Bounds">The viewport, where the pointer scrolls it.</param>
/// <param name="Element">The view's position, which keeps its offset: what the host scrolls, and what
/// the host walks up from a focus stop or a caret to find the views that hold it.</param>
/// <param name="MaxOffset">How far the content scrolls in this frame.</param>
/// <param name="Axis">The axis it scrolls along.</param>
/// <param name="Fallback">The node's own <c>Offset</c>, where it stands until the host scrolls it.</param>
public readonly record struct ScrollRegion(Rect Bounds, Element Element, float MaxOffset, ScrollAxis Axis,
    float Fallback);

/// <summary>
/// W3: a <see cref="Canvas"/> the host routes pointer events to, in the canvas's OWN coordinates.
/// The engine knows only that a point landed in the box — what was drawn there, and therefore what
/// was hit, is the app's arithmetic.
/// </summary>
public readonly record struct CanvasRegion(Rect Bounds, Canvas Node, Element Element);

/// <summary>
/// A surface the host routes a drag to: a press that travels past the slop drags the TOPMOST region
/// under where it started (paint-order last-wins). <see cref="Node"/> is the gesture node itself — a
/// <see cref="DragDismiss"/> or a <see cref="Draggable"/> — because the RULES (axis, limits, what a
/// release means) belong to it, and duplicating them here would be a second place to get them wrong.
/// <see cref="Element"/> is its position, which keeps the drag: the host moves it by that, so a drag
/// in flight never passes to a node drawn at its place next.
/// </summary>
public readonly record struct DragRegion(Rect Bounds, Element Element, VisualNode Node);

/// <summary>
/// A navigation surface: a tap no pressable claims resolves to the TOPMOST link region under the
/// point, through the host's navigation seam.
/// <para>
/// It carries the DESTINATION rather than the <see cref="Link"/> node, because a link is not always
/// a node: a linked run inside a sentence is a rectangle the layout computed and a string, and the
/// destination was the only thing the host ever asked the node for.
/// </para>
/// <para>
/// The <paramref name="Id"/> is what the KEYBOARD and a screen reader name it by: its node's
/// position's where there is a node. A linked run has none, so <see cref="RichTextRuns"/> names it by
/// the id its paragraph's position keeps for it (<see cref="NodeElement.PartId"/>), and the region,
/// the focus stop and the semantic node all carry that same id (#255). A link that wraps registers
/// one region per line under ONE id: the pointer needs both rectangles, the keyboard needs one stop.
/// </para>
/// </summary>
public readonly record struct LinkRegion(Rect Bounds, string Destination, int Id);

/// <summary>Spec S8: a keyboard binding that is live because its subtree is on screen — the host
/// dispatches a key press to the LAST registered match (the dialog on top wins the chord). A
/// <see cref="Scope"/> is the position of the subtree a focus-scoped binding answers for: it is live
/// only while the focus is inside it.</summary>
public readonly record struct ShortcutBinding(KeyChord Chord, Action OnPressed, Element? Scope = null);

/// <summary>An editable field. A text entry is not a pressable — a click puts a CARET in it and the
/// keys that follow belong to it — so it registers its own kind of region, and the host keeps the
/// caret against its <paramref name="Element"/> for the same reason the press does: the tree is
/// rebuilt on every keystroke. <paramref name="Visible"/> is the part of <paramref name="Bounds"/> the clips
/// around the field leave on screen, which is all a press can land on, as a code surface's. Null where
/// nothing clips it.</summary>
public readonly record struct TextRegion(Rect Bounds, TextEntry Entry, Element Element, Rect? Visible = null);

/// <summary>An editable SPREADSHEET surface: a click takes the selection (a cell resolved by
/// prefix-sum arithmetic over the window), a drag extends it, and the keys that follow speak
/// Excel through the shared controller. <paramref name="Visible"/> is the part of
/// <paramref name="Bounds"/> on screen, which is all a press can land on. Null where nothing clips
/// it.</summary>
public readonly record struct SheetRegion(Rect Bounds, SheetSurface Surface, Element Element, Rect? Visible = null);

/// <summary>A surface that changes what the mouse pointer looks like (BoxStyle.Cursor — the CSS
/// cursor mirror). The host answers CursorAt from these, topmost first.</summary>
public readonly record struct CursorRegion(Rect Bounds, PointerCursor Cursor);

/// <summary>An editable CODE surface. Like a text region it takes the caret on a click and the keys
/// that follow, but what those keys mean lives in its controller, so the region only has to carry
/// its position and the geometry that turns a point into a (line, column). <paramref name="Offered"/> is
/// where what the surface offers at its caret landed (<see cref="CodeSurface.Options"/>), on screen: a
/// press there that no row of it took is the list's, and never moves the caret under it.
/// <paramref name="Visible"/> is the part of <paramref name="Bounds"/> the clips around the surface
/// leave on screen, which is all a press can land on: a long file's surface runs far past the viewport
/// that shows it. Null where nothing clips it.</summary>
public readonly record struct CodeRegion(Rect Bounds, CodeSurface Surface, Element Element, Rect? Offered = null,
    Rect? Visible = null);

/// <summary>
/// One stop on the Tab route. Buttons and fields are different kinds of region and are dispatched
/// differently, but they are ONE sequence to the person pressing Tab — a form whose traversal skips
/// its own text fields is not a form. Stops are appended as they are registered, so the order is
/// paint order, which is tree order.
/// <para>
/// Registered whether or not the control is on screen, which is the one place the clip rule is
/// deliberately NOT applied. Clipping exists so nobody can click what they cannot see; the keyboard
/// wants the opposite — Tab reaches the field below the fold and the view scrolls to it. Applying
/// the pointer's rule here made the seven fields under a 200dp viewport unreachable without a
/// mouse, with the tab order quietly looping over the five that showed.
/// </para>
/// <para>
/// A LINK registers one of these too, so Tab reaches it — but what FOLLOWING it takes lives on
/// <see cref="LinkRegion"/> rather than here. A stop is suppressed inside a composite and a region
/// is not, and a link inside a grid still has to be followable by the reader that announces it.
/// </para>
/// <para>
/// <see cref="Id"/> is what the focus is held by, and <see cref="Element"/> the position the stop sits
/// in, which the host walks up from to find what holds it: the same position's id for a control, and
/// for a linked run the id its paragraph keeps for it, inside the paragraph's position.
/// </para>
/// </summary>
public readonly record struct FocusStop(int Id, Element Element, Pressable? Pressable, TextEntry? Entry, Rect Bounds,
    Adjustable? Adjustable = null, CodeSurface? Code = null, Navigable? Grid = null,
    SheetSurface? Sheet = null);

/// <summary>The realized frame: the laid-out tree (absolute bounds) and the interactive hit regions.</summary>
public sealed class RealizeResult
{
    public RealizeResult(LayoutNode root, IReadOnlyList<HitRegion> hitRegions, bool hasActiveMotion,
        IReadOnlyList<HoverRegion>? hoverRegions = null, IReadOnlyList<ScrollRegion>? scrollRegions = null,
        IReadOnlyList<DragRegion>? dragRegions = null, IReadOnlyList<LinkRegion>? linkRegions = null,
        IReadOnlyList<ShortcutBinding>? shortcuts = null, IReadOnlyList<TextRegion>? textRegions = null,
        IReadOnlyList<FocusStop>? focusStops = null, IReadOnlyList<CodeRegion>? codeRegions = null,
        IReadOnlyList<LayoutNode>? overlayRoots = null, IReadOnlyList<Overlay>? overlayLayers = null,
        IReadOnlyList<SheetRegion>? sheetRegions = null,
        IReadOnlyList<CursorRegion>? cursorRegions = null,
        IReadOnlyList<CanvasRegion>? canvasRegions = null,
        IReadOnlyList<TableRegion>? tableRegions = null)
    {
        Root = root;
        OverlayRoots = overlayRoots ?? Array.Empty<LayoutNode>();
        OverlayLayers = overlayLayers ?? Array.Empty<Overlay>();
        SheetRegions = sheetRegions ?? Array.Empty<SheetRegion>();
        CursorRegions = cursorRegions ?? Array.Empty<CursorRegion>();
        CanvasRegions = canvasRegions ?? Array.Empty<CanvasRegion>();
        TableRegions = tableRegions ?? Array.Empty<TableRegion>();
        HitRegions = hitRegions;
        HasActiveMotion = hasActiveMotion;
        HoverRegions = hoverRegions ?? Array.Empty<HoverRegion>();
        ScrollRegions = scrollRegions ?? Array.Empty<ScrollRegion>();
        DragRegions = dragRegions ?? Array.Empty<DragRegion>();
        LinkRegions = linkRegions ?? Array.Empty<LinkRegion>();
        Shortcuts = shortcuts ?? Array.Empty<ShortcutBinding>();
        TextRegions = textRegions ?? Array.Empty<TextRegion>();
        FocusStops = focusStops ?? Array.Empty<FocusStop>();
        CodeRegions = codeRegions ?? Array.Empty<CodeRegion>();
    }

    /// <summary>Canvases the pointer can reach, in paint order (topmost last).</summary>
    public IReadOnlyList<CanvasRegion> CanvasRegions { get; }

    /// <summary>
    /// The live regions this frame holds, in tree order. INTERNAL, unlike every list beside it: a
    /// mark carries a <c>LayoutNode</c> belonging to this frame, and this frame's tree is recycled
    /// the moment the next one lands. What crosses to a shell is the ANNOUNCEMENT
    /// (<see cref="LiveAnnouncement"/>), which is strings and outlives anything.
    /// </summary>
    internal IReadOnlyList<LiveRegionMark> LiveRegions { get; init; } = Array.Empty<LiveRegionMark>();

    /// <summary>The regions registered under a transform, and what turns a point on screen back into
    /// each one's own space (#513). Empty in a frame that transforms nothing interactive.</summary>
    internal IReadOnlyList<TransformedRegion> TransformedRegions { get; init; } = Array.Empty<TransformedRegion>();

    /// <summary>Editable code surfaces, in paint order (topmost last).</summary>
    public IReadOnlyList<CodeRegion> CodeRegions { get; }

    /// <summary>Editable spreadsheet surfaces, in paint order (topmost last).</summary>
    public IReadOnlyList<SheetRegion> SheetRegions { get; }

    /// <summary>Table viewports the wheel, a pan and a scrollbar press reach, in paint order (topmost
    /// last).</summary>
    public IReadOnlyList<TableRegion> TableRegions { get; }

    /// <summary>Pointer-shape surfaces (BoxStyle.Cursor), in paint order (topmost last).</summary>
    public IReadOnlyList<CursorRegion> CursorRegions { get; }

    /// <summary>The overlay layers' laid-out trees, in paint order — retained so the semantics
    /// walk sees what a dialog shows, not just the page beneath it.</summary>
    public IReadOnlyList<LayoutNode> OverlayRoots { get; }

    /// <summary>
    /// The <see cref="Overlay"/> NODES behind those roots, index for index — what each layer IS,
    /// beside where it landed. The root's own source is the overlay's CHILD (the realizer lays that
    /// out against the viewport), so without this the semantics walk reaches a dialog's contents
    /// with no way to know it is in one.
    /// </summary>
    public IReadOnlyList<Overlay> OverlayLayers { get; }

    /// <summary>Everything Tab visits, in tree order: buttons and fields in one sequence.</summary>
    public IReadOnlyList<FocusStop> FocusStops { get; }

    /// <summary>Editable fields, in paint order (topmost last).</summary>
    public IReadOnlyList<TextRegion> TextRegions { get; }

    public LayoutNode Root { get; }
    public IReadOnlyList<HitRegion> HitRegions { get; }

    /// <summary>Hover-reactive regions (Boxes with a Hover diff), in paint order.</summary>
    public IReadOnlyList<HoverRegion> HoverRegions { get; }

    /// <summary>Scrollable viewports, in paint order (topmost last).</summary>
    public IReadOnlyList<ScrollRegion> ScrollRegions { get; }

    /// <summary>Drag-to-dismiss surfaces, in paint order (topmost last).</summary>
    public IReadOnlyList<DragRegion> DragRegions { get; }

    /// <summary>Navigation surfaces (Links), in paint order (topmost last).</summary>
    public IReadOnlyList<LinkRegion> LinkRegions { get; }

    /// <summary>Spec S8 keyboard bindings live in this frame, in mount order (last wins a chord).</summary>
    public IReadOnlyList<ShortcutBinding> Shortcuts { get; }

    /// <summary>True when the frame contains running loop motion — the host keeps scheduling frames
    /// while set (and stops when Reduce Motion statically disables the movement).</summary>
    public bool HasActiveMotion { get; }
}

/// <summary>
/// The NATIVE REALIZER for the shared abstract vocabulary (docs/SHARED-COMPONENTS-PLAN.md): lays a
/// <see cref="VisualNode"/> tree out with the C# flex engine, resolves tokens for the active theme
/// mode, and lowers the result to Photon draw commands. The web realizer lowers the SAME tree to
/// HtmlElement/DOM + CSS.
/// </summary>
public static class PhotonRealizer
{
    public static RealizeResult Realize(
        VisualNode root,
        float viewportWidth,
        float viewportHeight,
        IAppTheme theme,
        ThemeMode mode,
        DisplayListBuilder builder,
        ITextMeasurer? measurer = null,
        float typeScale = 1f,
        Pressable? pressed = null,
        Pressable? focused = null,
        VisualNode? hovered = null,
        // The tree the host keeps across frames (null builds one for this realize alone, calling no
        // lifecycle hook — what a test or a tool laying a tree out once wants).
        BuildOwner? owner = null,
        float timeMs = 0,
        bool reducedMotion = false,
        // The host's motion clocks (null = values snap, presences are settled, drags rest). What a
        // node keeps between frames lives on its position in the owner's tree, so a frame realized
        // without an owner starts every node fresh.
        TransitionClock? transitions = null,
        PresenceClock? presences = null,
        DragClock? drags = null,
        Framework.ITextRasterizer? textRasterizer = null,
        TextRasterCache? textCache = null,
        float renderScale = 1f,
        Framework.IIconRasterizer? iconRasterizer = null,
        IconRasterCache? iconCache = null,
        Framework.IImageLoader? imageLoader = null,
        Dictionary<string, TextureData?>? imageCache = null,
        EdgeInsets safeAreaInsets = default,
        // A press lasts a moment and focus lasts until the user says otherwise — both outlive the
        // frame that started them, and `Build` hands back NEW nodes every time. The node is the
        // thing to draw; its POSITION is the thing that survives, and the focus its id, since a
        // linked run is focused without a node of its own. (Tests still pass nodes alone: with one
        // frame and no rebuild, identity by reference is the same answer.)
        Element? pressedAt = null,
        int focusedId = 0,
        // The field being edited and where its caret sits. Both live in the HOST, not in the tree:
        // the app owns the text and hands back a new node for every character, so a caret stored in
        // the node would be reborn at the end of the string on every keystroke.
        Element? textAt = null,
        int caretIndex = 0,
        bool caretVisible = true,
        // The selected range inside that field. Zero-length = a caret and nothing more.
        int selectionStart = 0,
        int selectionEnd = 0,
        Density density = Density.Comfortable,
        // The IME composition in flight for the focused field — rendered inline at the caret,
        // underlined, without the value changing (the value only changes on commit).
        string markedText = "",
        // A phone keyboard's composition: a range OF the value, drawn underlined the same way.
        (int Start, int End, string Text)? composing = null,
        // The owning host's node recycler (see LayoutNodePool) — null allocates fresh.
        LayoutNodePool? nodePool = null,
        // Where the hover LIVES across rebuilds — the press's rule, applied to the pointer. A CHAIN,
        // not a single node: CSS :hover matches every ancestor under the pointer, and the native
        // realizer answers the same question the web twin answers.
        IReadOnlyList<Element>? hoveredAt = null,
        // Where the system's window controls sit under a unified desktop chrome (see
        // LayoutContext.WindowControlsInsets) — zero everywhere else.
        EdgeInsets windowControlsInsets = default,
        // The frame before this one, whose region counts size this frame's lists (FrameRegions).
        RealizeResult? sizedLike = null,
        // Whether every component builds in this frame, or only the ones a SetState marked: the
        // HOST decides, because only it knows whether something it did since the last frame (input,
        // an invalidation) changed what a build would read.
        bool rebuildAll = true,
        // Where layout says what it decided that the developer did not write (LayoutContext.Log).
        Action<string>? log = null)
    {
        var context = new LayoutContext(theme, measurer ?? ApproximateTextMeasurer.Instance, typeScale,
            density)
        {
            Pool = nodePool,
            // The HOST's cutouts. Zero on a desktop window, and the shell's real numbers on a phone
            // — the tree is the same either way, which is the point of the node.
            SafeAreaInsets = safeAreaInsets,
            WindowControlsInsets = windowControlsInsets,
            SizeClass = WindowSizeClasses.FromWidth(viewportWidth),
            Transitions = transitions,
            Presences = presences,
            TimeMs = timeMs,
            ReducedMotion = reducedMotion,
            Drags = drags,
            Log = log,
        };
        transitions?.BeginFrame();
        presences?.BeginFrame(timeMs);
        drags?.BeginFrame();
        // The BASE layer is the page's body: an auto-sized root stretches to the real containing
        // block — the window — the way a CSS block does, and ONLY in width: block height hugs
        // (pages grow downward; a full-height page asks for Height = Fill). Overlay layers below
        // keep shrink-to-fit on both axes (position:fixed semantics), which is what lets a
        // dropdown panel hug its options while the page behind it still fills the viewport.
        //
        // The tree BUILDS first, and the layout measures what it built (docs/ELEMENT-TREE-PLAN.md, 3.2).
        var builds = owner ?? BuildOwner.Structural();
        var rootElement = builds.Build(root, context.Components, context.SizeClass, rebuildAll);
        var layout = LayoutEngine.Layout(root, viewportWidth, viewportHeight, context,
            rootStretch: StretchKind.Block, rootElement: rootElement);

        var regions = new FrameRegions(sizedLike);
        var motion = new MotionScope(timeMs, reducedMotion)
        {
            Presences = presences,
            Transitions = transitions,
            ViewportW = viewportWidth,
            ViewportH = viewportHeight,
            TextRasterizer = textRasterizer,
            TextCache = textCache,
            ImageLoader = imageLoader,
            ImageCache = imageCache,
            RenderScale = renderScale,
            TypeScale = typeScale,
            IconRasterizer = iconRasterizer,
            IconCache = iconCache,
        };
        var overlays = new List<QueuedLayer>();
        var input = new InputSink(regions);
        // An InView reports in a tree that is drawn again, as a lifecycle hook runs: a callback that
        // sets state for a tree laid out once would set it for nobody.
        var reportsInView = builds.Live;
        EmitVisitor.Shared.Emit(new EmitState(layout, input, new PressScope(pressed, focused, hovered, pressedAt, focusedId, textAt, caretIndex, caretVisible, selectionStart, selectionEnd, density, hoveredAt) { MarkedText = markedText, Composing = composing, Surface = new Rect(0, 0, viewportWidth, viewportHeight), ReportsInView = reportsInView },
            theme, mode, builder, motion, overlays));

        // Overlay pass (Phase C): each queued layer lays out against the VIEWPORT and paints ABOVE
        // the page (painter's order); its hit regions register after the page's, so the topmost-
        // last-wins dispatch routes taps to the layer — a full-viewport scrim Pressable in the
        // layer blocks (and optionally handles) everything behind it.
        var overlayRoots = new List<LayoutNode>();
        var realizedLayers = new List<Overlay>();
        for (var i = 0; i < overlays.Count; i++)
        {
            // A CLOSED layer is realized NOWHERE — no pixels, no hit region, no focus stop, no
            // semantic node. That is the state the web expresses with `visibility: hidden`, and
            // Photon used to express as nothing at all: `Open` was read by no native reader, so a
            // dismissed dialog kept painting at full opacity, kept eating taps, kept its Tab stop
            // and was still read out (#252). Motion gates it because Motion is what makes `Open`
            // mean anything — without one the layer is shown and hidden by being built or not, and
            // the web ignores the flag for the same reason.
            //
            // A layer is found again by its Overlay's position, which holds it in a slot of its own,
            // so skipping a closed one moves nothing after it: the dialog under a closing toast
            // keeps its focus, its press state and its presence snapshots. When layers were named
            // `ov{i}` by their place in this queue, the index had to stay for that (#976).
            var queued = overlays[i];
            if (queued.Layer is { Motion: not null, Open: false }) continue;
            // Shown, so its deferred slot builds now, just before the layer is laid out against it.
            // Every node the layout measured has a position, so a layer without one is the engine
            // gone wrong: built here it would be a tree of fresh instances every frame, silently.
            var layerElement = (queued.Holder ?? throw new InvalidOperationException(
                    $"Overlay layer {i} of this frame has no position to build in."))
                .ShowDeferred(queued.Layer.Child);
            var overlayLayout = LayoutEngine.Layout(queued.Layer.Child, viewportWidth, viewportHeight,
                context, rootElement: layerElement);
            overlayRoots.Add(overlayLayout);
            realizedLayers.Add(queued.Layer);
            // The UNCLIPPED sink: a layer lays out against the viewport, not inside whatever the
            // page happens to be scrolling.
            EmitVisitor.Shared.Emit(new EmitState(overlayLayout, input, new PressScope(pressed, focused, hovered, pressedAt, focusedId, textAt, caretIndex, caretVisible, selectionStart, selectionEnd, density, hoveredAt) { MarkedText = markedText, Composing = composing, Surface = new Rect(0, 0, viewportWidth, viewportHeight), ReportsInView = reportsInView },
                theme, mode, builder, motion, overlays));
        }

        // The frame's end, for the tree: what a layer did not show this frame leaves it, and what
        // left unmounts before what arrived mounts. BEFORE the exits: a presence leaves either during
        // the build (its parent stopped building it) or here (its layer was not shown), and either
        // way its last paint becomes an exit stamped with this frame's time, so a layer that closed
        // in this frame fades from this frame, not from the next.
        builds.EndFrame();

        // Mid-flight exits replay here, ABOVE everything (departed layers were topmost), as pixels
        // only — no hit regions, input passes through immediately.
        if (presences != null)
        {
            foreach (var exit in presences.ActiveExits(timeMs, reducedMotion))
            {
                // The drop is handed to the replay in DEVICE units instead of being pushed as a
                // transform. Pushing it made the recorded commands compose with the whole current
                // transform — root scale included, which they already carry — so on a retina screen
                // a closing dialog jumped down and right for one frame before it faded.
                var offset = exit.Drop != 0
                    ? Matrix2D.Translation(0, exit.Drop * renderScale)
                    : Matrix2D.Identity;
                builder.PushLayer(exit.Alpha);
                foreach (var command in exit.Commands) builder.Replay(command, offset);
                builder.PopLayer();
            }
        }
        return new RealizeResult(layout, regions.Hits,
            motion.Active || transitions is { AnyActive: true } || presences is { AnyActive: true }
                || drags is { AnyActive: true },
            regions.Hovers, regions.Scrolls, regions.Drags, regions.Links, regions.Shortcuts,
            regions.Texts, regions.Stops, regions.Codes, overlayRoots,
            realizedLayers, regions.Sheets, regions.Cursors, regions.Canvases, regions.Tables)
        {
            // Set here rather than through the constructor: the mark is INTERNAL (it holds a node
            // of this frame), and a public constructor cannot take one. The public shape is
            // unchanged, which is also what every existing caller wants.
            LiveRegions = regions.LivesOrEmpty,
            TransformedRegions = regions.TransformedOrEmpty,
        };
    }


}
