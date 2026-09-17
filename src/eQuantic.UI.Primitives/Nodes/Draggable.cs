namespace eQuantic.UI.Primitives;

/// <summary>
/// A CONTINUOUS gesture (spec S9): the child follows the finger along one axis between
/// <see cref="Min"/> and <see cref="Max"/>, and on release the caller is told where it ended so it
/// can decide what that meant. Swipe-to-reveal, pull-to-refresh and a sheet's own detents are all
/// this node plus a decision.
/// <para>
/// CONTROLLED like everything else: the node does not remember where it was left. The host tracks
/// the LIVE finger, and <see cref="RestOffset"/> — which the caller owns — is where the child glides
/// when the finger lifts. A row that stays open after a swipe is a caller that set RestOffset to
/// -96 in <see cref="OnReleased"/>; one that springs back is a caller that left it at 0.
/// </para>
/// </summary>
public sealed class Draggable : SingleChildNode
{
    public override string NodeKind => "draggable";

    public Draggable(VisualNode child, Action<float>? onReleased = null)
        : base(child)
    {
        OnReleased = onReleased;
    }


    public DragAxis Axis { get; init; }

    /// <summary>Travel limits along the axis. Negative is up / towards the start.</summary>
    public float Min { get; init; }
    public float Max { get; init; }

    /// <summary>Where the child sits when no finger is on it — the CALLER's state, not the node's.</summary>
    public float RestOffset { get; init; }

    /// <summary>The offset at the moment of release. The caller decides what it meant.</summary>
    public Action<float>? OnReleased { get; init; }

    /// <summary>
    /// Reported on EVERY move while the gesture is live — a slider's value has to follow the thumb,
    /// not wait for the finger to lift. Opt-in, because it costs a re-render per frame: leave it
    /// null and the offset paints without rebuilding anything, which is what a sheet wants.
    /// </summary>
    public Action<float>? OnMoved { get; init; }

    /// <summary>
    /// Report the offset as a FRACTION of the surface's own extent along the axis, rather than in
    /// dp. A slider's track is fluid — the component cannot know its pixel width, and the host can,
    /// so it converts. <see cref="Min"/> and <see cref="Max"/> are then fractions too.
    /// </summary>
    public bool Normalized { get; init; }

    /// <summary>
    /// Whether the subtree follows the finger. True for anything that IS the thing being moved — a
    /// sheet, a swiped row. False when the caller repaints the position itself from
    /// <see cref="OnMoved"/>: a slider's thumb is placed by its value, and translating it as well
    /// would move it twice.
    /// </summary>
    public bool Follows { get; init; } = true;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
