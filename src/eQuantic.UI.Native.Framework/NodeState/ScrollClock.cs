using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The host's scroll MOTION (scroll compositor v1): whether a wheel glides, the scroll views gliding
/// toward where a wheel or a fling sent them, and their advance before each frame. Where a view IS
/// lives on its position (<see cref="NodeStates"/>), so the next scroll view drawn at its place
/// starts at its own offset; the host moves a view through this clock, by the view's element, and the
/// layout reads the offset off the element with no clock at all.
/// </summary>
public sealed class ScrollClock
{
    /// <summary>
    /// How much of the remaining distance is covered each millisecond, as a fraction. Exponential
    /// rather than a fixed duration because notches ARRIVE while the last one is still settling:
    /// a duration would restart on every one, and the content would crawl. Chasing a target eats
    /// them all — the wheel stays in charge, the pixels just stop teleporting.
    /// </summary>
    private const float SmoothingPerMs = 0.022f;

    /// <summary>Below this the glide is over — a fraction of a pixel is not a frame's worth.</summary>
    private const float RestingDp = 0.5f;

    /// <summary>
    /// How far a fling carries per dp/ms of release velocity. The glide's own decay does the
    /// slowing down, so this only says how much runway the flick buys.
    /// </summary>
    private const float FlingReach = 90;

    private readonly List<ScrollState> _gliding = new();
    private float _lastTimeMs = float.NaN;

    /// <summary>
    /// Whether a wheel or drag glides to where it was sent instead of jumping. On by default; the
    /// host turns it off for an app that asked, and ALWAYS off under Reduce Motion — a smooth
    /// scroll is movement, and that setting means what it says.
    /// </summary>
    public bool Smooth { get; set; } = true;

    /// <summary>The offset the host scrolled the view at <paramref name="at"/> to, or null when it
    /// never did, or there is no view there.</summary>
    public static float? OffsetOf(Element? at) => NodeStates.Find<ScrollState>(at)?.Offset;

    /// <summary>
    /// Puts an offset exactly where the caller says, now. This is what a FINGER does: the content
    /// is under it and follows it, with nothing to smooth — smoothing a direct manipulation is how
    /// a surface stops feeling attached to the hand.
    /// </summary>
    public bool ScrollTo(Element at, float offset, float maxOffset)
    {
        if (NodeStates.For<ScrollState>(at) is not { } view) return false;
        var next = Math.Clamp(offset, 0, MathF.Max(0, maxOffset));
        if (view.Offset is { } current && current == next) return false;
        view.Offset = next;
        view.Target = null;
        return true;
    }

    /// <summary>
    /// Carries on after the finger lifts, in proportion to how fast it was moving. The glide is
    /// already an exponential decay, so a fling is nothing more than a target far enough away —
    /// the slowing down comes free and matches every other movement in the app.
    /// </summary>
    public bool Fling(Element at, float velocity, float maxOffset)
    {
        if (!Smooth || MathF.Abs(velocity) < 0.05f) return false;
        if (NodeStates.For<ScrollState>(at) is not { } view) return false;
        var current = StandingAt(view, 0, maxOffset);
        var target = Math.Clamp(current + velocity * FlingReach, 0, MathF.Max(0, maxOffset));
        if (target == current) return false;
        Glide(view, target);
        return true;
    }

    /// <summary>Adjusts an offset by <paramref name="delta"/> (positive scrolls toward the content
    /// end), clamped to [0, <paramref name="maxOffset"/>]. Returns true when the value changed.</summary>
    public bool ScrollBy(Element at, float delta, float maxOffset, float fallback = 0)
    {
        if (NodeStates.For<ScrollState>(at) is not { } view) return false;
        var current = StandingAt(view, fallback, maxOffset);
        var from = Smooth && view.Target is { } pending ? pending : current;
        var next = Math.Clamp(from + delta, 0, MathF.Max(0, maxOffset));

        if (!Smooth)
        {
            if (next == current) return false;
            view.Offset = next;
            return true;
        }

        if (next == from && MathF.Abs(next - current) < RestingDp) return false;
        Glide(view, next);
        return true;
    }

    /// <summary>
    /// Where <paramref name="view"/> stands in a range of <paramref name="maxOffset"/>, written back to it: an
    /// offset an app asked for past the end (ScrollView.Request) is kept as asked until the view is moved, and
    /// stands at the end, so a wheel or a fling starts from there. Left as asked, a glide would have started
    /// from the hidden excess, and the view stayed at the end until the glide had crossed it.
    /// </summary>
    private static float StandingAt(ScrollState view, float fallback, float maxOffset)
    {
        var standing = Math.Clamp(view.Offset ?? fallback, 0, MathF.Max(0, maxOffset));
        if (view.Offset is { } asked && asked != standing) view.Offset = standing;
        return standing;
    }

    private void Glide(ScrollState view, float target)
    {
        view.Target = target;
        if (view.Listed) return;
        view.Listed = true;
        _gliding.Add(view);
    }

    /// <summary>
    /// Moves every gliding offset toward its target for the time that passed. Returns true while
    /// anything is still moving, which is what keeps the frames coming — and stops them the moment
    /// it arrives, so a still page costs nothing. A view whose position left the tree stops where it
    /// was, and so does one a finger took since (<see cref="ScrollTo"/> drops its target).
    /// </summary>
    public bool Advance(float timeMs)
    {
        var elapsed = float.IsNaN(_lastTimeMs) ? 0 : MathF.Max(0, timeMs - _lastTimeMs);
        _lastTimeMs = timeMs;
        for (var i = _gliding.Count - 1; i >= 0; i--)
        {
            var view = _gliding[i];
            if (view.Target is not null && !view.Released) continue;
            view.Listed = false;
            _gliding.RemoveAt(i);
        }

        if (_gliding.Count == 0) return false;

        // Smoothing switched off with a glide in flight — because the app said so, or because
        // Reduce Motion arrived. What was asked for still has to HAPPEN; it just happens now.
        if (!Smooth)
        {
            foreach (var view in _gliding) Land(view);
            _gliding.Clear();
            return true;
        }

        if (elapsed <= 0) return false;

        // 1 - (1-k)^dt: the same fraction per millisecond however long the frame took, so the glide
        // looks the same at 120 Hz and at 30.
        var step = 1 - MathF.Pow(1 - SmoothingPerMs, elapsed);
        for (var i = _gliding.Count - 1; i >= 0; i--)
        {
            var view = _gliding[i];
            var target = view.Target!.Value;
            var current = view.Offset ?? 0;
            var distance = target - current;
            if (MathF.Abs(distance) < RestingDp)
            {
                // One last frame lands exactly on the target.
                Land(view);
                _gliding.RemoveAt(i);
                continue;
            }

            view.Offset = current + distance * step;
        }

        return true;
    }

    private static void Land(ScrollState view)
    {
        view.Offset = view.Target;
        view.Target = null;
        view.Listed = false;
    }
}
