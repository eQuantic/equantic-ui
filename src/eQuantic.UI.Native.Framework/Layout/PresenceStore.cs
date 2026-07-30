using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Spec §06 — the native STATE-TRANSITION clock behind <see cref="Presence"/>, both halves:
/// <para>ENTER — the first frame a presence path is seen starts its entrance; progress is a pure
/// function of (path, timeMs) from then on (deterministic frames, goldens pin a fixed t). Departed
/// paths are pruned so a subtree that leaves and later returns replays its entrance.</para>
/// <para>EXIT — the emit pass SNAPSHOTS each live presence subtree's draw commands every frame; when
/// the path departs, the last snapshot replays with falling opacity (and the SlideUp reverse drop)
/// over Motion.Fast. Exits always run to completion — a path that re-enters mid-exit starts a fresh
/// entrance WHILE the old copy fades (the cross-fade the web gets by construction). Hit regions are
/// never snapshotted: a departed subtree is pixels only, input passes through immediately.</para>
/// Reduce Motion swaps both clocks for the spec's short crossfade (movement dropped by the caller).
/// Easing is the same smoothstep standard-curve stand-in the value animator uses (curve pack fence).
/// </summary>
public sealed class PresenceStore
{
    private sealed class Exit
    {
        public required DrawCommand[] Commands;
        public required PresenceMotion Kind;
        public required float StartMs;
    }

    private readonly Dictionary<string, float> _firstSeen = new();
    private readonly Dictionary<string, (PresenceMotion Kind, DrawCommand[] Commands)> _snapshots = new();
    private readonly Dictionary<string, Exit> _exits = new();
    private readonly HashSet<string> _seenThisFrame = new();

    /// <summary>True when any entrance or exit was mid-flight during the current frame — the host
    /// keeps scheduling frames while set. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    public void BeginFrame()
    {
        AnyActive = false;
        _seenThisFrame.Clear();
    }

    /// <summary>
    /// The eased entrance progress (0..1) for a presence path this frame. A path's first sighting
    /// registers <paramref name="timeMs"/> as its start — progress 0, the entrance begins.
    /// </summary>
    public float Progress(string path, float timeMs, bool reducedMotion)
    {
        _seenThisFrame.Add(path);
        if (!_firstSeen.TryGetValue(path, out var start))
        {
            _firstSeen[path] = start = timeMs;
        }

        var duration = reducedMotion ? Motion.ReducedCrossfadeMs : Motion.BaseMs;
        var t = Math.Clamp((timeMs - start) / duration, 0f, 1f);
        if (t < 1f) AnyActive = true;
        return Smoothstep(t);
    }

    /// <summary>The emit pass records each live presence subtree's commands — the frame that finds
    /// the path gone replays the LAST snapshot as the exit.</summary>
    public void Snapshot(string path, PresenceMotion kind, DrawCommand[] commands) =>
        _snapshots[path] = (kind, commands);

    /// <summary>
    /// Prunes paths not seen this frame — each departure spawns an EXIT from its last snapshot
    /// (starting at <paramref name="timeMs"/>), so the re-entry contract holds AND the subtree
    /// fades out instead of popping. Call after ALL passes — overlay paths register there.
    /// </summary>
    public void EndFrame(float timeMs)
    {
        if (_firstSeen.Count == 0) return;
        List<string>? departed = null;
        foreach (var path in _firstSeen.Keys)
        {
            if (!_seenThisFrame.Contains(path)) (departed ??= new List<string>()).Add(path);
        }
        if (departed == null) return;

        foreach (var path in departed)
        {
            _firstSeen.Remove(path);
            if (_snapshots.Remove(path, out var snapshot))
            {
                _exits[path] = new Exit { Commands = snapshot.Commands, Kind = snapshot.Kind, StartMs = timeMs };
            }
        }
    }

    /// <summary>One departed subtree's replay for this frame: the cached commands at the falling
    /// layer alpha, dropped by the SlideUp reverse rise (0 for Fade or Reduce Motion).</summary>
    public readonly record struct ExitFrame(DrawCommand[] Commands, float Alpha, float Drop);

    /// <summary>
    /// The exits still mid-flight at <paramref name="timeMs"/> (finished ones are evicted). Marks
    /// the frame active while any remain — the host keeps scheduling until every exit lands.
    /// </summary>
    public List<ExitFrame> ActiveExits(float timeMs, bool reducedMotion)
    {
        var frames = new List<ExitFrame>();
        if (_exits.Count == 0) return frames;

        var duration = reducedMotion ? Motion.ReducedCrossfadeMs : Motion.FastMs;
        List<string>? finished = null;
        foreach (var (path, exit) in _exits)
        {
            var t = Math.Clamp((timeMs - exit.StartMs) / duration, 0f, 1f);
            if (t >= 1f)
            {
                (finished ??= new List<string>()).Add(path);
                continue;
            }

            AnyActive = true;
            var eased = Smoothstep(t);
            var drop = exit.Kind == PresenceMotion.SlideUp && !reducedMotion
                ? eased * Presence.SlideDistance
                : 0f;
            frames.Add(new ExitFrame(exit.Commands, 1f - eased, drop));
        }

        if (finished != null)
        {
            foreach (var path in finished) _exits.Remove(path);
        }
        return frames;
    }

    private static float Smoothstep(float t) => t * t * (3f - 2f * t);
}
