namespace eQuantic.UI.Primitives;

/// <summary>
/// ENTER MOTION for state transitions (spec §06 — the C2/C3/C4 fence): wraps a subtree whose
/// DECLARATIVE appearance (`if (_open) …`) should animate in instead of popping. Layout-transparent
/// like <see cref="LoopMotion"/> — the effect is paint-only (opacity layer + translate), so frames
/// never re-lay-out. First sighting starts the entrance; a subtree that leaves and later returns
/// replays it (presence tracking prunes departed paths). Reduce Motion replaces movement with a
/// short crossfade (<c>Motion.ReducedCrossfadeMs</c> — spec §06's static-replacement rule). Web
/// lowers to a mount-playing CSS animation class; native resolves a presence clock per layout path.
/// v1 is ENTER only: exit (removal-deferred motion) stays a fence on both targets.
/// </summary>
public sealed class Presence : SingleChildNode
{
    public override string NodeKind => "presence";

    /// <summary>The SlideUp rise distance (dp) — one spacing step, not a journey (spec §06).</summary>
    public const float SlideDistance = 16;

    public Presence(VisualNode child, PresenceMotion enter = PresenceMotion.Fade)
        : base(child)
    {
        Enter = enter;
    }

    public PresenceMotion Enter { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
