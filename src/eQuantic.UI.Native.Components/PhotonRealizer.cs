using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>A pressable region registered by the realizer — hit rect expanded to the §08 contract.</summary>
/// <param name="Path">Where the pressable sits in the tree. A press outlives the frame it began in
/// — the pressed state repaints, and the next Build makes fresh nodes — so the target is remembered
/// by PATH. Remembering the object meant every press that spanned a frame quietly did nothing, and
/// a real click always spans one.</param>
public readonly record struct HitRegion(Rect Bounds, Pressable Node, string Path = "");

/// <summary>Spec S5/gestures: a hover-reactive region — a Box carrying a Hover diff. The host's
/// pointer tracking resolves the TOPMOST region under the pointer (paint order = registration
/// order, so last-contains wins).</summary>
public readonly record struct HoverRegion(Rect Bounds, VisualNode Node);

/// <summary>Scroll compositor v1: a scrollable viewport — the host routes wheel/drag input to the
/// TOPMOST region under the pointer and adjusts its stored offset (clamped to MaxOffset).</summary>
public readonly record struct ScrollRegion(Rect Bounds, string Path, float MaxOffset, ScrollAxis Axis, float Fallback);

/// <summary>Gestures v2: a drag-to-dismiss surface — the host tracks a press that travels past the
/// slop as a vertical drag on the TOPMOST region under the start point (paint-order last-wins).</summary>
/// <summary>
/// A surface the host routes a drag to. <see cref="Node"/> is the gesture node itself — a
/// <see cref="DragDismiss"/> or a <see cref="Draggable"/> — because the RULES (axis, limits, what a
/// release means) belong to it, and duplicating them here would be a second place to get them wrong.
/// </summary>
public readonly record struct DragRegion(Rect Bounds, string Path, VisualNode Node);

/// <summary>A navigation surface (the write-once Link): a tap no pressable claims resolves to the
/// TOPMOST link region under the point, through the host's navigation seam.</summary>
public readonly record struct LinkRegion(Rect Bounds, Link Node);

/// <summary>Spec S8: a keyboard binding that is live because its subtree is on screen — the host
/// dispatches a key press to the LAST registered match (the dialog on top wins the chord).</summary>
public readonly record struct ShortcutBinding(KeyChord Chord, Action OnPressed);

/// <summary>An editable field. A text entry is not a pressable — a click puts a CARET in it and the
/// keys that follow belong to it — so it registers its own kind of region, and the host keeps the
/// caret against the <paramref name="Path"/> for the same reason the press does: the tree is rebuilt
/// on every keystroke.</summary>
public readonly record struct TextRegion(Rect Bounds, TextEntry Entry, string Path);

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
    Adjustable? Adjustable = null);

/// <summary>The realized frame: the laid-out tree (absolute bounds) and the interactive hit regions.</summary>
public sealed class RealizeResult
{
    public RealizeResult(LayoutNode root, IReadOnlyList<HitRegion> hitRegions, bool hasActiveMotion,
        IReadOnlyList<HoverRegion>? hoverRegions = null, IReadOnlyList<ScrollRegion>? scrollRegions = null,
        IReadOnlyList<DragRegion>? dragRegions = null, IReadOnlyList<LinkRegion>? linkRegions = null,
        IReadOnlyList<ShortcutBinding>? shortcuts = null, IReadOnlyList<TextRegion>? textRegions = null,
        IReadOnlyList<FocusStop>? focusStops = null)
    {
        Root = root;
        HitRegions = hitRegions;
        HasActiveMotion = hasActiveMotion;
        HoverRegions = hoverRegions ?? Array.Empty<HoverRegion>();
        ScrollRegions = scrollRegions ?? Array.Empty<ScrollRegion>();
        DragRegions = dragRegions ?? Array.Empty<DragRegion>();
        LinkRegions = linkRegions ?? Array.Empty<LinkRegion>();
        Shortcuts = shortcuts ?? Array.Empty<ShortcutBinding>();
        TextRegions = textRegions ?? Array.Empty<TextRegion>();
        FocusStops = focusStops ?? Array.Empty<FocusStop>();
    }

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
        Density density = Density.Comfortable)
    {
        var context = new LayoutContext(theme, measurer ?? ApproximateTextMeasurer.Instance, typeScale,
            density)
        {
            // The HOST's cutouts. Zero on a desktop window, and the shell's real numbers on a phone
            // — the tree is the same either way, which is the point of the node.
            SafeAreaInsets = safeAreaInsets,
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
        // One-shot flag: the root consumes it.
        context.StretchWidth = StretchKind.Block;
        var layout = LayoutEngine.Layout(root, viewportWidth, viewportHeight, context);

        var hits = new List<HitRegion>();
        var hovers = new List<HoverRegion>();
        var scrolls = new List<ScrollRegion>();
        var motion = new MotionScope(timeMs, reducedMotion)
        {
            Presences = presences,
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
        var input = new InputSink(hits, hovers, scrolls, dragRegions, links, shortcuts, texts, stops);
        Emit(layout, theme, mode, builder, input, context.ScrollMeta!, new PressScope(pressed, focused, hovered, pressedPath, focusedPath, textPath, caretIndex, caretVisible, selectionStart, selectionEnd, density), motion, overlays);

        // Overlay pass (Phase C): each queued layer lays out against the VIEWPORT and paints ABOVE
        // the page (painter's order); its hit regions register after the page's, so the topmost-
        // last-wins dispatch routes taps to the layer — a full-viewport scrim Pressable in the
        // layer blocks (and optionally handles) everything behind it.
        for (var i = 0; i < overlays.Count; i++)
        {
            var overlayLayout = LayoutEngine.Layout(overlays[i].Child, viewportWidth, viewportHeight,
                context, rootPath: $"ov{i}");
            // The UNCLIPPED sink: a layer lays out against the viewport, not inside whatever the
            // page happens to be scrolling.
            Emit(overlayLayout, theme, mode, builder, input, context.ScrollMeta!, new PressScope(pressed, focused, hovered, pressedPath, focusedPath, textPath, caretIndex, caretVisible, selectionStart, selectionEnd, density), motion, overlays);
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
            hovers, scrolls, dragRegions, links, shortcuts, texts, stops);
    }

    /// <summary>The frame clock for loop motion: offsets resolve as a PURE function of
    /// <see cref="TimeMs"/> (deterministic frames — goldens pin a fixed t). Reduce Motion (spec §06)
    /// replaces movement statically: the child renders at rest and the frame reports no active
    /// motion, so the host stops burning frames.</summary>
    private sealed class MotionScope
    {
        public MotionScope(float timeMs, bool reduced)
        {
            TimeMs = timeMs;
            Reduced = reduced;
        }

        public float TimeMs { get; }
        public bool Reduced { get; }

        /// <summary>Wave 3: anchored panels position against the viewport (Top/End placements).</summary>
        public float ViewportW { get; init; }
        public float ViewportH { get; init; }

        /// <summary>W4: the platform text service + per-host raster cache (null = placeholder bars).</summary>
        public Framework.ITextRasterizer? TextRasterizer { get; init; }
        public TextRasterCache? TextCache { get; init; }
        public Framework.IImageLoader? ImageLoader { get; init; }
        public Dictionary<string, TextureData?>? ImageCache { get; init; }

        /// <summary>W4: the platform icon service + per-host raster cache (null = disc placeholder).</summary>
        public Framework.IIconRasterizer? IconRasterizer { get; init; }
        public IconRasterCache? IconCache { get; init; }
        public float RenderScale { get; init; } = 1f;
        public float TypeScale { get; init; } = 1f;
        public bool Active { get; set; }

        /// <summary>The host's presence clock — the emit pass snapshots each live presence subtree's
        /// commands into it (the exit replay source). Null = no exit machinery (layout-only tests).</summary>
        public PresenceStore? Presences { get; init; }
    }

    /// <summary>Carries the held press through the emit walk: entering the pressed Pressable arms the
    /// token swap, and the FIRST descendant Box consumes it (the component convention: Pressable → Box
    /// carries the fill — the spec's "token swap on the same rrect").</summary>
    private sealed class PressScope
    {
        public PressScope(Pressable? pressed, Pressable? focused, VisualNode? hovered = null,
            string? pressedPath = null, string? focusedPath = null,
            string? textPath = null, int caretIndex = 0, bool caretVisible = true,
            int selectionStart = 0, int selectionEnd = 0,
            Density density = Density.Comfortable)
        {
            Density = density;
            SelectionStart = selectionStart;
            SelectionEnd = selectionEnd;
            Pressed = pressed;
            Focused = focused;
            Hovered = hovered;
            PressedPath = pressedPath;
            FocusedPath = focusedPath;
            TextPath = textPath;
            CaretIndex = caretIndex;
            CaretVisible = caretVisible;
        }

        /// <summary>The selected range in the field under edit (equal = no selection).</summary>
        public int SelectionStart { get; }
        public int SelectionEnd { get; }

        /// <summary>The field under edit, its caret, and whether the caret is in its ON blink.</summary>
        public string? TextPath { get; }
        public int CaretIndex { get; }
        public bool CaretVisible { get; }

        /// <summary>The target's density — what the hit-rect expansion answers to.</summary>
        public Density Density { get; }

        public Pressable? Pressed { get; }
        public Pressable? Focused { get; }

        /// <summary>Where the held press and the focus LIVE, which is what survives a rebuild. The
        /// node reference is kept alongside as the answer for a single frame that never rebuilt.</summary>
        public string? PressedPath { get; }
        public string? FocusedPath { get; }

        /// <summary>True when this node is the one being tracked: by path when there is one (the
        /// tree may have been rebuilt since), by reference otherwise.</summary>
        public bool IsTracked(LayoutNode node, VisualNode? tracked, string? trackedPath) =>
            trackedPath is { Length: > 0 }
                ? node.Path == trackedPath
                : tracked is not null && ReferenceEquals(node.Source, tracked);

        /// <summary>Spec S5: the node the pointer is over — its Box applies its Hover diff. Fed by
        /// the host's pointer tracking (the gesture slice); tests pass it directly.</summary>
        public VisualNode? Hovered { get; }
        public ColorToken? PendingFill { get; set; }
        public bool PendingFocusRing { get; set; }
    }

    private static void Emit(LayoutNode node, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, InputSink input, Dictionary<ScrollView, (string Path, float MaxOffset)> scrollMeta, PressScope press, MotionScope motion, List<Overlay> overlays)
    {
        // Spec S1 — group opacity + static transform wrap the WHOLE box (chrome and children):
        // opacity is one PushLayer composite (overlaps never double-blend); the transform is the
        // center-anchored Matrix2D twin of the CSS list, paint-only (layout already ran).
        if (node.Source is Box styled &&
            (styled.Style.Opacity is { } sAlpha && sAlpha < 1f || styled.Style.Transform is { IsIdentity: false }))
        {
            var opacity = styled.Style.Opacity is { } a && a < 1f ? a : (float?)null;
            if (opacity is { } layerAlpha) builder.PushLayer(layerAlpha);
            var transformed = styled.Style.Transform is { IsIdentity: false } t;
            if (transformed)
                builder.PushTransform(CenterAnchored(styled.Style.Transform!.Value, node.Bounds.Center));

            EmitNode(node, theme, mode, builder, input, scrollMeta, press, motion, overlays);

            if (transformed) builder.Pop();
            if (opacity is not null) builder.PopLayer();
            return;
        }

        EmitNode(node, theme, mode, builder, input, scrollMeta, press, motion, overlays);
    }

    /// <summary>
    /// W4 images: decode through the platform loader (cached per source), draw as an Rgba8 Texture
    /// command with the FIT math — Stretch fills, Contain letter-boxes centered, Cover fills and
    /// CLIPS to the node's rrect. No loader / failed decode → the SurfaceSubtle placeholder box.
    /// Nearest sampling v1 (bilinear is the scaling-quality fence).
    /// </summary>
    private static void EmitImage(LayoutNode node, Image image, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        TextureData? data = null;
        if (motion.ImageLoader is { } loader)
        {
            var cache = motion.ImageCache;
            if (cache is null || !cache.TryGetValue(image.Source, out data))
            {
                var decoded = loader.Load(image.Source);
                data = decoded is null ? null : TextureData.Rgba(decoded.Width, decoded.Height, decoded.Rgba);
                cache?[image.Source] = data;
            }
        }
        if (data is null)
        {
            builder.FillRRect(new RRect(node.Bounds, image.CornerRadius),
                Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
            return;
        }

        var b = node.Bounds;
        Rect dest;
        var clip = !image.CornerRadius.IsZero;
        switch (image.Fit)
        {
            case ImageFit.Stretch:
                dest = b;
                break;
            case ImageFit.Contain:
            {
                var scale = MathF.Min(b.Width / data.Width, b.Height / data.Height);
                var w = data.Width * scale;
                var h = data.Height * scale;
                dest = new Rect(b.X + (b.Width - w) / 2, b.Y + (b.Height - h) / 2, w, h);
                break;
            }
            default: // Cover — fills and overflows; always clipped to the bounds.
            {
                var scale = MathF.Max(b.Width / data.Width, b.Height / data.Height);
                var w = data.Width * scale;
                var h = data.Height * scale;
                dest = new Rect(b.X + (b.Width - w) / 2, b.Y + (b.Height - h) / 2, w, h);
                clip = true;
                break;
            }
        }

        if (clip) builder.PushClip(new RRect(b, image.CornerRadius));
        builder.Texture(dest, new Color(255, 255, 255, 255), data);
        if (clip) builder.PopClip();
    }

    /// <summary>Centers a panel on the anchor's X WITHOUT measuring it: a symmetric fixed-width
    /// row around the center point with Main=Center (panels wider than 2× the center overflow —
    /// the documented flip/clamp fence).</summary>
    private static VisualNode CenteredOn(VisualNode panel, float centerX)
    {
        var row = new Row(gap: 0) { Main = MainAlign.Center, Width = 2 * centerX };
        row.Add(panel);
        return row;
    }

    /// <summary>The CSS transform list twin: translate → rotate → scale, anchored at the box center.</summary>
    private static Matrix2D CenterAnchored(in Transform2D t, Point center) =>
        Matrix2D.Translation(-center.X, -center.Y)
        * Matrix2D.Scale(t.ScaleX, t.ScaleY)
        * Matrix2D.Rotation(t.RotationDegrees * MathF.PI / 180f)
        * Matrix2D.Translation(center.X + t.TranslateX, center.Y + t.TranslateY);

    private static void EmitNode(LayoutNode node, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, InputSink input, Dictionary<ScrollView, (string Path, float MaxOffset)> scrollMeta, PressScope press, MotionScope motion, List<Overlay> overlays)
    {
        if (press.IsTracked(node, press.Pressed, press.PressedPath) && press.Pressed?.PressedBackground is { } pressedFill)
            press.PendingFill = pressedFill;
        if ((press.Focused is not null || press.FocusedPath is not null)
            && press.IsTracked(node, press.Focused, press.FocusedPath))
            press.PendingFocusRing = true;

        switch (node.Source)
        {
            case Box box:
            {
                // Spec S3 frosted glass: the backdrop blurs FIRST — under the shadow and the box's
                // own translucent fill (the engine consumes this as a pass split).
                if (box.Style.BackdropBlur > 0)
                    builder.BackdropBlur(new RRect(node.Bounds, box.Style.CornerRadius), box.Style.BackdropBlur);

                // Spec S6 FENCE: `BoxStyle.Transition` (and its Anchored/Sticky/Text siblings) is a
                // WEB-only glide today — native SNAPS to each new value until the style interpolator
                // lands. Honesty over smoothness, the same fence Flexible.AnimateChanges documents.
                // Spec S1 FENCE: a gradient's `Via` midpoint is ignored — the shader interpolates two
                // stops, so native paints From→To until the 3-stop paint lands.

                // §05: the analytic shadow draws under the fill (one per node, theme-resolved).
                if (box.Style.Elevation > 0)
                {
                    var spec = theme.Elevation(box.Style.Elevation);
                    if (!spec.IsNone)
                    {
                        builder.ShadowRRect(new RRect(node.Bounds, box.Style.CornerRadius),
                            spec.OffsetY, spec.Blur, spec.Spread, spec.Color.Resolve(mode));
                    }
                }

                // CUSTOM shadow (glows, halos): the same analytic rrect shadow with the caller's
                // full spec — composes with the neutral elevation above. Lists draw in order.
                if (box.Style.Shadow is { } custom)
                {
                    builder.ShadowRRect(new RRect(node.Bounds, box.Style.CornerRadius),
                        custom.OffsetY, custom.Blur, custom.Spread, custom.Color.Resolve(mode));
                }
                if (box.Style.Shadows is { Count: > 0 } customList)
                {
                    foreach (var entry in customList)
                        builder.ShadowRRect(new RRect(node.Bounds, box.Style.CornerRadius),
                            entry.OffsetY, entry.Blur, entry.Spread, entry.Color.Resolve(mode));
                }

                var fill = press.PendingFill ?? box.Style.Background;
                press.PendingFill = null;
                var borderColor = box.Style.BorderColor;
                var borderWidth = box.Style.BorderWidth;
                // Spec S5: hover-reactive boxes register for the host's pointer tracking.
                if (box.Style.Hover is { IsEmpty: false })
                    input.Add(new HoverRegion(node.Bounds, box));

                // Spec S5: the hovered Box applies its Hover diff (pressed still wins on fill).
                if (ReferenceEquals(node.Source, press.Hovered) && box.Style.Hover is { IsEmpty: false } hover)
                {
                    if (press.PendingFill is null && hover.Background is { } hoverFill) fill = hoverFill;
                    if (hover.BorderColor is { } hoverBorder) borderColor = hoverBorder;
                    if (hover.BorderWidth is { } hoverWidth) borderWidth = hoverWidth;
                }
                EmitChrome(node.Bounds, fill, box.Style.CornerRadius,
                    borderColor, borderWidth, theme, mode, builder,
                    box.Style.Gradient, box.Style.Pattern, box.Style.Glow);

                // Focus double ring (spec §01): 2dp Surface gap + 2dp FocusRing OUTSIDE the control,
                // following the control's own radius — the first Box under the focused Pressable
                // carries it (the same convention as the pressed fill swap).
                if (press.PendingFocusRing)
                {
                    press.PendingFocusRing = false;
                    var radii = box.Style.CornerRadius;
                    builder.StrokeRRect(
                        new RRect(node.Bounds.Inflate(1), new CornerRadii(
                            radii.TopLeft + 1, radii.TopRight + 1, radii.BottomRight + 1, radii.BottomLeft + 1)),
                        2, Paint.Solid(theme.Surface.Resolve(mode)));
                    builder.StrokeRRect(
                        new RRect(node.Bounds.Inflate(3), new CornerRadii(
                            radii.TopLeft + 3, radii.TopRight + 3, radii.BottomRight + 3, radii.BottomLeft + 3)),
                        2, Paint.Solid(theme.FocusRing.Resolve(mode)));
                }
                break;
            }

            case FlexNode flex when flex.Background is not null:
                EmitChrome(node.Bounds, flex.Background, flex.CornerRadius, default, 0, theme, mode, builder);
                break;

            case Text text:
                EmitText(node, text, theme, mode, builder, motion);
                break;

            // Spec B9 fence: the entry renders the W4 one-line placeholder bar — value in
            // TextPrimary, empty shows the placeholder in TextMuted. Caret/selection/IME land at M4.
            case TextEntry entry:
                EmitEntry(node, entry, theme, mode, builder, input, press, motion);
                break;

            // Spec A11 fence: a SurfaceSubtle box under the radius stands in for the bitmap until the
            // engine gains texture upload (M4) - the documented placeholder pattern.
            case Image image:
                EmitImage(node, image, theme, mode, builder, motion);
                break;

            case CameraPreview camera:
                EmitCameraPreview(node, camera, theme, mode, builder, motion);
                break;

            // Spec A10, W4 fence: a tinted disc at 30% alpha stands in for the glyph until the atlas
            // lands — the same documented placeholder pattern as text bars.
            case Icon icon:
            {
                // W4: REAL glyph when the platform service is present — one A8 raster per
                // (glyph, size, scale), drawn as a tinted Texture command. No service → the
                // documented 30% disc placeholder (tests, headless).
                if (motion.IconRasterizer is { } icons
                    && (motion.IconCache ?? IconRasterCache.Shared)
                        .Get(icons, icon.Glyph, icon.Size, motion.RenderScale) is { } raster)
                {
                    var tint = (icon.Color ?? theme.TextPrimary).Resolve(mode);
                    builder.Texture(node.Bounds, tint, raster);
                    break;
                }
                var placeholder = (icon.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
                builder.FillRRect(
                    new RRect(node.Bounds, new CornerRadii(node.Bounds.Width / 2)),
                    Paint.Solid(placeholder));
                break;
            }

            case Vector vector:
            {
                // The vector rides the SAME rasterizer as an icon — one tinted A8 raster per
                // (glyph, size, scale). No service → the icon placeholder disc, same contract.
                if (motion.IconRasterizer is { } vectorIcons
                    && (motion.IconCache ?? IconRasterCache.Shared)
                        .Get(vectorIcons, vector.Glyph, vector.Size, motion.RenderScale) is { } vectorRaster)
                {
                    var vectorTint = (vector.Color ?? theme.TextPrimary).Resolve(mode);
                    builder.Texture(node.Bounds, vectorTint, vectorRaster);
                    break;
                }
                var vectorPlaceholder = (vector.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
                builder.FillRRect(
                    new RRect(node.Bounds, new CornerRadii(node.Bounds.Width / 2)),
                    Paint.Solid(vectorPlaceholder));
                break;
            }

            case Spinner spinner:
                EmitSpinner(node, spinner, theme, mode, builder, motion);
                break;

            case Pressable pressable:
                input.Add(new HitRegion(ExpandHitRect(node.Bounds, press.Density), pressable, node.Path ?? ""));
                break;

            // S5 programmable hover: the region rides the SAME pointer pipeline Style.Hover uses;
            // the host fires OnChanged on the transitions (PhotonHost.PointerMove).
            case Hoverable hoverable:
                input.Add(new HoverRegion(node.Bounds, hoverable));
                break;

            // ONE Tab stop for the whole control; the press targets inside it stay pointer-only —
            // stopping on "decrease half" and then "increase half" is two stops for one slider,
            // and neither of them answers to the arrows.
            case Adjustable adjustable:
                input.AddAdjustable(new FocusStop(node.Path ?? "", null, null, node.Bounds, adjustable));
                foreach (var child in node.Children)
                    Emit(child, theme, mode, builder, input.WithoutFocusStops(), scrollMeta, press, motion, overlays);
                return;

            // Spec S8: being on screen IS the subscription — the binding lives for exactly as long
            // as this frame, so an unmounted dialog's Esc stops firing with no bookkeeping.
            case Shortcut shortcut:
                input.Add(new ShortcutBinding(shortcut.Chord, shortcut.OnPressed));
                break;
        }

        // A ScrollView clips its subtree to the viewport (spec A6) — the engine clip primitive.
        if (node.Source is ScrollView scrollView)
        {
            // Scroll compositor v1: the host routes wheel/drag to the topmost region (paint order).
            if (scrollMeta.TryGetValue(scrollView, out var meta))
                input.Add(new ScrollRegion(node.Bounds, meta.Path, meta.MaxOffset, scrollView.Axis, scrollView.Offset));
            builder.PushClip(new RRect(node.Bounds));
            // …and the SUBTREE'S INPUT to the same rectangle. A row scrolled out of the viewport is
            // drawn nowhere, so it takes no taps — otherwise it keeps taking the ones aimed at
            // whatever the app shows there instead, and a fixed toolbar goes dead the moment the
            // list under it moves.
            var scrolled = input.Under(node.Bounds);
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, scrolled, scrollMeta, press, motion, overlays);
            builder.PopClip();
            return;
        }

        // A clipping Box confines its CHILDREN to its rrect (chrome already drew unclipped above) —
        // the container side of loop motion (the sweeping segment stays inside the track).
        if (node.Source is Box { Style.Clip: true } clipBox)
        {
            builder.PushClip(new RRect(node.Bounds, clipBox.Style.CornerRadius));
            var confined = input.Under(node.Bounds);
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, confined, scrollMeta, press, motion, overlays);
            builder.PopClip();
            return;
        }

        // An Overlay queues for the viewport pass — nothing renders in the page flow.
        if (node.Source is Overlay overlay)
        {
            overlays.Add(overlay);
            return;
        }

        // Wave 3 anchored overlay: the anchor renders in flow; while Open, a SYNTHETIC overlay
        // layer carries [full-viewport filler (scrim Pressable when dismissible) + the panel
        // Positioned from the anchor's ABSOLUTE bounds] — pure reuse of the overlay machinery,
        // so panel pressables, hit routing and painter's order all come for free.
        if (node.Source is Anchored anchored)
        {
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);

            // Wave 3b hover reveal (the Tooltip mechanism): the anchor registers for the host's
            // pointer tracking; the panel opens while hovered — no scrim (leave = closed).
            var hoverOpen = false;
            if (anchored.OpenOnHover)
            {
                input.Add(new HoverRegion(node.Bounds, anchored));
                hoverOpen = ReferenceEquals(press.Hovered, anchored);
            }
            if (!anchored.Open && !hoverOpen) return;

            // Mega-menu dimming: ScrimStyle paints the outside-tap scrim (a full Box — veil
            // gradient, backdrop blur) instead of the invisible filler.
            var filler = new Box((anchored.ScrimStyle ?? default) with { Width = SizeValue.Fill, Height = SizeValue.Fill });
            var layer = new Stack();
            // Hover-open panels take no scrim: leaving the anchor closes them.
            layer.Add(anchored.OnDismiss is { } dismiss && !hoverOpen
                ? new Pressable(filler, dismiss) { Label = "Dismiss" }
                : filler);
            // Same rule for an anchored panel — a menu you opened with the keyboard has to be
            // closable with it. Hover-open panels are excluded: leaving closes them, and there is
            // nothing for Escape to do.
            if (anchored.OnDismiss is { } escapable && !hoverOpen)
                input.Add(new ShortcutBinding(KeyChord.Escape, escapable));

            // The MinWidth goes ON the panel's own box when there is one, not on a wrapper around
            // it: a hugging wrapper clamps its own frame and its hugging child re-measures at
            // intrinsic width inside it — a 550dp panel whose option rows stayed 178dp wide. On
            // the panel itself, the Min/Max reflow hands the final width down to the rows, which
            // is also exactly what the web's min-width:100% does.
            var panel = !anchored.MatchAnchorWidth ? anchored.Panel
                : anchored.Panel is Box panelBox
                    ? new Box(panelBox.Style with
                    {
                        MinWidth = MathF.Max(panelBox.Style.MinWidth, node.Bounds.Width),
                    }, panelBox.Child)
                    : new Box(new BoxStyle { MinWidth = node.Bounds.Width }, anchored.Panel);
            var b = node.Bounds;
            var gap = anchored.Gap;
            layer.Add(anchored.Placement switch
            {
                AnchorPlacement.BottomEnd => new Positioned(panel, top: b.Bottom + gap, end: motion.ViewportW - b.Right),
                AnchorPlacement.TopStart => new Positioned(panel, bottom: motion.ViewportH - b.Y + gap, start: b.X),
                AnchorPlacement.TopEnd => new Positioned(panel, bottom: motion.ViewportH - b.Y + gap, end: motion.ViewportW - b.Right),
                AnchorPlacement.BottomCenter => new Positioned(CenteredOn(panel, b.Center.X), top: b.Bottom + gap, start: 0),
                AnchorPlacement.TopCenter => new Positioned(CenteredOn(panel, b.Center.X), bottom: motion.ViewportH - b.Y + gap, start: 0),
                _ => new Positioned(panel, top: b.Bottom + gap, start: b.X),
            });
            overlays.Add(new Overlay(layer));
            return;
        }

        // Loop motion: translate the subtree by the frame-clock offset (spec §06 transform-only).
        // Offsets are fractions of the node's OWN laid-out width — parity with CSS translateX(%).
        if (node.Source is LoopMotion loop)
        {
            // Decorative loops (Skeleton shimmer) disappear under Reduce Motion — the spec's
            // static-placeholder behavior; positional loops render a still frame instead.
            if (motion.Reduced && loop.HideAtRest) return;
            var offset = ResolveLoopOffset(loop, node.Bounds.Width, motion);
            if (offset != 0) builder.PushTransform(Matrix2D.Translation(offset, 0));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);
            if (offset != 0) builder.Pop();
            return;
        }

        if (node.Source is Link link)
        {
            // Navigation surface: pure semantics — the child paints; a tap that no pressable claims
            // resolves to this region through the host's navigation seam.
            input.Add(new LinkRegion(node.Bounds, link));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);
            return;
        }

        if (node.Source is (DragDismiss or Draggable) && node.DragPath is { } dragPath)
        {
            // Gestures v2: the surface registers for the host's drag routing, and the current offset
            // (active follow or glide-back) paints as a translate — layout untouched, exactly like
            // loop motion. Hit regions inside register at their laid-out bounds; mid-drag taps are
            // cancelled by the slop rule, so the transient misalignment is unreachable.
            input.Add(new DragRegion(node.Bounds, dragPath, node.Source));
            // The axis is the node's: a swipe-to-reveal travels sideways, a sheet down.
            var dragOffset = node.DragOffset;
            var horizontal = node.Source is Draggable { Axis: DragAxis.Horizontal };
            if (dragOffset != 0)
            {
                builder.PushTransform(horizontal
                    ? Matrix2D.Translation(dragOffset, 0)
                    : Matrix2D.Translation(0, dragOffset));
            }
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);
            if (dragOffset != 0) builder.Pop();
            return;
        }

        if (node.Source is Presence presence)
        {
            // Enter motion (spec §06): a mid-entrance subtree paints inside a GROUP-opacity layer
            // (CSS-opacity semantics — overlapping children never double-blend) with the SlideUp
            // rise as a paint-only translate. Reduce Motion drops the movement (the store already
            // shortened the clock to the crossfade) — fade only, exactly the web media query.
            // Settled subtrees paint plainly (no layer cost at rest). Either way, the commands are
            // SNAPSHOTTED by path — the frame that finds this path gone replays them as the exit
            // (a mid-enter departure carries its inner layer, so the cross-fade composes).
            var start = builder.CommandCount;
            var entering = node.Presence < 1f;
            var rise = entering && presence.Enter == PresenceMotion.SlideUp && !motion.Reduced
                ? (1f - node.Presence) * Presence.SlideDistance
                : 0f;
            if (rise != 0) builder.PushTransform(Matrix2D.Translation(0, rise));
            if (entering) builder.PushLayer(node.Presence);
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);
            if (entering) builder.PopLayer();
            if (rise != 0) builder.Pop();
            if (motion.Presences != null && node.PresencePath is { } presencePath)
                motion.Presences.Snapshot(presencePath, presence.Enter, builder.CommandsFrom(start));
            return;
        }

        foreach (var child in node.Children)
            Emit(child, theme, mode, builder, input, scrollMeta, press, motion, overlays);
    }

    /// <summary>The loop offset at the scope's clock: linear phase over the period, lerped between
    /// the fractional endpoints, scaled by the node's own width. Reduce Motion → at-rest (0) and the
    /// frame does not report active motion.</summary>
    private static float ResolveLoopOffset(LoopMotion loop, float width, MotionScope motion)
    {
        if (motion.Reduced || loop.DurationMs <= 0 || width <= 0) return 0;
        motion.Active = true;
        var phase = motion.TimeMs % loop.DurationMs / loop.DurationMs;
        if (phase < 0) phase += 1;
        return (loop.FromX + (loop.ToX - loop.FromX) * phase) * width;
    }

    private static void EmitChrome(Rect bounds, ColorToken? background, CornerRadii radius,
        ColorToken borderColor, float borderWidth, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder,
        LinearGradient? gradient = null, GridPattern? pattern = null, RadialGradient? glow = null)
    {
        if (bounds.IsEmpty) return;

        if (background is { } bg)
        {
            var color = bg.Resolve(mode);
            if (color.A > 0)
                builder.FillRRect(new RRect(bounds, radius), Paint.Solid(color));
        }

        // The grid draws over the solid and UNDER the gradient — the same layer order the web
        // realizer's background-image list produces. Hairlines are ordinary fills bounded by the
        // box: no engine primitive and no shader, which is what keeps this write-once today.
        if (pattern is { } grid && grid.Cell > 0 && grid.LineWidth > 0)
            EmitGridPattern(bounds, grid, mode, builder);

        // The spotlight sits above the grid and below the linear gradient — the same stacking the
        // web realizer's layer list produces (there the FIRST entry is topmost; here the LAST drawn
        // is). The center is a fraction of the box, so it tracks a resize without recomputation.
        if (glow is { } g2)
        {
            builder.FillRRect(new RRect(bounds, radius), Paint.Radial(
                new Point(bounds.X + bounds.Width * g2.CenterX, bounds.Y + bounds.Height * g2.CenterY),
                g2.RadiusX, g2.RadiusY,
                g2.From.Resolve(mode), g2.To.Resolve(mode)));
        }

        // The gradient draws OVER the solid (CSS background-image/background-color composition):
        // Paint.Linear across the box bounds on the declared axis, stops resolved per mode.
        if (gradient is { } g)
        {
            // The axis is a pair of points, so the diagonals need no new engine primitive — only
            // the right corners. ToBottomLeft starts at the TOP-RIGHT corner (CSS's `to bottom left`
            // runs from the opposite corner), which is why the start point is not always the origin.
            var (start, end) = g.Direction switch
            {
                GradientDirection.ToBottom => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X, bounds.Y + bounds.Height)),
                GradientDirection.ToBottomRight => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height)),
                GradientDirection.ToBottomLeft => (
                    new Point(bounds.X + bounds.Width, bounds.Y),
                    new Point(bounds.X, bounds.Y + bounds.Height)),
                _ => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X + bounds.Width, bounds.Y)),
            };
            builder.FillRRect(new RRect(bounds, radius),
                Paint.Linear(start, end, g.From.Resolve(mode), g.To.Resolve(mode)));
        }

        if (borderWidth > 0)
        {
            var color = borderColor.Resolve(mode);
            if (color.A > 0)
            {
                // Borders draw INSIDE the bounds (spec fence). The engine stroke is centered, so a
                // centered stroke on the half-width-deflated shape covers exactly [0, w] inward.
                builder.StrokeRRect(
                    new RRect(bounds.Inflate(-borderWidth / 2), radius.Deflate(borderWidth / 2)),
                    borderWidth,
                    Paint.Solid(color));
            }
        }
    }

    /// <summary>
    /// The <see cref="GridPattern"/> hairlines, matching the CSS layers exactly: rules START at the
    /// box origin and repeat every <c>Cell</c> dp — the same phase as a <c>background-size</c> tile,
    /// so a grid straddling both realizers lands on the same pixels. Lines are clipped to the box by
    /// construction (each rule is sized to the bounds), and a degenerate cell emits nothing rather
    /// than looping forever.
    /// </summary>
    private static void EmitGridPattern(Rect bounds, GridPattern pattern, ThemeMode mode, DisplayListBuilder builder)
    {
        var color = pattern.Color.Resolve(mode);
        if (color.A == 0) return;

        var paint = Paint.Solid(color);
        var line = pattern.LineWidth;

        for (var x = bounds.X; x < bounds.X + bounds.Width; x += pattern.Cell)
            builder.FillRect(new Rect(x, bounds.Y, line, bounds.Height), paint);

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y += pattern.Cell)
            builder.FillRect(new Rect(bounds.X, y, bounds.Width, line), paint);
    }

    /// <summary>
    /// W4-pending placeholder: until the HarfBuzz/FreeType text stack lands, a text run renders as one
    /// soft bar per measured line (55% of the line box, text color at 30% alpha) — deterministic,
    /// verifies layout geometry in goldens, and is unmistakably a placeholder. Regenerating goldens
    /// when real glyphs arrive is by design.
    /// </summary>
    /// <summary>
    /// W4: REAL text when the platform service is present — one A8 raster per block (cached by
    /// content/style/width/scale; the tint carries the color, so one raster serves both modes),
    /// drawn as a single Texture command over the node bounds. No service → the deterministic
    /// placeholder bars (tests, headless).
    /// </summary>
    private static void EmitText(LayoutNode node, Text text, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        if (motion.TextRasterizer is { } rasterizer && node.Text is { } measured)
        {
            var style = text.StyleOverride ?? theme.Type(text.Role);
            if (text.Mono) style = style with { Mono = true };
            var raster = (motion.TextCache ?? TextRasterCache.Shared).Get(
                rasterizer, text.PlainContent, style, motion.TypeScale, node.Bounds.Width, text.MaxLines, motion.RenderScale);
            if (raster is not null)
            {
                // The bitmap may carry ink ABOVE the line box (a tall ascender, an accent); it
                // was grown upward, so the draw rises by the same amount and the line box itself
                // still sits exactly where layout put it.
                var rect = new Rect(node.Bounds.X, node.Bounds.Y - raster.PadTop / motion.RenderScale,
                    raster.Texture.Width / motion.RenderScale, raster.Texture.Height / motion.RenderScale);
                if (text.Gradient is { } g)
                {
                    // Gradient text: the glyph coverage is tinted by a PAINT. The axis spans the
                    // text's own box on the declared direction, matching what `background-clip:text`
                    // does on web (the gradient box is the element).
                    var end = g.Direction switch
                    {
                        GradientDirection.ToBottom => new Point(rect.X, rect.Y + rect.Height),
                        GradientDirection.ToBottomRight => new Point(rect.X + rect.Width, rect.Y + rect.Height),
                        GradientDirection.ToBottomLeft => new Point(rect.X, rect.Y + rect.Height),
                        _ => new Point(rect.X + rect.Width, rect.Y),
                    };
                    var start = g.Direction == GradientDirection.ToBottomLeft
                        ? new Point(rect.X + rect.Width, rect.Y)
                        : new Point(rect.X, rect.Y);
                    builder.Texture(rect,
                        Paint.Linear(start, end, g.From.Resolve(mode), g.To.Resolve(mode)),
                        raster.Texture);
                    return;
                }

                var color = (text.Color ?? theme.TextPrimary).Resolve(mode);
                builder.Texture(rect, color, raster.Texture);
                return;
            }
        }
        EmitTextPlaceholder(node, text, theme, mode, builder);
    }

    private static void EmitTextPlaceholder(LayoutNode node, Text text, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (node.Text is not { } measurement) return;
        var color = (text.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
        var barHeight = measurement.LineHeight * 0.55f;

        for (var i = 0; i < measurement.Lines.Count; i++)
        {
            var line = measurement.Lines[i];
            if (line.Width <= 0) continue;
            var y = node.Bounds.Y + i * measurement.LineHeight + (measurement.LineHeight - barHeight) / 2;
            builder.FillRRect(
                new RRect(new Rect(node.Bounds.X, y, MathF.Min(line.Width, node.Bounds.Width), barHeight),
                    new CornerRadii(barHeight / 3)),
                Paint.Solid(color));
        }
    }

    /// <summary>
    /// The live camera surface. The session mutates ONE byte array and bumps a version; this keeps
    /// ONE TextureData wrapping that array and mirrors the version, so the renderer's identity
    /// cache re-uploads the same GPU slot instead of minting a leaked texture per frame. While a
    /// session is on screen the frame clock keeps turning — video IS motion.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ICameraSession, TextureData>
        LiveFrames = new();

    private static void EmitCameraPreview(LayoutNode node, CameraPreview camera, IAppTheme theme,
        ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        var rrect = new RRect(node.Bounds, camera.CornerRadius);
        if (camera.Session is not { FrameBytes: { } bytes, FrameWidth: > 0 } session)
        {
            // Not started (or no pixels yet): the same SurfaceSubtle placeholder Image degrades to.
            builder.FillRRect(rrect, Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
            return;
        }

        motion.Active = true;   // frames keep arriving; the host must keep asking for them

        if (!LiveFrames.TryGetValue(session, out var texture)
            || texture.Width != session.FrameWidth || texture.Height != session.FrameHeight
            || !ReferenceEquals(texture.Alpha, bytes))
        {
            texture = TextureData.Rgba(session.FrameWidth, session.FrameHeight, bytes);
            LiveFrames.Remove(session);
            LiveFrames.Add(session, texture);
        }
        texture.Version = session.FrameVersion;

        builder.PushClip(rrect);
        builder.Texture(CoverRect(node.Bounds, session.FrameWidth, session.FrameHeight),
            Color.White, texture);
        builder.PopClip();
    }

    /// <summary>Center-crop: the rect that fills the slot at the source's aspect (ImageFit.Cover).</summary>
    private static Rect CoverRect(in Rect slot, float sourceW, float sourceH)
    {
        var scale = MathF.Max(slot.Width / sourceW, slot.Height / sourceH);
        var w = sourceW * scale;
        var h = sourceH * scale;
        return new Rect(slot.X + (slot.Width - w) / 2, slot.Y + (slot.Height - h) / 2, w, h);
    }

    /// <summary>The TextEntry stand-in (spec B9 fence): one soft bar per the W4 text placeholder
    /// convention — the VALUE in the entry's text color, an empty value shows the PLACEHOLDER in
    /// TextMuted. Deterministic layout geometry until the real text stack (M4).</summary>
    /// <summary>
    /// An editable field: its text, its caret, and the region that makes it clickable.
    /// <para>
    /// The bar this used to draw was the W4 fence, kept long after the fence came down — every other
    /// string on screen was already rastering real glyphs. A field that shows a grey bar instead of
    /// what you typed is not a placeholder for a missing feature; it is a field nobody can use.
    /// </para>
    /// </summary>
    private static void EmitEntry(LayoutNode node, TextEntry entry, IAppTheme theme, ThemeMode mode,
        DisplayListBuilder builder, InputSink input, PressScope press, MotionScope motion)
    {
        // Clickable even when empty and even without a rasterizer: the region is where the caret
        // comes from, and an empty field is exactly the one you most need to click into.
        if (!entry.Disabled) input.Add(new TextRegion(node.Bounds, entry, node.Path ?? ""));

        var editing = press.TextPath is { Length: > 0 } && node.Path == press.TextPath;
        var value = entry.Obscure ? new string('•', entry.Value.Length) : entry.Value;
        var shown = value.Length > 0 ? value : entry.Placeholder ?? "";
        var token = value.Length > 0 ? theme.TextPrimary : theme.TextMuted;
        var style = theme.Type(entry.Role);

        var advance = 0f;
        var shift = 0f;
        if (motion.TextRasterizer is null)
        {
            // No platform text service — headless tests, and any surface where glyphs are not
            // available yet. The soft bar is the same stand-in `Text` falls back to; the caret still
            // draws, because where it is remains the useful thing to see.
            EmitEntryPlaceholder(node, entry, theme, mode, builder);
        }
        else if (shown.Length > 0)
        {
            var rasterizer = motion.TextRasterizer;
            // While EDITING, the raster is unbounded and the FIELD is the window onto it — the
            // browser's own input behaviour. Bounded-and-ellipsized is for reading, and an ellipsis
            // in a field someone is typing into hides exactly the characters they just typed.
            var raster = (motion.TextCache ?? TextRasterCache.Shared)
                .Get(rasterizer, shown, style, motion.TypeScale,
                    editing ? float.MaxValue : node.Bounds.Width, 1, motion.RenderScale);
            if (raster is not null)
            {
                var width = raster.Texture.Width / motion.RenderScale;
                // Where the caret goes is a measurement of the text BEFORE it, not a fraction of the
                // whole: proportional glyphs make "iii" and "WWW" different widths at equal length.
                advance = press.CaretIndex >= value.Length
                    ? (value.Length > 0 ? width : 0)
                    : MeasureUpTo(rasterizer, value, press.CaretIndex, style, motion);

                // The window FOLLOWS the caret: when it would leave the right edge, the text slides
                // left just enough to keep it visible (a small margin shows the character being
                // approached). Derived from the caret alone — no scroll state to desynchronize.
                if (editing && advance > node.Bounds.Width - CaretFollowMargin)
                    shift = advance - (node.Bounds.Width - CaretFollowMargin);

                var rect = new Rect(node.Bounds.X - shift, node.Bounds.Y,
                    width, raster.Texture.Height / motion.RenderScale);
                if (shift > 0 || width > node.Bounds.Width)
                {
                    builder.PushClip(new RRect(node.Bounds, default));
                    builder.Texture(rect, token.Resolve(mode), raster.Texture);
                    builder.PopClip();
                }
                else
                {
                    builder.Texture(rect, token.Resolve(mode), raster.Texture);
                }
            }
        }

        if (!editing || entry.Disabled) return;

        var caretHeight = node.Text?.LineHeight ?? style.LineHeight;

        // The selection band. Drawn AFTER the glyphs and translucent rather than under them and
        // opaque: the text stays legible through it, which is what every platform does, and it
        // saves measuring the run twice to paint around it.
        if (press.SelectionEnd > press.SelectionStart && motion.TextRasterizer is { } selectionRasterizer)
        {
            var from = MeasureUpTo(selectionRasterizer, value, press.SelectionStart, style, motion) - shift;
            var to = MeasureUpTo(selectionRasterizer, value, press.SelectionEnd, style, motion) - shift;
            from = MathF.Max(from, 0);
            to = MathF.Min(to, node.Bounds.Width);
            if (to > from)
                builder.FillRRect(
                    new RRect(new Rect(node.Bounds.X + from, node.Bounds.Y, to - from, caretHeight),
                        new CornerRadii(1)),
                    Paint.Solid(theme.FocusRing.Resolve(mode).WithOpacity(0.28f)));
        }

        // A caret inside a selection would be noise: the range already says where you are.
        if (!press.CaretVisible || press.SelectionEnd > press.SelectionStart) return;
        builder.FillRRect(
            new RRect(new Rect(node.Bounds.X + advance - shift, node.Bounds.Y, CaretWidth, caretHeight),
                new CornerRadii(0)),
            Paint.Solid(theme.TextPrimary.Resolve(mode)));
    }

    /// <summary>The width of the first <paramref name="count"/> characters — the caret's x.</summary>
    private static float MeasureUpTo(Framework.ITextRasterizer rasterizer, string value, int count,
        TypeStyle style, MotionScope motion)
    {
        if (count <= 0) return 0;
        var raster = (motion.TextCache ?? TextRasterCache.Shared).Get(
            rasterizer, value[..Math.Min(count, value.Length)], style, motion.TypeScale, float.MaxValue, 1, motion.RenderScale);
        return raster is null ? 0 : raster.Texture.Width / motion.RenderScale;
    }

    /// <summary>2dp: thin enough to sit between glyphs, thick enough to see on a scaled display.</summary>
    private const float CaretWidth = 2f;

    /// <summary>How close to the right edge the caret may ride before the text slides: enough to
    /// see the caret itself plus a sliver of what comes next.</summary>
    private const float CaretFollowMargin = 8f;

    private static void EmitEntryPlaceholder(LayoutNode node, TextEntry entry, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (node.Text is not { } measurement || measurement.Lines.Count == 0) return;
        var hasValue = entry.Value.Length > 0;
        var token = hasValue ? theme.TextPrimary : theme.TextMuted;
        var color = token.Resolve(mode).WithOpacity(0.30f);
        var line = measurement.Lines[0];
        if (line.Width <= 0) return;

        var barHeight = measurement.LineHeight * 0.55f;
        var y = node.Bounds.Y + (measurement.LineHeight - barHeight) / 2;
        builder.FillRRect(
            new RRect(new Rect(node.Bounds.X, y, MathF.Min(line.Width, node.Bounds.Width), barHeight),
                new CornerRadii(barHeight / 3)),
            Paint.Solid(color));
    }

    /// <summary>
    /// Spec B15, drawn INSIDE the fence: 8 rrect bars (2×5 in the 16dp em-box, scaled), rotated
    /// i·45° about the center, opacity phase-staggered on the 800ms/rev linear clock — a pure
    /// function of the frame time (golden-testable at fixed t). Reduce Motion keeps the fade but
    /// drops the rotation phase: every bar pulses IN PLACE with the same alpha (spec B15) — the
    /// spinner therefore always reports active motion (it is a functional indicator, not
    /// decoration).
    /// </summary>
    private static void EmitSpinner(LayoutNode node, Spinner spinner, IAppTheme theme, ThemeMode mode,
        DisplayListBuilder builder, MotionScope motion)
    {
        motion.Active = true;
        var phase = motion.TimeMs % Spinner.RevolutionMs / Spinner.RevolutionMs;
        var tint = (spinner.Color ?? theme.TextPrimary).Resolve(mode);

        var scale = node.Bounds.Width / 16f;
        var centerX = node.Bounds.X + node.Bounds.Width / 2;
        var centerY = node.Bounds.Y + node.Bounds.Height / 2;
        var bar = new RRect(
            new Rect(centerX - scale, node.Bounds.Y, 2 * scale, 5 * scale),
            new CornerRadii(scale));

        for (var i = 0; i < 8; i++)
        {
            // Web parity: bar i runs the same 1→0.3 sawtooth with a -i·(rev/8) delay; Reduce
            // Motion zeroes the stagger (all bars share the pulse).
            var k = motion.Reduced ? phase : (((i - phase * 8) % 8) + 8) % 8 / 8;
            var alpha = 1f - 0.7f * k;

            builder.PushTransform(
                Matrix2D.Translation(-centerX, -centerY)
                * Matrix2D.Rotation(i * 45 * MathF.PI / 180)
                * Matrix2D.Translation(centerX, centerY));
            builder.FillRRect(bar, Paint.Solid(tint.WithOpacity(alpha)));
            builder.Pop();
        }
    }

    /// <summary>Hit contract (spec §08): every interactive node exposes ≥ 48dp per side — visual bounds
    /// may be smaller; the hit rect expands symmetrically.</summary>
    private static Rect ExpandHitRect(Rect bounds, Density density = Density.Comfortable)
    {
        // A POINTER lands where it is aimed: the §08 minimum is a FINGER's contract, and applying
        // it to a dense toolbar grew every 26dp button into its neighbour's margin.
        var minimum = density == Density.Compact ? 0 : Touch.MinTarget;
        var growX = MathF.Max(0, minimum - bounds.Width) / 2;
        var growY = MathF.Max(0, minimum - bounds.Height) / 2;
        return new Rect(bounds.X - growX, bounds.Y - growY, bounds.Width + growX * 2, bounds.Height + growY * 2);
    }
}
