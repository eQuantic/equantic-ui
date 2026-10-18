using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a gliding node's position keeps (a styled box, a pinned header, a weighted child): one track
/// per number it glides, and how many custom shadows it drew last. Resolved through the host's
/// <see cref="TransitionClock"/>.
/// </summary>
internal sealed class TransitionState
{
    private sealed class Track
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

    /// <summary>The tracks, by key and colour component (0 for a number, 0 to 3 for a colour's red,
    /// green, blue and alpha).</summary>
    private readonly Dictionary<(TrackKey Key, int Component), Track> _tracks = new();

    /// <summary>How many custom shadows the box drew the last frame it was drawn, or null the first
    /// time (see <see cref="TransitionClock.ShadowsDrawn"/>).</summary>
    public int? ShadowsDrawn;

    /// <summary>
    /// The value of a track this frame under <paramref name="motion"/>: the target the first time
    /// (transitions animate changes, not appearances), the interpolation otherwise, retargeted from
    /// where it is now when the target changed. <paramref name="moving"/> says whether it is still
    /// on its way.
    /// </summary>
    public float Resolve(TrackKey key, int component, float target, float timeMs, TransitionSpec motion,
        out bool moving)
    {
        if (!_tracks.TryGetValue((key, component), out var track))
        {
            // First sighting: mount AT the target — transitions animate changes, not appearances.
            _tracks[(key, component)] = new Track
            {
                From = target, To = target, StartMs = timeMs,
                DurationMs = motion.DurationMs, DelayMs = motion.DelayMs, Easing = motion.Easing,
            };
            moving = false;
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

        moving = current != track.To;
        return current;
    }

    /// <summary>Snaps a track: forgets it, so a later spec starts from the value shown.</summary>
    public void Forget(TrackKey key, int component) => _tracks.Remove((key, component));

    /// <summary>The value a track is gliding TOWARD, or null when there is no such track.</summary>
    public float? Target(TrackKey key, int component) =>
        _tracks.TryGetValue((key, component), out var track) ? track.To : null;

    private static float ValueAt(Track track, float timeMs)
    {
        if (track.From == track.To) return track.To;
        // The delay holds the FROM value; the curve is the author's, evaluated for real — the
        // smoothstep that stood in here made every Photon animation move on one curve while the
        // web moved on the one the token named.
        var t = Math.Clamp((timeMs - track.StartMs - track.DelayMs) / track.DurationMs, 0f, 1f);
        return track.From + (track.To - track.From) * track.Easing.Ease(t);
    }
}
