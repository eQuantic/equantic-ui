using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Spec B14 — the native VALUE-TRANSITION animator: when a tracked value CHANGES between frames, it
/// interpolates over Motion.Base (200ms) instead of snapping — the Photon twin of the web's
/// <c>transition: flex-grow</c>. Deliberately a FUNCTION OF TIME like LoopMotion: the current value
/// is pure in (path, target, timeMs), so frames stay deterministic and tests pin a fixed t.
/// Rules (CSS parity): the FIRST sighting mounts at the target (transitions animate changes, not
/// mounts); a mid-flight retarget starts from the current interpolated value; Reduce Motion and
/// untracked values snap. The easing is a smoothstep stand-in for the standard curve (the exact
/// curve pack is a motion fence).
/// </summary>
public sealed class TransitionStore
{
    private sealed class Transition
    {
        public float From;
        public float To;
        public float StartMs;
    }

    private readonly Dictionary<string, Transition> _tracks = new();

    /// <summary>True when any transition was mid-flight during the current frame — the host keeps
    /// scheduling frames while set. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    public void BeginFrame() => AnyActive = false;

    /// <summary>
    /// The value to LAYOUT WITH this frame. <paramref name="animate"/> false (or reduced motion)
    /// snaps and clears tracking; otherwise a changed target starts a Base-duration interpolation
    /// from the currently shown value.
    /// </summary>
    public float Resolve(string path, float target, float timeMs, bool animate, bool reducedMotion)
    {
        if (!animate || reducedMotion)
        {
            _tracks.Remove(path);
            return target;
        }

        if (!_tracks.TryGetValue(path, out var track))
        {
            // First sighting: mount AT the target — transitions animate changes, not appearances.
            _tracks[path] = new Transition { From = target, To = target, StartMs = timeMs };
            return target;
        }

        var current = ValueAt(track, timeMs);
        if (track.To != target)
        {
            // Retarget: continue from wherever the interpolation currently is.
            track.From = current;
            track.To = target;
            track.StartMs = timeMs;
            current = ValueAt(track, timeMs);
        }

        if (current != track.To) AnyActive = true;
        return current;
    }

    private static float ValueAt(Transition track, float timeMs)
    {
        if (track.From == track.To) return track.To;
        var t = Math.Clamp((timeMs - track.StartMs) / Motion.BaseMs, 0f, 1f);
        var eased = t * t * (3f - 2f * t); // smoothstep — the standard-curve stand-in (motion fence)
        return track.From + (track.To - track.From) * eased;
    }
}
