using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>Everything a layout pass needs besides the tree: theme (type styles), text metrics, Dynamic Type factor.</summary>
public sealed class LayoutContext
{
    public LayoutContext(IAppTheme theme, ITextMeasurer measurer, float typeScale = 1f)
    {
        Theme = theme;
        Measurer = measurer;
        TypeScale = typeScale;
        Components = new ComponentContext(theme, typeScale);
    }

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

    public VisualNode Source { get; }
    public Rect Bounds { get; internal set; }
    public List<LayoutNode> Children { get; } = new();
    public TextMeasurement? Text { get; internal set; }

    /// <summary>Entrance progress (0..1) stamped at MEASURE time when <see cref="Source"/> is a
    /// <see cref="Presence"/> — the emit pass reads it (paths exist only during layout). 1 = settled
    /// (no presence clock in the context, or the entrance finished).</summary>
    public float Presence { get; internal set; } = 1f;

    /// <summary>The stable layout path of a <see cref="Presence"/> node, stamped at measure time —
    /// the emit pass keys its exit snapshot by it (paths exist only during layout).</summary>
    public string? PresencePath { get; internal set; }

    /// <summary>The drag offset (dp downward) of a <see cref="DragDismiss"/> node, resolved at
    /// measure time against the host's drag clock — the emit pass paints the translate.</summary>
    public float DragOffset { get; internal set; }

    /// <summary>The stable layout path of a <see cref="DragDismiss"/> node — the host routes drag
    /// input by it (registered with the frame's drag regions at emit).</summary>
    public string? DragPath { get; internal set; }
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
        var node = Measure(root, viewportWidth, viewportHeight, context, rootPath);
        Absolutize(node, 0, 0);
        return node;
    }

    // ---- measurement (bounds are PARENT-RELATIVE until Absolutize) ------------------------------

    private static LayoutNode Measure(VisualNode node, float maxW, float maxH, LayoutContext ctx, string path) => node switch
    {
        Box box => MeasureBox(box, maxW, maxH, ctx, path),
        FlexNode flex => MeasureFlex(flex, maxW, maxH, ctx, path),
        Stack stack => MeasureStack(stack, maxW, maxH, ctx, path),
        Grid grid => MeasureGrid(grid, maxW, maxH, ctx, path),
        // Spec S6: an AdaptiveNode IS its resolved variant on native — the other variants never
        // measure, never paint (the web keeps them, CSS-gated).
        AdaptiveNode adaptive => Measure(adaptive.Resolve(ctx.SizeClass), maxW, maxH, ctx, path + "/0"),
        // Spec S7: Sticky renders IN FLOW on native until engine scrolling lands (correct at scroll
        // offset 0); the pinning joins the scroll compositor (fence on the node's doc).
        Sticky sticky => MeasureWrapper(sticky, sticky.Child, maxW, maxH, ctx, path),
        // The system's own margins. A desktop window has no cutouts, so the host reports zero and
        // the node measures as its child plus whatever padding the caller asked for on top — the
        // SAME tree an iPhone insets, with the numbers coming from the host rather than the app.
        SafeArea safeArea => MeasureSafeArea(safeArea, maxW, maxH, ctx, path),
        // Wave 3: the anchor owns layout; the panel realizes in the realizer's overlay pass.
        Anchored anchored => MeasureWrapper(anchored, anchored.Anchor, maxW, maxH, ctx, path),
        ScrollView scroll => MeasureScrollView(scroll, maxW, maxH, ctx, path),
        // A Positioned outside a Stack has no anchor frame — degrade to a transparent wrapper.
        // A continuous gesture is layout-transparent: the offset is a PAINT translate, exactly like
        // the sheet's. Re-laying out under a finger would fight the scroll it usually lives in.
        Draggable draggable => MeasureDraggable(draggable, maxW, maxH, ctx, path),
        Positioned positioned => MeasureWrapper(positioned, positioned.Child, maxW, maxH, ctx, path),
        Text text => MeasureText(text, maxW, ctx),
        TextEntry entry => MeasureTextEntry(entry, maxW, ctx),
        // Images are an explicitly sized slot - layout can't infer extent from undecoded sources (A11).
        Image image => new LayoutNode(image) { Bounds = new Rect(0, 0, image.Width, image.Height) },
        // Icons are a fixed square em-box (§07 whitelist) and ignore Dynamic Type (spec A10).
        Icon icon => new LayoutNode(icon) { Bounds = new Rect(0, 0, icon.Size, icon.Size) },
        // A vector is the same square em-box, at the size the author asked for.
        Vector vector => new LayoutNode(vector) { Bounds = new Rect(0, 0, vector.Size, vector.Size) },
        // The Spinner shares the icon em-box contract (spec B15: sizes = the §07 whitelist).
        Spinner spinner => new LayoutNode(spinner) { Bounds = new Rect(0, 0, spinner.Size, spinner.Size) },
        Pressable pressable => MeasureWrapper(pressable, pressable.Child, maxW, maxH, ctx, path),
        // Pointer presence is layout-transparent (S5 programmable hover — the child owns visuals).
        Hoverable hoverable => MeasureWrapper(hoverable, hoverable.Child, maxW, maxH, ctx, path),
        // Spec S8: a Shortcut is layout-transparent — the binding rides the realizer's walk.
        Shortcut shortcut => MeasureWrapper(shortcut, shortcut.Child, maxW, maxH, ctx, path),
        // A Link is layout-transparent (semantics + interaction only — the child owns visuals).
        Link link => MeasureWrapper(link, link.Child, maxW, maxH, ctx, path),
        Flexible flexible => MeasureWrapper(flexible, flexible.Child, maxW, maxH, ctx, path),
        // Loop motion is layout-transparent: the offset is a REALIZE-time transform (spec §06 —
        // transform-only frames never re-lay-out).
        LoopMotion motion => MeasureWrapper(motion, motion.Child, maxW, maxH, ctx, path),
        // Enter motion is layout-transparent too (opacity layer + paint-only translate) — but the
        // progress is resolved HERE, where the stable path exists, and stamped on the node.
        Presence presence => MeasurePresence(presence, maxW, maxH, ctx, path),
        // Drag-to-dismiss follows the same pattern: transparent for layout, offset stamped by path.
        DragDismiss drag => MeasureDragDismiss(drag, maxW, maxH, ctx, path),
        // An Overlay is ZERO in the page flow — the realizer lays its child out against the
        // VIEWPORT in the overlay pass (path "ov<i>", stable for the reconciler).
        Overlay => new LayoutNode(node),
        // A component expands INLINE: Build produces its subtree (pure, mode-free), which is measured
        // in place — the component wraps it in the layout tree, drawing nothing itself.
        // Components RECONCILE by position first: the retained instance (state alive) replaces the
        // fresh one the parent just built, adopting its config; then it expands inline via Build.
        UiComponent component => MeasureComponent(component, maxW, maxH, ctx, path),
        Spacer => new LayoutNode(node), // zero outside a flex container (layout-only)
        _ => new LayoutNode(node),
    };

    /// <summary>
    /// Insets from the HOST (notch, status bar, home indicator) plus the caller's own padding. The
    /// host reports them; on a desktop window they are zero, which is the correct answer there.
    /// </summary>
    private static LayoutNode MeasureSafeArea(SafeArea safeArea, float maxW, float maxH,
        LayoutContext ctx, string path)
    {
        var host = ctx.SafeAreaInsets;
        var top = (safeArea.Edges.HasFlag(SafeEdges.Top) ? host.Top : 0) + safeArea.Extra.Top;
        var bottom = (safeArea.Edges.HasFlag(SafeEdges.Bottom) ? host.Bottom : 0) + safeArea.Extra.Bottom;
        var start = (safeArea.Edges.HasFlag(SafeEdges.Start) ? host.Start : 0) + safeArea.Extra.Start;
        var end = (safeArea.Edges.HasFlag(SafeEdges.End) ? host.End : 0) + safeArea.Extra.End;

        var child = Measure(safeArea.Child, MathF.Max(0, maxW - start - end),
            MathF.Max(0, maxH - top - bottom), ctx, path + "/0");
        child.Bounds = child.Bounds with { X = start, Y = top };

        var node = new LayoutNode(safeArea)
        {
            Bounds = new Rect(0, 0, child.Bounds.Width + start + end, child.Bounds.Height + top + bottom),
        };
        node.Children.Add(child);
        return node;
    }

    private static LayoutNode MeasureWrapper(VisualNode node, VisualNode child, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = new LayoutNode(node);
        var inner = Measure(child, maxW, maxH, ctx, path + "/0");
        result.Children.Add(inner);
        result.Bounds = new Rect(0, 0, inner.Bounds.Width, inner.Bounds.Height);
        return result;
    }

    private static LayoutNode MeasureComponent(UiComponent component, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var resolved = ctx.Instances?.Reconcile(path, component) ?? component;
        return MeasureWrapper(resolved, resolved.Build(ctx.Components), maxW, maxH, ctx, path);
    }

    /// <summary>A transparent wrapper that also resolves the ENTRANCE progress against the host's
    /// presence clock, keyed by this stable path — the emit pass applies the paint-only effect and
    /// snapshots the subtree's commands by the same path (the exit replay source).</summary>
    private static LayoutNode MeasurePresence(Presence presence, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(presence, presence.Child, maxW, maxH, ctx, path);
        result.Presence = ctx.Presences?.Progress(path, ctx.TimeMs, ctx.ReducedMotion) ?? 1f;
        result.PresencePath = path;
        return result;
    }

    /// <summary>A transparent wrapper that resolves the current DRAG offset against the host's drag
    /// clock (active follow or glide-back), keyed by this stable path — the emit pass paints the
    /// translate and registers the drag region the host routes input by.</summary>
    private static LayoutNode MeasureDragDismiss(DragDismiss drag, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(drag, drag.Child, maxW, maxH, ctx, path);
        result.DragOffset = ctx.Drags?.Resolve(path, ctx.TimeMs) ?? 0f;
        result.DragPath = path;
        return result;
    }

    /// <summary>The live offset for this gesture — the finger while it is down, the glide after it
    /// lifts, and the caller's RestOffset when neither is happening.</summary>
    private static LayoutNode MeasureDraggable(Draggable draggable, float maxW, float maxH,
        LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(draggable, draggable.Child, maxW, maxH, ctx, path);
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
        var result = new LayoutNode(stack);
        var contentW = 0f;
        var contentH = 0f;

        for (var stackIndex = 0; stackIndex < stack.Children.Count; stackIndex++)
        {
            var child = stack.Children[stackIndex];
            var measured = Measure(child, maxW, maxH, ctx, path + "/" + stackIndex);
            result.Children.Add(measured);
            if (child is Positioned) continue;
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

            if (child is Positioned positioned)
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

        // Spec S7 z-order: children paint (and hit-test, topmost-last) in ZIndex order — a stable
        // sort keeps declaration order for equal values (flow order = the painter's default).
        if (stack.Children.Any(c => c is Positioned { ZIndex: not 0 }))
        {
            var ordered = result.Children
                .Select((node, i) => (Node: node, Z: stack.Children[i] is Positioned p ? p.ZIndex : 0, I: i))
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
    private static LayoutNode MeasureScrollView(ScrollView scroll, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = new LayoutNode(scroll);
        var horizontal = scroll.Axis == ScrollAxis.Horizontal;

        var child = Measure(scroll.Child,
            horizontal ? float.PositiveInfinity : maxW,
            horizontal ? maxH : float.PositiveInfinity, ctx, path + "/0");
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

        // Spec S7 — Sticky PINNING (vertical v1): a Sticky at content-y y0 shows at y0 - offset;
        // once that would pass its own Offset from the viewport start, it pins there instead.
        if (!horizontal && offset > 0)
            PinSticky(child, accumulatedY: child.Bounds.Y);

        return result;
    }

    /// <summary>Walks the scrolled content for Sticky wrappers and clamps their viewport-relative Y
    /// (v1: vertical, no end-of-container release — that fence joins the compositor polish). Nested
    /// ScrollViews own their own pinning pass.</summary>
    private static void PinSticky(LayoutNode node, float accumulatedY)
    {
        foreach (var child in node.Children)
        {
            if (child.Source is ScrollView) continue;
            var viewportY = accumulatedY + child.Bounds.Y;
            if (child.Source is Sticky sticky && viewportY < sticky.Offset)
            {
                child.Bounds = child.Bounds with { Y = child.Bounds.Y + (sticky.Offset - viewportY) };
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
        var result = new LayoutNode(text);
        var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
        var measurement = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, maxW, text.MaxLines);
        result.Text = measurement;
        result.Bounds = new Rect(0, 0, measurement.Width, measurement.Height);
        return result;
    }

    /// <summary>A text entry is <see cref="TextEntry.Lines"/> lines of its role (1 by default),
    /// filling the available width (the field's editable area) — height from the type scale so forms
    /// lay out identically before and after the real caret/IME land (spec B9's fixed contract). A
    /// multi-line field is exactly that many lines TALL whatever it currently holds: its box must not
    /// grow and shrink as the user types.</summary>
    private static LayoutNode MeasureTextEntry(TextEntry entry, float maxW, LayoutContext ctx)
    {
        var result = new LayoutNode(entry);
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

    private static LayoutNode MeasureBox(Box box, float maxW, float maxH, LayoutContext ctx, string path)
    {
        var result = new LayoutNode(box);
        var style = box.Style;

        var selfMaxW = CapMax(maxW, style.MaxWidth);
        var selfMaxH = CapMax(maxH, style.MaxHeight);

        // Content box the child may use (explicit/Fill pin it; Hug passes the available through).
        var childMaxW = ResolveForChild(style.Width, selfMaxW) - style.Padding.Horizontal;
        var childMaxH = ResolveForChild(style.Height, selfMaxH) - style.Padding.Vertical;

        LayoutNode? child = null;
        if (box.Child is not null)
        {
            child = Measure(box.Child, MathF.Max(0, childMaxW), MathF.Max(0, childMaxH), ctx, path + "/0");
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Children.Add(child);
        }

        var width = ResolveSelf(style.Width, selfMaxW, (child?.Bounds.Width ?? 0) + style.Padding.Horizontal);
        var height = ResolveSelf(style.Height, selfMaxH, (child?.Bounds.Height ?? 0) + style.Padding.Vertical);
        width = Clamp(width, style.MinWidth, style.MaxWidth);
        height = Clamp(height, style.MinHeight, style.MaxHeight);

        // Spec S1 aspect-ratio (CSS twin): when exactly one axis is author-determined, the other
        // derives from it; two explicit axes win over the ratio (no constraint fight).
        if (style.AspectRatio > 0)
        {
            var widthSet = style.Width.Kind != SizeKind.Hug;
            var heightSet = style.Height.Kind != SizeKind.Hug;
            if (widthSet && !heightSet)
                height = Clamp(width / style.AspectRatio, style.MinHeight, style.MaxHeight);
            else if (heightSet && !widthSet)
                width = Clamp(height * style.AspectRatio, style.MinWidth, style.MaxWidth);
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

    private static LayoutNode MeasureFlex(FlexNode flex, float maxW, float maxH, LayoutContext ctx, string path)
    {
        if (flex.Wrap) return MeasureFlexWrapped(flex, maxW, maxH, ctx, path);

        var result = new LayoutNode(flex);
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
        var mainBounded = mainSize.Kind == SizeKind.Fixed
                          || (!float.IsPositiveInfinity(mainAvail)
                              && (mainSize.Kind == SizeKind.Fill || hasFlexibles));

        var crossAvail = crossSize.Kind == SizeKind.Fixed ? crossSize.Value - padCross
            : !float.IsPositiveInfinity(crossMax) ? crossMax - padCross
            : float.PositiveInfinity;

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
                    flexWeights[i] = ctx.Transitions?.Resolve(path + "/" + i, f.Flex, ctx.TimeMs,
                        f.AnimateChanges, ctx.ReducedMotion) ?? f.Flex;
                    continue;
                case Spacer { Flex: > 0 } s:
                    flexWeights[i] = ctx.Transitions?.Resolve(path + "/" + i, s.Flex, ctx.TimeMs,
                        s.AnimateChanges, ctx.ReducedMotion) ?? s.Flex;
                    continue;
                case Spacer fixedSpacer:
                    mains[i] = fixedSpacer.FixedLength;
                    rigidSum += mains[i];
                    continue;
            }

            var childMaxW = horizontal ? mainAvail : crossAvail;
            var childMaxH = horizontal ? crossAvail : mainAvail;
            var child = Measure(children[i], childMaxW, childMaxH, ctx, path + "/" + i);
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
                    var remeasured = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, reduced,
                        Math.Max(1, text.MaxLines));
                    var node = new LayoutNode(text) { Text = remeasured };
                    node.Bounds = new Rect(0, 0, remeasured.Width, remeasured.Height);
                    laid[i] = node;
                    rigidSum -= mains[i] - (horizontal ? node.Bounds.Width : node.Bounds.Height);
                    mains[i] = horizontal ? node.Bounds.Width : node.Bounds.Height;
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
            var share = flexTotal > 0 ? leftover * flexWeights[i] / flexTotal : 0f;
            mains[i] = share;

            if (children[i] is Flexible flexible)
            {
                var childMaxW = horizontal ? share : crossAvail;
                var childMaxH = horizontal ? crossAvail : share;
                var child = Measure(flexible.Child, childMaxW, childMaxH, ctx, path + "/" + i + "/0");
                // The flexible slot IS the share on the main axis (the child fills it).
                child.Bounds = horizontal
                    ? child.Bounds with { Width = share }
                    : child.Bounds with { Height = share };
                var wrapper = new LayoutNode(flexible) { Bounds = child.Bounds };
                wrapper.Children.Add(child);
                laid[i] = wrapper;
            }
            else
            {
                laid[i] = new LayoutNode(children[i]); // flexible Spacer: pure space
            }
        }

        // Container size.
        var contentMain = rigidSum + (flexTotal > 0 ? leftover : 0) + gapTotal;
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };

        var crossContent = 0f;
        foreach (var child in laid)
            if (child is not null)
                crossContent = MathF.Max(crossContent, horizontal ? child.Bounds.Height : child.Bounds.Width);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(crossMax) => crossMax,
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
        var result = new LayoutNode(flex);
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

        // Measure every child at natural size (wrap v1: Flexible/Spacer weights don't distribute —
        // a Flexible degrades to its child, a flexible Spacer to nothing).
        var measured = new List<LayoutNode>(flex.Children.Count);
        var sources = new List<VisualNode>(flex.Children.Count);
        for (var i = 0; i < flex.Children.Count; i++)
        {
            var child = flex.Children[i] is Flexible flexible ? flexible.Child : flex.Children[i];
            if (child is Spacer) continue;
            var node = Measure(child, mainAvail, crossMax - padCross, ctx, path + "/" + i);
            measured.Add(node);
            sources.Add(flex.Children[i]);
        }

        // Break into lines.
        var lines = new List<(int Start, int Count, float Main, float Cross)>();
        var lineStart = 0;
        var lineMain = 0f;
        var lineCross = 0f;
        for (var i = 0; i < measured.Count; i++)
        {
            var childMain = horizontal ? measured[i].Bounds.Width : measured[i].Bounds.Height;
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
        var result = new LayoutNode(grid);
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
            var child = Measure(node, cellW, maxH, ctx, path + "/" + i);
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
        Hoverable hoverable => CrossSizeKind(hoverable.Child, horizontal),
        Shortcut shortcut => CrossSizeKind(shortcut.Child, horizontal),
        Flexible flexible => CrossSizeKind(flexible.Child, horizontal),
        // Always-explicit nodes: their constructors demand a size — stretch must never override.
        Image => SizeKind.Fixed,
        Icon => SizeKind.Fixed,
        Vector => SizeKind.Fixed,
        Spinner => SizeKind.Fixed,
        Grid grid => (horizontal ? grid.Height : grid.Width).Kind,
        // Layout-transparent wrappers delegate to what they wrap.
        Sticky sticky => CrossSizeKind(sticky.Child, horizontal),
        Draggable draggable => CrossSizeKind(draggable.Child, horizontal),
        SafeArea safeArea => CrossSizeKind(safeArea.Child, horizontal),
        Presence presence => CrossSizeKind(presence.Child, horizontal),
        LoopMotion loop => CrossSizeKind(loop.Child, horizontal),
        DragDismiss drag => CrossSizeKind(drag.Child, horizontal),
        Link link => CrossSizeKind(link.Child, horizontal),
        Anchored anchored => CrossSizeKind(anchored.Anchor, horizontal),
        _ => SizeKind.Hug,
    };

    private static float CapMax(float available, float styleMax) =>
        styleMax > 0 ? MathF.Min(available, styleMax) : available;

    /// <summary>Max extent a child may use, given the node's own size request.</summary>
    private static float ResolveForChild(SizeValue size, float available) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        _ => available,
    };

    /// <summary>Own size: explicit &gt; Fill &gt; Hug (spec A1).</summary>
    private static float ResolveSelf(SizeValue size, float available, float hug) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        SizeKind.Fill when !float.IsPositiveInfinity(available) => available,
        _ => hug,
    };

    private static float Clamp(float value, float min, float max)
    {
        if (min > 0) value = MathF.Max(value, min);
        if (max > 0) value = MathF.Min(value, max);
        return value;
    }

    private static void Absolutize(LayoutNode node, float originX, float originY)
    {
        node.Bounds = node.Bounds with { X = node.Bounds.X + originX, Y = node.Bounds.Y + originY };
        foreach (var child in node.Children)
            Absolutize(child, node.Bounds.X, node.Bounds.Y);
    }
}
