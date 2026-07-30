using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Spec §06 — the native ENTER-MOTION clock behind <see cref="Presence"/>: the first frame a
/// presence path is seen starts its entrance, and progress is a pure function of (path, timeMs)
/// from then on — the same deterministic-frames philosophy as LoopMotion/TransitionStore (goldens
/// pin a fixed t). Departed paths are PRUNED at end-of-frame so a subtree that leaves and later
/// returns replays its entrance (the declarative `if (_open)` contract). Reduce Motion swaps the
/// Base-duration entrance for the spec's short crossfade (movement is the realizer's concern —
/// it drops the slide; the store only shortens the clock). Easing is the same smoothstep
/// standard-curve stand-in the value animator uses (exact curve pack = motion fence).
/// </summary>
public sealed class PresenceStore
{
    private readonly Dictionary<string, float> _firstSeen = new();
    private readonly HashSet<string> _seenThisFrame = new();

    /// <summary>True when any entrance was mid-flight during the current frame — the host keeps
    /// scheduling frames while set. Reset by <see cref="BeginFrame"/>.</summary>
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
        return t * t * (3f - 2f * t); // smoothstep — the standard-curve stand-in (motion fence)
    }

    /// <summary>Prunes paths that were not seen this frame, so a departed subtree re-enters fresh
    /// (its entrance replays). Call after ALL passes — the overlay pass registers paths too.</summary>
    public void EndFrame()
    {
        if (_firstSeen.Count == 0) return;
        var departed = new List<string>();
        foreach (var path in _firstSeen.Keys)
        {
            if (!_seenThisFrame.Contains(path)) departed.Add(path);
        }
        foreach (var path in departed) _firstSeen.Remove(path);
    }
}
