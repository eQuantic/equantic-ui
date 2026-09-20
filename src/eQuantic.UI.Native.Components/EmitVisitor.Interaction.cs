using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The nodes that register a REGION rather than paint one: what the host routes a press, a
/// hover, a key, a link or a drag to. Their pixels are their children's.
/// </summary>
internal sealed partial class EmitVisitor
{
    private void EmitPressable(Pressable pressable, EmitState s)
    {
        s.Input.Add(new HitRegion(ExpandHitRect(s.Node.Bounds, s.Press.Density), pressable, s.Node.Path ?? ""));

        // A PRESSABLE IS THE STOP FOR ITS SUBTREE, exactly as a link, an Adjustable and a Navigable
        // are, and for the reason all four share: `Visit(Pressable)` announces and CONSUMES, so
        // anything inside it is not announced — and a stop nothing names is an offer nothing
        // performs. Measured on a Link inside a Pressable: announced `Button@r/0`, stops `r/0` AND
        // `r/0/0`. A TextEntry inside one had the same shape before any of this, and takes the same
        // answer; the tree is nonsense either way (the web calls nested interactive elements
        // invalid), and what matters is that the two walks agree about what one control is.
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = s.Input.WithoutFocusStops() });
    }

    // Draws its subtree AS IF it were in these states. Nothing is tracked and no handler
    // runs — the states are pushed for the child's emission and restored after, so a
    // preview cannot leak into the sibling beside it.
    private void EmitSimulated(Simulated simulated, EmitState s)
    {
        var previous = s.Press.Simulated;
        // Nested previews COMBINE: a hovered card holding a pressed button is two nodes.
        s.Press.Simulated = previous | simulated.State;
        try
        {
            foreach (var child in s.Node)
                Emit(s with { Node = child });
        }
        finally
        {
            s.Press.Simulated = previous;
        }
    }

    // Presence, reported on the TRANSITIONS. The web observes; here the walk already has
    // the bounds and the surface, so the answer costs a rectangle comparison.
    private void EmitInView(Primitives.InView inView, EmitState s)
    {
        var bounds = s.Node.Bounds;
        var visible = s.Press.Surface;
        var overlapW = MathF.Min(bounds.X + bounds.Width, visible.X + visible.Width)
            - MathF.Max(bounds.X, visible.X);
        var overlapH = MathF.Min(bounds.Y + bounds.Height, visible.Y + visible.Height)
            - MathF.Max(bounds.Y, visible.Y);
        var shown = overlapW > 0 && overlapH > 0
            ? overlapW * overlapH / MathF.Max(bounds.Width * bounds.Height, 0.0001f)
            : 0f;
        // A threshold of 0 means ANY sliver, which is not the same as "zero of it".
        var onScreen = inView.Threshold <= 0 ? shown > 0 : shown >= inView.Threshold;
        if (s.Press.InView?.Changed(s.Node.Path ?? "", onScreen) == true)
            inView.OnChanged(onScreen);
        foreach (var child in s.Node)
            Emit(s with { Node = child });
    }

    // S5 programmable hover: the region rides the SAME pointer pipeline Style.Hover uses;
    // the host fires OnChanged on the transitions (PhotonHost.PointerMove).
    private void EmitHoverable(Hoverable hoverable, EmitState s)
    {
        s.Input.Add(new HoverRegion(s.Node.Bounds, hoverable, s.Node.Path ?? ""));
    }

    // ONE Tab stop for the whole control; the press targets inside it stay pointer-only —
    // stopping on "decrease half" and then "increase half" is two stops for one slider,
    // and neither of them answers to the arrows.
    private void EmitAdjustable(Adjustable adjustable, EmitState s)
    {
        s.Input.AddComposite(new FocusStop(s.Node.Path ?? "", null, null, s.Node.Bounds, adjustable));
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = s.Input.WithoutFocusStops() });
    }

    /// <summary>
    /// The 2-D twin of the line above, and the same two moves: ONE stop for the whole grid, and the
    /// cells inside it pointer-only. Its own node says why — "one Tab stop for the whole thing, and
    /// a keyboard that moves a selection around inside it" — and the moment the rows started laying
    /// out (#248) the alternative was measurable: a month became 31 Tab presses.
    /// </summary>
    private void EmitNavigable(Navigable navigable, EmitState s)
    {
        s.Input.AddComposite(new FocusStop(s.Node.Path ?? "", null, null, s.Node.Bounds, Grid: navigable));
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = s.Input.WithoutFocusStops() });
    }

    // Spec S8: being on screen IS the subscription — the binding lives for exactly as long
    // as this frame, so an unmounted dialog's Esc stops firing with no bookkeeping.
    private void EmitShortcut(Shortcut shortcut, EmitState s)
    {
        s.Input.Add(new ShortcutBinding(shortcut.Chord, shortcut.OnPressed));
    }

    private void EmitLink(Link link, EmitState s)
    {
        // Navigation surface: pure semantics — the child paints; a tap that no pressable claims
        // resolves to this region through the host's navigation seam.
        var path = s.Node.Path ?? "";
        s.Input.Add(new LinkRegion(s.Node.Bounds, link.Destination, path));

        // "Tab reaches every interactive control" (handoff, Foundations · Keyboard conventions), and
        // a link is one — the web gets the stop and the activate from <a href> for nothing, and
        // Photon announced a link it could then reach by neither (#255). The stop is the TAB half
        // only; following it is the REGION's, which is what survives a composite's suppression.
        s.Input.Add(new FocusStop(path, null, null, s.Node.Bounds));

        // The ring the Box arm draws for a focused control, drawn here because a link is words and
        // has no box of its own. Radius zero: the ring follows the text's own rectangle.
        if (s.Press.PendingFocusRing)
        {
            s.Press.PendingFocusRing = false;
            FocusRing(s, s.Node.Bounds, default);
        }

        // A LINK IS THE STOP FOR ITS SUBTREE, which the semantics walk already says by CONSUMING it
        // (Visit(Link) announces and stops). Without this the emit walk disagreed with the walk
        // beside it: a Link over a paragraph holding its own linked run registered two stops and
        // announced one, so Tab landed on an inner run no reader names — the empty offer #256 spent
        // two rounds removing, one level down. Measured: stops `r/0`, `r/0/0#0`; announced `r/0`.
        // The same shape as Adjustable and Navigable, for the same reason.
        foreach (var child in s.Node)
            Emit(s with { Node = child, Input = s.Input.WithoutFocusStops() });
    }

    private void EmitDrag(EmitState s, string dragPath)
    {
        // Gestures v2: the surface registers for the host's drag routing, and the current offset
        // (active follow or glide-back) paints as a translate — layout untouched, exactly like
        // loop motion. Hit regions inside register at their laid-out bounds; mid-drag taps are
        // cancelled by the slop rule, so the transient misalignment is unreachable.
        s.Input.Add(new DragRegion(s.Node.Bounds, dragPath, s.Node.Source));
        // The axis is the node's: a swipe-to-reveal travels sideways, a sheet down.
        var dragOffset = s.Node.DragOffset;
        var horizontal = s.Node.Source is Draggable { Axis: DragAxis.Horizontal };
        if (dragOffset != 0)
        {
            s.Builder.PushTransform(horizontal
                ? Matrix2D.Translation(dragOffset, 0)
                : Matrix2D.Translation(0, dragOffset));
        }
        foreach (var child in s.Node)
            Emit(s with { Node = child });
        if (dragOffset != 0) s.Builder.Pop();
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
