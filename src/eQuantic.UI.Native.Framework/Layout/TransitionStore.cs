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
        // The spec the track was retargeted under — duration and curve are the AUTHOR's, per node,
        // not one constant for every animation on the target.
        public float DurationMs;
        public float DelayMs;
        public Curve Easing;
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
    /// <summary>The flex-weight form (spec B14): glides over the base motion on the standard curve.</summary>
    public float Resolve(string path, float target, float timeMs, bool animate, bool reducedMotion) =>
        Resolve(path, target, timeMs, animate ? Default : null, reducedMotion);

    private static readonly TransitionSpec Default = new(StyleChannels.Size);

    /// <summary>
    /// The value to LAY OUT or PAINT for <paramref name="path"/> this frame. A null
    /// <paramref name="spec"/> snaps and forgets the track; a spec animates changes under ITS
    /// duration, delay and curve — so the box that said <c>Transition = TransitionSpec.Of(Colors,
    /// Motion.Press)</c> moves in 100 ms on the standard curve, and the one beside it that said
    /// nothing snaps, exactly as the same two boxes do in a browser.
    /// </summary>
    public float Resolve(string path, float target, float timeMs, TransitionSpec? spec, bool reducedMotion)
    {
        if (spec is not { } motion || reducedMotion || motion.DurationMs <= 0)
        {
            _tracks.Remove(path);
            return target;
        }

        if (!_tracks.TryGetValue(path, out var track))
        {
            // First sighting: mount AT the target — transitions animate changes, not appearances.
            _tracks[path] = new Transition
            {
                From = target, To = target, StartMs = timeMs,
                DurationMs = motion.DurationMs, DelayMs = motion.DelayMs, Easing = motion.Easing,
            };
            return target;
        }

        var current = ValueAt(track, timeMs);
        if (track.To != target)
        {
            // Retarget: continue from wherever the interpolation currently is, under the spec in
            // force NOW — an author who changed the duration between renders meant the new one.
            track.From = current;
            track.To = target;
            track.StartMs = timeMs;
            track.DurationMs = motion.DurationMs;
            track.DelayMs = motion.DelayMs;
            track.Easing = motion.Easing;
            current = ValueAt(track, timeMs);
        }

        if (current != track.To) AnyActive = true;
        return current;
    }

    private static float ValueAt(Transition track, float timeMs)
    {
        if (track.From == track.To) return track.To;
        // The delay holds the FROM value; the curve is the author's, evaluated for real — the
        // smoothstep that stood in here made every Photon animation move on one curve while the
        // web moved on the one the token named.
        var t = Math.Clamp((timeMs - track.StartMs - track.DelayMs) / track.DurationMs, 0f, 1f);
        return track.From + (track.To - track.From) * track.Easing.Ease(t);
    }
}
