namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The host-owned SCROLL STATE (scroll compositor v1): current offsets per ScrollView, keyed by the
/// node's stable layout PATH (trees rebuild every frame; paths don't). Layout resolves the effective
/// offset from here — the node's programmatic <c>Offset</c> is only the untouched default — and the
/// host mutates it from pointer/wheel input, clamped to the frame's measured max.
/// </summary>
public sealed class ScrollStore
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

    private readonly Dictionary<string, float> _offsets = new();
    private readonly Dictionary<string, float> _targets = new();
    private float _lastTimeMs = float.NaN;

    /// <summary>
    /// Whether a wheel or drag glides to where it was sent instead of jumping. On by default; the
    /// host turns it off for an app that asked, and ALWAYS off under Reduce Motion — a smooth
    /// scroll is movement, and that setting means what it says.
    /// </summary>
    public bool Smooth { get; set; } = true;

    /// <summary>
    /// How far a fling carries per dp/ms of release velocity. The glide's own decay does the
    /// slowing down, so this only says how much runway the flick buys.
    /// </summary>
    private const float FlingReach = 90;

    /// <summary>The stored offset, or null when the host never scrolled this view.</summary>
    public float? Get(string path) => _offsets.TryGetValue(path, out var offset) ? offset : null;

    /// <summary>
    /// Puts an offset exactly where the caller says, now. This is what a FINGER does: the content
    /// is under it and follows it, with nothing to smooth — smoothing a direct manipulation is how
    /// a surface stops feeling attached to the hand.
    /// </summary>
    public bool ScrollTo(string path, float offset, float maxOffset)
    {
        var next = Math.Clamp(offset, 0, MathF.Max(0, maxOffset));
        if (Get(path) is { } current && current == next) return false;
        _offsets[path] = next;
        _targets.Remove(path);
        return true;
    }

    /// <summary>
    /// Carries on after the finger lifts, in proportion to how fast it was moving. The glide is
    /// already an exponential decay, so a fling is nothing more than a target far enough away —
    /// the slowing down comes free and matches every other movement in the app.
    /// </summary>
    public bool Fling(string path, float velocity, float maxOffset)
    {
        if (!Smooth || MathF.Abs(velocity) < 0.05f) return false;
        var current = Get(path) ?? 0;
        var target = Math.Clamp(current + velocity * FlingReach, 0, MathF.Max(0, maxOffset));
        if (target == current) return false;
        _targets[path] = target;
        return true;
    }

    /// <summary>Adjusts an offset by <paramref name="delta"/> (positive scrolls toward the content
    /// end), clamped to [0, <paramref name="maxOffset"/>]. Returns true when the value changed.</summary>
    public bool ScrollBy(string path, float delta, float maxOffset, float fallback = 0)
    {
        var current = Get(path) ?? fallback;
        var from = Smooth && _targets.TryGetValue(path, out var pending) ? pending : current;
        var next = Math.Clamp(from + delta, 0, MathF.Max(0, maxOffset));

        if (!Smooth)
        {
            if (next == current) return false;
            _offsets[path] = next;
            return true;
        }

        if (next == from && MathF.Abs(next - current) < RestingDp) return false;
        _targets[path] = next;
        return true;
    }

    /// <summary>
    /// Moves every gliding offset toward its target for the time that passed. Returns true while
    /// anything is still moving, which is what keeps the frames coming — and stops them the moment
    /// it arrives, so a still page costs nothing.
    /// </summary>
    public bool Advance(float timeMs)
    {
        var elapsed = float.IsNaN(_lastTimeMs) ? 0 : MathF.Max(0, timeMs - _lastTimeMs);
        _lastTimeMs = timeMs;
        if (_targets.Count == 0) return false;

        // Smoothing switched off with a glide in flight — because the app said so, or because
        // Reduce Motion arrived. What was asked for still has to HAPPEN; it just happens now.
        if (!Smooth)
        {
            foreach (var (path, target) in _targets) _offsets[path] = target;
            _targets.Clear();
            return true;
        }

        if (elapsed <= 0) return false;

        // 1 - (1-k)^dt: the same fraction per millisecond however long the frame took, so the glide
        // looks the same at 120 Hz and at 30.
        var step = 1 - MathF.Pow(1 - SmoothingPerMs, elapsed);
        var moving = false;

        foreach (var path in _targets.Keys.ToArray())
        {
            var target = _targets[path];
            var current = Get(path) ?? 0;
            var distance = target - current;
            if (MathF.Abs(distance) < RestingDp)
            {
                _offsets[path] = target;
                _targets.Remove(path);
                moving = true;      // one last frame lands exactly on the target
                continue;
            }
            _offsets[path] = current + distance * step;
            moving = true;
        }

        return moving;
    }
}
