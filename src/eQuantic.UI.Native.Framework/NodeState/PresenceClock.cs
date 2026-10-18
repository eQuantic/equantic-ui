using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Spec §06 — the native STATE-TRANSITION clock behind <see cref="Presence"/>, both halves:
/// <para>ENTER — the first frame a presence's position is measured starts its entrance; progress is a
/// pure function of the frame's time from then on (deterministic frames, goldens pin a fixed t). The
/// entrance belongs to the position (<see cref="NodeStates"/>): a presence drawn at the place of one
/// that left enters on its own, and one that leaves and returns replays its entrance.</para>
/// <para>EXIT — the paint walk SNAPSHOTS each live presence's draw commands every frame; when its
/// position leaves the tree, the last snapshot is handed here and replays with falling opacity (and
/// the SlideUp reverse drop) over Motion.Fast, from the frame it left in. Exits always run to
/// completion — a presence that re-enters mid-exit starts a fresh entrance WHILE the old copy fades
/// (the cross-fade the web gets by construction). Hit regions are never snapshotted: a departed
/// subtree is pixels only, input passes through immediately.</para>
/// Reduce Motion swaps both clocks for the spec's short crossfade (movement dropped by the caller). A
/// frame realized without a clock draws every presence settled.
/// </summary>
public sealed class PresenceClock
{
    private sealed class Exit
    {
        public required DrawCommand[] Commands;
        public required PresenceMotion Kind;
        public required float StartMs;
        // The frame the exit started in, and when its paint was painted: the exits of one frame are
        // drawn in the order their presences were.
        public required int Frame;
        public required long PaintedAt;
    }

    /// <summary>The exits in flight, in the order they are drawn: by the frame each started in, and
    /// within a frame by when each was painted.</summary>
    private readonly List<Exit> _exits = new();
    private float _frameTimeMs;
    private int _frame;
    private long _painted;

    /// <summary>True when any entrance or exit was mid-flight during the current frame — the host
    /// keeps scheduling frames while set. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    /// <summary>Starts a frame at <paramref name="timeMs"/>, the time a presence that leaves in it
    /// starts its exit at.</summary>
    public void BeginFrame(float timeMs)
    {
        AnyActive = false;
        _frameTimeMs = timeMs;
        _frame++;
    }

    /// <summary>
    /// The eased entrance progress (0..1) of the presence at <paramref name="at"/> this frame. Its
    /// first measure registers <paramref name="timeMs"/> as its start — progress 0, the entrance
    /// begins. A presence with no position has nothing to remember an entrance by, and is settled.
    /// </summary>
    public float Progress(Element? at, float timeMs, bool reducedMotion)
    {
        if (NodeStates.For<PresenceState>(at) is not { } presence) return 1f;
        presence.Clock = this;
        if (float.IsNaN(presence.EnteredAtMs)) presence.EnteredAtMs = timeMs;

        var duration = reducedMotion ? Motion.ReducedCrossfadeMs : Motion.BaseMs;
        var t = Math.Clamp((timeMs - presence.EnteredAtMs) / duration, 0f, 1f);
        if (t < 1f) AnyActive = true;
        return Smoothstep(t);
    }

    /// <summary>The paint walk records each live presence's commands, in the order it paints them:
    /// the frame its position leaves in replays the LAST of them as the exit.</summary>
    public void Snapshot(Element? at, PresenceMotion kind, DrawCommand[] commands)
    {
        if (NodeStates.Find<PresenceState>(at) is not { } presence) return;
        presence.Kind = kind;
        presence.LastPaint = commands;
        presence.PaintedAt = ++_painted;
    }

    /// <summary>
    /// A presence's position left the tree: its last paint exits from this frame. The exits that
    /// start in one frame are drawn in the order their presences were painted, whatever order their
    /// positions left in: a list's surplus members leave last first, and the layers a frame did not
    /// show are swept last first, so arriving in that order would draw the one that was on top
    /// underneath while both fade.
    /// </summary>
    internal void Depart(PresenceMotion kind, DrawCommand[] commands, long paintedAt)
    {
        var at = _exits.Count;
        while (at > 0 && _exits[at - 1].Frame == _frame && _exits[at - 1].PaintedAt > paintedAt) at--;
        _exits.Insert(at, new Exit
        {
            Commands = commands, Kind = kind, StartMs = _frameTimeMs, Frame = _frame, PaintedAt = paintedAt,
        });
    }

    /// <summary>One departed subtree's replay for this frame: the cached commands at the falling
    /// layer alpha, dropped by the SlideUp reverse rise (0 for Fade or Reduce Motion).</summary>
    public readonly record struct ExitFrame(DrawCommand[] Commands, float Alpha, float Drop);

    /// <summary>
    /// The exits still mid-flight at <paramref name="timeMs"/> (finished ones are evicted). Marks
    /// the frame active while any remain — the host keeps scheduling until every exit lands.
    /// </summary>
    public IReadOnlyList<ExitFrame> ActiveExits(float timeMs, bool reducedMotion)
    {
        if (_exits.Count == 0) return [];

        var frames = new List<ExitFrame>(_exits.Count);
        var duration = reducedMotion ? Motion.ReducedCrossfadeMs : Motion.FastMs;
        for (var i = 0; i < _exits.Count; i++)
        {
            var exit = _exits[i];
            var t = Math.Clamp((timeMs - exit.StartMs) / duration, 0f, 1f);
            if (t >= 1f)
            {
                _exits.RemoveAt(i--);
                continue;
            }

            AnyActive = true;
            var eased = Smoothstep(t);
            var drop = exit.Kind == PresenceMotion.SlideUp && !reducedMotion
                ? eased * Presence.SlideDistance
                : 0f;
            frames.Add(new ExitFrame(exit.Commands, 1f - eased, drop));
        }

        return frames;
    }

    private static float Smoothstep(float t) => t * t * (3f - 2f * t);
}
