using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>A pressable region registered by the realizer — hit rect expanded to the §08 contract.</summary>
public readonly record struct HitRegion(Rect Bounds, Pressable Node);

/// <summary>Spec S5/gestures: a hover-reactive region — a Box carrying a Hover diff. The host's
/// pointer tracking resolves the TOPMOST region under the pointer (paint order = registration
/// order, so last-contains wins).</summary>
public readonly record struct HoverRegion(Rect Bounds, Box Node);

/// <summary>Scroll compositor v1: a scrollable viewport — the host routes wheel/drag input to the
/// TOPMOST region under the pointer and adjusts its stored offset (clamped to MaxOffset).</summary>
public readonly record struct ScrollRegion(Rect Bounds, string Path, float MaxOffset, ScrollAxis Axis, float Fallback);

/// <summary>Gestures v2: a drag-to-dismiss surface — the host tracks a press that travels past the
/// slop as a vertical drag on the TOPMOST region under the start point (paint-order last-wins).</summary>
public readonly record struct DragRegion(Rect Bounds, string Path, DragDismiss Node);

/// <summary>A navigation surface (the write-once Link): a tap no pressable claims resolves to the
/// TOPMOST link region under the point, through the host's navigation seam.</summary>
public readonly record struct LinkRegion(Rect Bounds, Link Node);

/// <summary>The realized frame: the laid-out tree (absolute bounds) and the interactive hit regions.</summary>
public sealed class RealizeResult
{
    public RealizeResult(LayoutNode root, IReadOnlyList<HitRegion> hitRegions, bool hasActiveMotion,
        IReadOnlyList<HoverRegion>? hoverRegions = null, IReadOnlyList<ScrollRegion>? scrollRegions = null,
        IReadOnlyList<DragRegion>? dragRegions = null, IReadOnlyList<LinkRegion>? linkRegions = null)
    {
        Root = root;
        HitRegions = hitRegions;
        HasActiveMotion = hasActiveMotion;
        HoverRegions = hoverRegions ?? Array.Empty<HoverRegion>();
        ScrollRegions = scrollRegions ?? Array.Empty<ScrollRegion>();
        DragRegions = dragRegions ?? Array.Empty<DragRegion>();
        LinkRegions = linkRegions ?? Array.Empty<LinkRegion>();
    }

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
        float renderScale = 1f)
    {
        var context = new LayoutContext(theme, measurer ?? ApproximateTextMeasurer.Instance, typeScale)
        {
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
            RenderScale = renderScale,
            TypeScale = typeScale,
        };
        var overlays = new List<Overlay>();
        var dragRegions = new List<DragRegion>();
        var links = new List<LinkRegion>();
        Emit(layout, theme, mode, builder, hits, hovers, scrolls, dragRegions, links, context.ScrollMeta!, new PressScope(pressed, focused, hovered), motion, overlays);

        // Overlay pass (Phase C): each queued layer lays out against the VIEWPORT and paints ABOVE
        // the page (painter's order); its hit regions register after the page's, so the topmost-
        // last-wins dispatch routes taps to the layer — a full-viewport scrim Pressable in the
        // layer blocks (and optionally handles) everything behind it.
        for (var i = 0; i < overlays.Count; i++)
        {
            var overlayLayout = LayoutEngine.Layout(overlays[i].Child, viewportWidth, viewportHeight,
                context, rootPath: $"ov{i}");
            Emit(overlayLayout, theme, mode, builder, hits, hovers, scrolls, dragRegions, links, context.ScrollMeta!, new PressScope(pressed, focused, hovered), motion, overlays);
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
                if (exit.Drop != 0) builder.PushTransform(Matrix2D.Translation(0, exit.Drop));
                builder.PushLayer(exit.Alpha);
                foreach (var command in exit.Commands) builder.Replay(command);
                builder.PopLayer();
                if (exit.Drop != 0) builder.Pop();
            }
        }
        context.Instances?.EndPass();
        return new RealizeResult(layout, hits,
            motion.Active || transitions is { AnyActive: true } || presences is { AnyActive: true }
                || drags is { AnyActive: true },
            hovers, scrolls, dragRegions, links);
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
        public PressScope(Pressable? pressed, Pressable? focused, VisualNode? hovered = null)
        {
            Pressed = pressed;
            Focused = focused;
            Hovered = hovered;
        }

        public Pressable? Pressed { get; }
        public Pressable? Focused { get; }

        /// <summary>Spec S5: the node the pointer is over — its Box applies its Hover diff. Fed by
        /// the host's pointer tracking (the gesture slice); tests pass it directly.</summary>
        public VisualNode? Hovered { get; }
        public ColorToken? PendingFill { get; set; }
        public bool PendingFocusRing { get; set; }
    }

    private static void Emit(LayoutNode node, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, List<HitRegion> hits, List<HoverRegion> hovers, List<ScrollRegion> scrolls, List<DragRegion> drags, List<LinkRegion> links, Dictionary<ScrollView, (string Path, float MaxOffset)> scrollMeta, PressScope press, MotionScope motion, List<Overlay> overlays)
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

            EmitNode(node, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);

            if (transformed) builder.Pop();
            if (opacity is not null) builder.PopLayer();
            return;
        }

        EmitNode(node, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
    }

    /// <summary>The CSS transform list twin: translate → rotate → scale, anchored at the box center.</summary>
    private static Matrix2D CenterAnchored(in Transform2D t, Point center) =>
        Matrix2D.Translation(-center.X, -center.Y)
        * Matrix2D.Scale(t.ScaleX, t.ScaleY)
        * Matrix2D.Rotation(t.RotationDegrees * MathF.PI / 180f)
        * Matrix2D.Translation(center.X + t.TranslateX, center.Y + t.TranslateY);

    private static void EmitNode(LayoutNode node, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, List<HitRegion> hits, List<HoverRegion> hovers, List<ScrollRegion> scrolls, List<DragRegion> drags, List<LinkRegion> links, Dictionary<ScrollView, (string Path, float MaxOffset)> scrollMeta, PressScope press, MotionScope motion, List<Overlay> overlays)
    {
        if (ReferenceEquals(node.Source, press.Pressed) && press.Pressed?.PressedBackground is { } pressedFill)
            press.PendingFill = pressedFill;
        if (ReferenceEquals(node.Source, press.Focused))
            press.PendingFocusRing = true;

        switch (node.Source)
        {
            case Box box:
            {
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

                var fill = press.PendingFill ?? box.Style.Background;
                press.PendingFill = null;
                var borderColor = box.Style.BorderColor;
                var borderWidth = box.Style.BorderWidth;
                // Spec S5: hover-reactive boxes register for the host's pointer tracking.
                if (box.Style.Hover is { IsEmpty: false })
                    hovers.Add(new HoverRegion(node.Bounds, box));

                // Spec S5: the hovered Box applies its Hover diff (pressed still wins on fill).
                if (ReferenceEquals(node.Source, press.Hovered) && box.Style.Hover is { IsEmpty: false } hover)
                {
                    if (press.PendingFill is null && hover.Background is { } hoverFill) fill = hoverFill;
                    if (hover.BorderColor is { } hoverBorder) borderColor = hoverBorder;
                    if (hover.BorderWidth is { } hoverWidth) borderWidth = hoverWidth;
                }
                EmitChrome(node.Bounds, fill, box.Style.CornerRadius,
                    borderColor, borderWidth, theme, mode, builder,
                    box.Style.Gradient);

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
                EmitEntryPlaceholder(node, entry, theme, mode, builder);
                break;

            // Spec A11 fence: a SurfaceSubtle box under the radius stands in for the bitmap until the
            // engine gains texture upload (M4) - the documented placeholder pattern.
            case Image image:
                builder.FillRRect(
                    new RRect(node.Bounds, image.CornerRadius),
                    Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
                break;

            // Spec A10, W4 fence: a tinted disc at 30% alpha stands in for the glyph until the atlas
            // lands — the same documented placeholder pattern as text bars.
            case Icon icon:
            {
                var tint = (icon.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
                builder.FillRRect(
                    new RRect(node.Bounds, new CornerRadii(node.Bounds.Width / 2)),
                    Paint.Solid(tint));
                break;
            }

            case Spinner spinner:
                EmitSpinner(node, spinner, theme, mode, builder, motion);
                break;

            case Pressable pressable:
                hits.Add(new HitRegion(ExpandHitRect(node.Bounds), pressable));
                break;
        }

        // A ScrollView clips its subtree to the viewport (spec A6) — the engine clip primitive.
        if (node.Source is ScrollView scrollView)
        {
            // Scroll compositor v1: the host routes wheel/drag to the topmost region (paint order).
            if (scrollMeta.TryGetValue(scrollView, out var meta))
                scrolls.Add(new ScrollRegion(node.Bounds, meta.Path, meta.MaxOffset, scrollView.Axis, scrollView.Offset));
            builder.PushClip(new RRect(node.Bounds));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
            builder.PopClip();
            return;
        }

        // A clipping Box confines its CHILDREN to its rrect (chrome already drew unclipped above) —
        // the container side of loop motion (the sweeping segment stays inside the track).
        if (node.Source is Box { Style.Clip: true } clipBox)
        {
            builder.PushClip(new RRect(node.Bounds, clipBox.Style.CornerRadius));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
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
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
            if (!anchored.Open) return;

            var filler = new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill });
            var layer = new Stack();
            layer.Add(anchored.OnDismiss is { } dismiss
                ? new Pressable(filler, dismiss) { Label = "Dismiss" }
                : filler);

            var panel = anchored.MatchAnchorWidth
                ? new Box(new BoxStyle { MinWidth = node.Bounds.Width }, anchored.Panel)
                : anchored.Panel;
            var b = node.Bounds;
            var gap = anchored.Gap;
            layer.Add(anchored.Placement switch
            {
                AnchorPlacement.BottomEnd => new Positioned(panel, top: b.Bottom + gap, end: motion.ViewportW - b.Right),
                AnchorPlacement.TopStart => new Positioned(panel, bottom: motion.ViewportH - b.Y + gap, start: b.X),
                AnchorPlacement.TopEnd => new Positioned(panel, bottom: motion.ViewportH - b.Y + gap, end: motion.ViewportW - b.Right),
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
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
            if (offset != 0) builder.Pop();
            return;
        }

        if (node.Source is Link link)
        {
            // Navigation surface: pure semantics — the child paints; a tap that no pressable claims
            // resolves to this region through the host's navigation seam.
            links.Add(new LinkRegion(node.Bounds, link));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
            return;
        }

        if (node.Source is DragDismiss dragDismiss && node.DragPath is { } dragPath)
        {
            // Gestures v2: the surface registers for the host's drag routing, and the current offset
            // (active follow or glide-back) paints as a translate — layout untouched, exactly like
            // loop motion. Hit regions inside register at their laid-out bounds; mid-drag taps are
            // cancelled by the slop rule, so the transient misalignment is unreachable.
            drags.Add(new DragRegion(node.Bounds, dragPath, dragDismiss));
            var dragOffset = node.DragOffset;
            if (dragOffset != 0) builder.PushTransform(Matrix2D.Translation(0, dragOffset));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
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
                Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
            if (entering) builder.PopLayer();
            if (rise != 0) builder.Pop();
            if (motion.Presences != null && node.PresencePath is { } presencePath)
                motion.Presences.Snapshot(presencePath, presence.Enter, builder.CommandsFrom(start));
            return;
        }

        foreach (var child in node.Children)
            Emit(child, theme, mode, builder, hits, hovers, scrolls, drags, links, scrollMeta, press, motion, overlays);
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
        LinearGradient? gradient = null)
    {
        if (bounds.IsEmpty) return;

        if (background is { } bg)
        {
            var color = bg.Resolve(mode);
            if (color.A > 0)
                builder.FillRRect(new RRect(bounds, radius), Paint.Solid(color));
        }

        // The gradient draws OVER the solid (CSS background-image/background-color composition):
        // Paint.Linear across the box bounds on the declared axis, stops resolved per mode.
        if (gradient is { } g)
        {
            var end = g.Direction == GradientDirection.ToBottom
                ? new Point(bounds.X, bounds.Y + bounds.Height)
                : new Point(bounds.X + bounds.Width, bounds.Y);
            builder.FillRRect(new RRect(bounds, radius),
                Paint.Linear(new Point(bounds.X, bounds.Y), end, g.From.Resolve(mode), g.To.Resolve(mode)));
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
            var raster = (motion.TextCache ?? TextRasterCache.Shared).Get(
                rasterizer, text.Content, style, motion.TypeScale, node.Bounds.Width, text.MaxLines, motion.RenderScale);
            if (raster is not null)
            {
                var color = (text.Color ?? theme.TextPrimary).Resolve(mode);
                var rect = new Rect(node.Bounds.X, node.Bounds.Y,
                    raster.Texture.Width / motion.RenderScale, raster.Texture.Height / motion.RenderScale);
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

    /// <summary>The TextEntry stand-in (spec B9 fence): one soft bar per the W4 text placeholder
    /// convention — the VALUE in the entry's text color, an empty value shows the PLACEHOLDER in
    /// TextMuted. Deterministic layout geometry until the real text stack (M4).</summary>
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
    private static Rect ExpandHitRect(Rect bounds)
    {
        var growX = MathF.Max(0, Touch.MinTarget - bounds.Width) / 2;
        var growY = MathF.Max(0, Touch.MinTarget - bounds.Height) / 2;
        return new Rect(bounds.X - growX, bounds.Y - growY, bounds.Width + growX * 2, bounds.Height + growY * 2);
    }
}
