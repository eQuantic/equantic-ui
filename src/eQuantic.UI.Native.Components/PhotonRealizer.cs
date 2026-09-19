using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>A pressable region registered by the realizer — hit rect expanded to the §08 contract.</summary>
/// <param name="Bounds">The hit rect, already expanded to the §08 minimum target.</param>
/// <param name="Node">The pressable this region belongs to.</param>
/// <param name="Path">Where the pressable sits in the tree. A press outlives the frame it began in
/// — the pressed state repaints, and the next Build makes fresh nodes — so the target is remembered
/// by PATH. Remembering the object meant every press that spanned a frame quietly did nothing, and
/// a real click always spans one.</param>
public readonly record struct HitRegion(Rect Bounds, Pressable Node, string Path = "");

/// <summary>Spec S5/gestures: a hover-reactive region — a Box carrying a Hover diff. The host's
/// pointer tracking resolves the TOPMOST region under the pointer (paint order = registration
/// order, so last-contains wins).</summary>
public readonly record struct HoverRegion(Rect Bounds, VisualNode Node, string Path);

/// <summary>Scroll compositor v1: a scrollable viewport — the host routes wheel/drag input to the
/// TOPMOST region under the pointer and adjusts its stored offset (clamped to MaxOffset).</summary>
public readonly record struct ScrollRegion(Rect Bounds, string Path, float MaxOffset, ScrollAxis Axis, float Fallback);

/// <summary>
/// W3: a <see cref="Canvas"/> the host routes pointer events to, in the canvas's OWN coordinates.
/// The engine knows only that a point landed in the box — what was drawn there, and therefore what
/// was hit, is the app's arithmetic.
/// </summary>
public readonly record struct CanvasRegion(Rect Bounds, Canvas Node, string Path);

/// <summary>Gestures v2: a drag-to-dismiss surface — the host tracks a press that travels past the
/// slop as a vertical drag on the TOPMOST region under the start point (paint-order last-wins).</summary>
/// <summary>
/// A surface the host routes a drag to. <see cref="Node"/> is the gesture node itself — a
/// <see cref="DragDismiss"/> or a <see cref="Draggable"/> — because the RULES (axis, limits, what a
/// release means) belong to it, and duplicating them here would be a second place to get them wrong.
/// </summary>
public readonly record struct DragRegion(Rect Bounds, string Path, VisualNode Node);

/// <summary>
/// A navigation surface: a tap no pressable claims resolves to the TOPMOST link region under the
/// point, through the host's navigation seam.
/// <para>
/// It carries the DESTINATION rather than the <see cref="Link"/> node, because a link is not always
/// a node: a linked run inside a sentence is a rectangle the layout computed and a string, and the
/// destination was the only thing the host ever asked the node for.
/// </para>
/// </summary>
public readonly record struct LinkRegion(Rect Bounds, string Destination);

/// <summary>Spec S8: a keyboard binding that is live because its subtree is on screen — the host
/// dispatches a key press to the LAST registered match (the dialog on top wins the chord).</summary>
public readonly record struct ShortcutBinding(KeyChord Chord, Action OnPressed);

/// <summary>An editable field. A text entry is not a pressable — a click puts a CARET in it and the
/// keys that follow belong to it — so it registers its own kind of region, and the host keeps the
/// caret against the <paramref name="Path"/> for the same reason the press does: the tree is rebuilt
/// on every keystroke.</summary>
public readonly record struct TextRegion(Rect Bounds, TextEntry Entry, string Path);

/// <summary>An editable SPREADSHEET surface: a click takes the selection (a cell resolved by
/// prefix-sum arithmetic over the window), a drag extends it, and the keys that follow speak
/// Excel through the shared controller.</summary>
public readonly record struct SheetRegion(Rect Bounds, SheetSurface Surface, string Path);

/// <summary>A surface that changes what the mouse pointer looks like (BoxStyle.Cursor — the CSS
/// cursor mirror). The host answers CursorAt from these, topmost first.</summary>
public readonly record struct CursorRegion(Rect Bounds, PointerCursor Cursor);

/// <summary>An editable CODE surface. Like a text region it takes the caret on a click and the keys
/// that follow, but what those keys mean lives in its controller, so the region only has to carry
/// the path and the geometry that turns a point into a (line, column).</summary>
public readonly record struct CodeRegion(Rect Bounds, CodeSurface Surface, string Path);

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
/// </summary>
public readonly record struct FocusStop(string Path, Pressable? Pressable, TextEntry? Entry, Rect Bounds,
    Adjustable? Adjustable = null, CodeSurface? Code = null);

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
        IReadOnlyList<CanvasRegion>? canvasRegions = null)
    {
        Root = root;
        OverlayRoots = overlayRoots ?? Array.Empty<LayoutNode>();
        OverlayLayers = overlayLayers ?? Array.Empty<Overlay>();
        SheetRegions = sheetRegions ?? Array.Empty<SheetRegion>();
        CursorRegions = cursorRegions ?? Array.Empty<CursorRegion>();
        CanvasRegions = canvasRegions ?? Array.Empty<CanvasRegion>();
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

    /// <summary>Editable code surfaces, in paint order (topmost last).</summary>
    public IReadOnlyList<CodeRegion> CodeRegions { get; }

    /// <summary>Editable spreadsheet surfaces, in paint order (topmost last).</summary>
    public IReadOnlyList<SheetRegion> SheetRegions { get; }

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
        ComponentInstanceStore? instances = null,
        float timeMs = 0,
        bool reducedMotion = false,
        TransitionStore? transitions = null,
        ScrollStore? scrollOffsets = null,
        PresenceStore? presences = null,
        DragStore? drags = null,
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
        // thing to draw; the PATH is the thing that survives. (Tests still pass nodes alone: with
        // one frame and no rebuild, identity by reference is the same answer.)
        string? pressedPath = null,
        string? focusedPath = null,
        // The field being edited and where its caret sits. Both live in the HOST, not in the tree:
        // the app owns the text and hands back a new node for every character, so a caret stored in
        // the node would be reborn at the end of the string on every keystroke.
        string? textPath = null,
        int caretIndex = 0,
        bool caretVisible = true,
        // The selected range inside that field. Zero-length = a caret and nothing more.
        int selectionStart = 0,
        int selectionEnd = 0,
        Func<string, float?>? scrollOffset = null,
        Density density = Density.Comfortable,
        // The IME composition in flight for the focused field — rendered inline at the caret,
        // underlined, without the value changing (the value only changes on commit).
        string markedText = "",
        // The host's cross-frame path-string cache (see LayoutContext.PathCache).
        Dictionary<(string Parent, int Index, string? Key), string>? pathCache = null,
        // The owning host's node recycler (see LayoutNodePool) — null allocates fresh.
        LayoutNodePool? nodePool = null,
        // Which InView nodes were on screen LAST frame — the node is rebuilt every pass and cannot
        // remember, and the contract is that the callback fires on the transitions.
        InViewStore? inViewStore = null,
        // Where the hover LIVES across rebuilds — the press's path rule, applied to the pointer.
        // A CHAIN, not a single node: CSS :hover matches every ancestor under the pointer, and the
        // native realizer answers the same question the web twin answers.
        IReadOnlyList<string>? hoveredPaths = null,
        // Where the system's window controls sit under a unified desktop chrome (see
        // LayoutContext.WindowControlsInsets) — zero everywhere else.
        EdgeInsets windowControlsInsets = default)
    {
        var context = new LayoutContext(theme, measurer ?? ApproximateTextMeasurer.Instance, typeScale,
            density)
        {
            PathCache = pathCache,
            Pool = nodePool,
            // The HOST's cutouts. Zero on a desktop window, and the shell's real numbers on a phone
            // — the tree is the same either way, which is the point of the node.
            SafeAreaInsets = safeAreaInsets,
            WindowControlsInsets = windowControlsInsets,
            Instances = instances,
            SizeClass = WindowSizeClasses.FromWidth(viewportWidth),
            Transitions = transitions,
            Presences = presences,
            TimeMs = timeMs,
            ReducedMotion = reducedMotion,
            ScrollOffsets = scrollOffsets,
            Drags = drags,
            ScrollMeta = new Dictionary<ScrollView, (string, float)>(),
        };
        transitions?.BeginFrame();
        presences?.BeginFrame();
        drags?.BeginFrame();
        // The BASE layer is the page's body: an auto-sized root stretches to the real containing
        // block — the window — the way a CSS block does, and ONLY in width: block height hugs
        // (pages grow downward; a full-height page asks for Height = Fill). Overlay layers below
        // keep shrink-to-fit on both axes (position:fixed semantics), which is what lets a
        // dropdown panel hug its options while the page behind it still fills the viewport.
        var layout = LayoutEngine.Layout(root, viewportWidth, viewportHeight, context,
            rootStretch: StretchKind.Block);

        var hits = new List<HitRegion>();
        var hovers = new List<HoverRegion>();
        var scrolls = new List<ScrollRegion>();
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
        var overlays = new List<Overlay>();
        var dragRegions = new List<DragRegion>();
        var links = new List<LinkRegion>();
        var shortcuts = new List<ShortcutBinding>();
        var texts = new List<TextRegion>();
        var stops = new List<FocusStop>();
        var codes = new List<CodeRegion>();
        var sheets = new List<SheetRegion>();
        var cursors = new List<CursorRegion>();
        var canvases = new List<CanvasRegion>();
        var input = new InputSink(hits, hovers, scrolls, dragRegions, links, shortcuts, texts, stops, codes, sheets, cursors, canvases);
        EmitVisitor.Shared.Emit(new EmitState(layout, input, new PressScope(pressed, focused, hovered, pressedPath, focusedPath, textPath, caretIndex, caretVisible, selectionStart, selectionEnd, density, hoveredPaths) { ScrollOffset = scrollOffset, MarkedText = markedText, Surface = new Rect(0, 0, viewportWidth, viewportHeight), InView = inViewStore },
            theme, mode, builder, context.ScrollMeta!, motion, overlays));

        // Overlay pass (Phase C): each queued layer lays out against the VIEWPORT and paints ABOVE
        // the page (painter's order); its hit regions register after the page's, so the topmost-
        // last-wins dispatch routes taps to the layer — a full-viewport scrim Pressable in the
        // layer blocks (and optionally handles) everything behind it.
        var overlayRoots = new List<LayoutNode>();
        for (var i = 0; i < overlays.Count; i++)
        {
            var overlayLayout = LayoutEngine.Layout(overlays[i].Child, viewportWidth, viewportHeight,
                context, rootPath: $"ov{i}");
            overlayRoots.Add(overlayLayout);
            // The UNCLIPPED sink: a layer lays out against the viewport, not inside whatever the
            // page happens to be scrolling.
            EmitVisitor.Shared.Emit(new EmitState(overlayLayout, input, new PressScope(pressed, focused, hovered, pressedPath, focusedPath, textPath, caretIndex, caretVisible, selectionStart, selectionEnd, density, hoveredPaths) { ScrollOffset = scrollOffset, MarkedText = markedText, Surface = new Rect(0, 0, viewportWidth, viewportHeight), InView = inViewStore },
                theme, mode, builder, context.ScrollMeta!, motion, overlays));
        }

        // Presence pruning runs AFTER the overlay pass — overlay paths ("ov<i>/…") register there,
        // and a pruned-too-early path would replay its entrance every frame. Each departure spawns
        // an EXIT from its last snapshot; mid-flight exits replay here, ABOVE everything (departed
        // layers were topmost), as pixels only — no hit regions, input passes through immediately.
        presences?.EndFrame(timeMs);
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
        context.Instances?.EndPass();
        return new RealizeResult(layout, hits,
            motion.Active || transitions is { AnyActive: true } || presences is { AnyActive: true }
                || drags is { AnyActive: true },
            hovers, scrolls, dragRegions, links, shortcuts, texts, stops, codes, overlayRoots, overlays,
            sheets, cursors, canvases);
    }


}
