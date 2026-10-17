using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The wrappers that take no space of their own and resolve something else while they pass
/// the constraints through — a gesture's offset, an entrance's progress, a component's subtree.</summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>
    /// An <see cref="InFlow"/> measures as the wrapper it is. The intent it arms ("the overlay
    /// inside builds its panel rather than its layer") is armed where the inside BUILDS, which is
    /// the build phase now: each component's element knows whether an InFlow is above it.
    /// </summary>
    private LayoutNode MeasureInFlow(InFlow inFlow, LayoutConstraints constraints, LayoutContext ctx,
        Position at) => MeasureWrapper(inFlow, inFlow.Child, constraints, ctx, at);

    /// <summary>
    /// A component measures as a wrapper around what its element BUILT, and builds nothing here: the
    /// build phase ran before the layout (docs/ELEMENT-TREE-PLAN.md, 3.2), through the boundary, so a
    /// throw or a component that builds itself was contained there. A container that measures this
    /// twice measures one built subtree twice. The wrapper's node is the instance that built, the
    /// retained one for a stateful component.
    /// </summary>
    private LayoutNode MeasureComponent(UiComponent component, LayoutConstraints constraints, LayoutContext ctx, Position at)
    {
        if (at.Element is not ComponentElement element || element.ChildAt(0) is not { } built)
            throw new InvalidOperationException(
                $"{at.Path} measured the component {component.GetType().Name} with no subtree built for it: "
                + "no build made its position, or its Build answered null.");
        return MeasureWrapper(element.Component, built.Configuration, constraints, ctx, at);
    }

    /// <summary>A transparent wrapper that also resolves the ENTRANCE progress against the host's
    /// presence clock, keyed by this stable path — the emit pass applies the paint-only effect and
    /// snapshots the subtree's commands by the same path (the exit replay source).</summary>
    private LayoutNode MeasurePresence(Presence presence, LayoutConstraints constraints, LayoutContext ctx, Position at)
    {
        var result = MeasureWrapper(presence, presence.Child, constraints, ctx, at);
        result.Presence = ctx.Presences?.Progress(at.Path, ctx.TimeMs, ctx.ReducedMotion) ?? 1f;
        result.PresencePath = at.Path;
        return result;
    }

    /// <summary>A transparent wrapper that resolves the current DRAG offset against the host's drag
    /// clock (active follow or glide-back), keyed by this stable path — the emit pass paints the
    /// translate and registers the drag region the host routes input by.</summary>
    private LayoutNode MeasureDragDismiss(DragDismiss drag, LayoutConstraints constraints, LayoutContext ctx, Position at)
    {
        var result = MeasureWrapper(drag, drag.Child, constraints, ctx, at);
        result.DragOffset = ctx.Drags?.Resolve(at.Path, ctx.TimeMs) ?? 0f;
        result.DragPath = at.Path;
        return result;
    }

    /// <summary>The live offset for this gesture — the finger while it is down, the glide after it
    /// lifts, and the caller's RestOffset when neither is happening.</summary>
    private LayoutNode MeasureDraggable(Draggable draggable, LayoutConstraints constraints,
        LayoutContext ctx, Position at)
    {
        var result = MeasureWrapper(draggable, draggable.Child, constraints, ctx, at);
        // A gesture the caller paints itself never translates — its offset lives in the caller's
        // state and has already moved the subtree by the time this frame is measured.
        result.DragOffset = draggable.Follows
            ? ctx.Drags?.Resolve(at.Path, ctx.TimeMs, draggable.RestOffset) ?? draggable.RestOffset
            : 0f;
        result.DragPath = at.Path;
        return result;
    }
}
