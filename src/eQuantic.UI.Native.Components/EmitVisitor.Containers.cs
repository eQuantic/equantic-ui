using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The nodes that change what happens to their CHILDREN — clipped, queued for a later pass,
/// translated, faded, or laid over the viewport. Each one descends itself.
/// </summary>
internal sealed partial class EmitVisitor
{
    // A ScrollView clips its subtree to the viewport (spec A6) — the engine clip primitive.
    private void EmitScrollView(ScrollView scrollView, EmitState s)
    {
        // Scroll compositor v1: the host routes wheel/drag to the topmost region (paint order).
        if (s.ScrollMeta.TryGetValue(scrollView, out var meta))
        {
            s.Input.Add(new ScrollRegion(s.Node.Bounds, meta.Path, meta.MaxOffset, scrollView.Axis, scrollView.Offset));
            // The out channel: a component that windows its content needs both numbers, and
            // neither is knowable before layout. Reported from the realized frame, which is the
            // first moment either is true.
            scrollView.OnViewportChanged?.Invoke(
                scrollView.Axis == ScrollAxis.Horizontal ? s.Node.Bounds.Width : s.Node.Bounds.Height);
            scrollView.OnScrolled?.Invoke(s.Press.ScrollOffsetOf(meta.Path) ?? scrollView.Offset);
        }
        s.Builder.PushClip(new RRect(s.Node.Bounds));
        // …and the SUBTREE'S INPUT to the same rectangle. A row scrolled out of the viewport is
        // drawn nowhere, so it takes no taps — otherwise it keeps taking the ones aimed at
        // whatever the app shows there instead, and a fixed toolbar goes dead the moment the
        // list under it moves.
        var scrolled = s.Input.Under(s.Node.Bounds);
        // …and what counts as ON SCREEN. A row scrolled out of a list is not visible, and an
        // InView measured against the whole window would have said it was — the same mistake
        // in a third channel, after paint and input.
        var outerSurface = s.Press.Surface;
        s.Press.Surface = Intersect(outerSurface, s.Node.Bounds);
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = scrolled });
        s.Press.Surface = outerSurface;
        s.Builder.PopClip();
    }

    // A clipping Box confines its CHILDREN to its rrect (chrome already drew unclipped above) —
    // the container side of loop motion (the sweeping segment stays inside the track).
    private void EmitClippedBox(Box clipBox, EmitState s)
    {
        s.Builder.PushClip(new RRect(s.Node.Bounds, clipBox.Style.CornerRadius));
        var confined = s.Input.Under(s.Node.Bounds);
        // Clipped out is not on screen either — the same three channels as a ScrollView.
        var outer = s.Press.Surface;
        s.Press.Surface = Intersect(outer, s.Node.Bounds);
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = confined });
        s.Press.Surface = outer;
        s.Builder.PopClip();
    }

    private void EmitSheet(SheetSurface sheetSurface, EmitState s)
    {
        // The child paints EVERYTHING — cells, selection, active ring, the editing draft —
        // write-once in the shared component, so web and native cannot drift. This node only
        // registers the input region.
        s.Input.Add(new SheetRegion(s.Node.Bounds, sheetSurface, s.Node.Path ?? ""));
        foreach (var child in s.Node)
            Emit(s with { Node = child });
    }

    // An Overlay queues for the viewport pass — nothing renders in the page flow.
    private void EmitOverlay(Overlay overlay, EmitState s)
    {
        s.Overlays.Add(overlay);
    }

    // Wave 3 anchored overlay: the anchor renders in flow; while Open, a SYNTHETIC overlay
    // layer carries [full-viewport filler (scrim Pressable when dismissible) + the panel
    // Positioned from the anchor's ABSOLUTE bounds] — pure reuse of the overlay machinery,
    // so panel pressables, hit routing and painter's order all come for free.
    private void EmitAnchored(Anchored anchored, EmitState s)
    {
        foreach (var child in s.Node)
            Emit(s with { Node = child });

        // Wave 3b hover reveal (the Tooltip mechanism): the anchor registers for the host's
        // pointer tracking; the panel opens while hovered — no scrim (leave = closed).
        var hoverOpen = false;
        if (anchored.OpenOnHover)
        {
            s.Input.Add(new HoverRegion(s.Node.Bounds, anchored, s.Node.Path ?? ""));
            // By chain like every other hover read: hovering the TRIGGER inside is
            // hovering the anchored (CSS ancestor semantics), and the path survives the
            // rebuild its own opening causes.
            hoverOpen = s.Press.IsHovered(s.Node, anchored);
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
            s.Input.Add(new ShortcutBinding(KeyChord.Escape, escapable));

        // The MinWidth goes ON the panel's own box when there is one, not on a wrapper around
        // it: a hugging wrapper clamps its own frame and its hugging child re-measures at
        // intrinsic width inside it — a 550dp panel whose option rows stayed 178dp wide. On
        // the panel itself, the Min/Max reflow hands the final width down to the rows, which
        // is also exactly what the web's min-width:100% does.
        var panel = !anchored.MatchAnchorWidth ? anchored.Panel
            : anchored.Panel is Box panelBox
                ? new Box(panelBox.Style with
                {
                    MinWidth = MathF.Max(panelBox.Style.MinWidth, s.Node.Bounds.Width),
                }, panelBox.Child)
                : new Box(new BoxStyle { MinWidth = s.Node.Bounds.Width }, anchored.Panel);
        var b = s.Node.Bounds;
        var gap = anchored.Gap;
        layer.Add(anchored.Placement switch
        {
            AnchorPlacement.BottomEnd => new Positioned(panel, top: b.Bottom + gap, end: s.Motion.ViewportW - b.Right),
            AnchorPlacement.TopStart => new Positioned(panel, bottom: s.Motion.ViewportH - b.Y + gap, start: b.X),
            AnchorPlacement.TopEnd => new Positioned(panel, bottom: s.Motion.ViewportH - b.Y + gap, end: s.Motion.ViewportW - b.Right),
            AnchorPlacement.BottomCenter => new Positioned(CenteredOn(panel, b.Center.X), top: b.Bottom + gap, start: 0),
            AnchorPlacement.TopCenter => new Positioned(CenteredOn(panel, b.Center.X), bottom: s.Motion.ViewportH - b.Y + gap, start: 0),
            _ => new Positioned(panel, top: b.Bottom + gap, start: b.X),
        });
        s.Overlays.Add(new Overlay(layer));
    }

    // Loop motion: translate the subtree by the frame-clock offset (spec §06 transform-only).
    // Offsets are fractions of the node's OWN laid-out width — parity with CSS translateX(%).
    private void EmitLoopMotion(LoopMotion loop, EmitState s)
    {
        // Decorative loops (Skeleton shimmer) disappear under Reduce Motion — the spec's
        // static-placeholder behavior; positional loops render a still frame instead.
        if (s.Motion.Reduced && loop.HideAtRest) return;
        var offset = ResolveLoopOffset(loop, s.Node.Bounds.Width, s.Motion);
        if (offset != 0) s.Builder.PushTransform(Matrix2D.Translation(offset, 0));
        foreach (var child in s.Node)
            Emit(s with { Node = child });
        if (offset != 0) s.Builder.Pop();
    }

    private void EmitPresence(Presence presence, EmitState s)
    {
        // Enter motion (spec §06): a mid-entrance subtree paints inside a GROUP-opacity layer
        // (CSS-opacity semantics — overlapping children never double-blend) with the SlideUp
        // rise as a paint-only translate. Reduce Motion drops the movement (the store already
        // shortened the clock to the crossfade) — fade only, exactly the web media query.
        // Settled subtrees paint plainly (no layer cost at rest). Either way, the commands are
        // SNAPSHOTTED by path — the frame that finds this path gone replays them as the exit
        // (a mid-enter departure carries its inner layer, so the cross-fade composes).
        var start = s.Builder.CommandCount;
        var entering = s.Node.Presence < 1f;
        var rise = entering && presence.Enter == PresenceMotion.SlideUp && !s.Motion.Reduced
            ? (1f - s.Node.Presence) * Presence.SlideDistance
            : 0f;
        if (rise != 0) s.Builder.PushTransform(Matrix2D.Translation(0, rise));
        if (entering) s.Builder.PushLayer(s.Node.Presence);
        foreach (var child in s.Node)
            Emit(s with { Node = child });
        if (entering) s.Builder.PopLayer();
        if (rise != 0) s.Builder.Pop();
        if (s.Motion.Presences != null && s.Node.PresencePath is { } presencePath)
            s.Motion.Presences.Snapshot(presencePath, presence.Enter, s.Builder.CommandsFrom(start));
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

    /// <summary>One edge of a partial border: the full outline, clipped to that edge's band.</summary>
    /// <summary>The overlap of two rectangles, or an empty one where they do not meet.</summary>
    private static Rect Intersect(Rect a, Rect b)
    {
        var x = MathF.Max(a.X, b.X);
        var y = MathF.Max(a.Y, b.Y);
        var right = MathF.Min(a.X + a.Width, b.X + b.Width);
        var bottom = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        return right <= x || bottom <= y ? new Rect(x, y, 0, 0) : new Rect(x, y, right - x, bottom - y);
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
}
