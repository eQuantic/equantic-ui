using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The wrappers that take no space of their own and resolve something else while they pass
/// the constraints through — a gesture's offset, an entrance's progress, a component's subtree.</summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>
    /// Measures an <see cref="InFlow"/> with the intent armed, so the overlay inside it builds its
    /// panel rather than its layer. Restored after — a sibling dialog opened for real must still
    /// take the viewport.
    /// </summary>
    private LayoutNode MeasureInFlow(InFlow inFlow, LayoutConstraints constraints, LayoutContext ctx,
        string path)
    {
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

    private LayoutNode MeasureComponent(UiComponent component, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var resolved = ctx.Instances?.Reconcile(path, component) ?? component;
        // Through the BOUNDARY, never Build: a throw here used to reach the host and cost the FRAME —
        // the window stops presenting and the app is gone, for one component's null reference. And
        // ExpandContained rather than BuildContained, because this recurses: measuring what a
        // component built reaches MeasureComponent again, so a component that builds itself would
        // otherwise take the frame down by a stack overflow instead of a throw.
        // The visitor rides in the STATE rather than being captured. The lambda is `static` for a
        // measured reason: #224 landed this callback as a closure and PerfHarnessTests reported
        // 2,786 bytes a frame for it. A tuple field costs nothing — it is already a value being
        // passed — where a capture is a heap object per component per frame.
        return resolved.ExpandContained(ctx.Components, (self: this, resolved, constraints, ctx, path),
            static (built, state) =>
                state.self.MeasureWrapper(state.resolved, built, state.constraints, state.ctx, state.path));
    }

    /// <summary>A transparent wrapper that also resolves the ENTRANCE progress against the host's
    /// presence clock, keyed by this stable path — the emit pass applies the paint-only effect and
    /// snapshots the subtree's commands by the same path (the exit replay source).</summary>
    private LayoutNode MeasurePresence(Presence presence, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(presence, presence.Child, constraints, ctx, path);
        result.Presence = ctx.Presences?.Progress(path, ctx.TimeMs, ctx.ReducedMotion) ?? 1f;
        result.PresencePath = path;
        return result;
    }

    /// <summary>A transparent wrapper that resolves the current DRAG offset against the host's drag
    /// clock (active follow or glide-back), keyed by this stable path — the emit pass paints the
    /// translate and registers the drag region the host routes input by.</summary>
    private LayoutNode MeasureDragDismiss(DragDismiss drag, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(drag, drag.Child, constraints, ctx, path);
        result.DragOffset = ctx.Drags?.Resolve(path, ctx.TimeMs) ?? 0f;
        result.DragPath = path;
        return result;
    }

    /// <summary>The live offset for this gesture — the finger while it is down, the glide after it
    /// lifts, and the caller's RestOffset when neither is happening.</summary>
    private LayoutNode MeasureDraggable(Draggable draggable, LayoutConstraints constraints,
        LayoutContext ctx, string path)
    {
        var result = MeasureWrapper(draggable, draggable.Child, constraints, ctx, path);
        // A gesture the caller paints itself never translates — its offset lives in the caller's
        // state and has already moved the subtree by the time this frame is measured.
        result.DragOffset = draggable.Follows
            ? ctx.Drags?.Resolve(path, ctx.TimeMs, draggable.RestOffset) ?? draggable.RestOffset
            : 0f;
        result.DragPath = path;
        return result;
    }
}
