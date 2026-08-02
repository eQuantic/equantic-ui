using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Gestures v2 — the native drag offsets behind <see cref="DragDismiss"/>, keyed by stable layout
/// path (trees rebuild every frame; paths don't). Two regimes: an ACTIVE drag follows the pointer
/// raw (the input marks the frame dirty — a still finger burns no frames), and a RELEASED drag
/// glides back to rest over Motion.Base as a pure function of time (same smoothstep stand-in as the
/// value animator; deterministic frames). A dismissal drops the offset outright — the subtree is
/// leaving and the presence EXIT replays it from wherever the drag left it.
/// </summary>
public sealed class DragStore
{
    private sealed class Glide
    {
        public required float From;
        public required float To;
        public required float StartMs;
    }

    private readonly Dictionary<string, float> _active = new();
    private readonly Dictionary<string, Glide> _glides = new();

    /// <summary>True while any released drag is still gliding back — the host keeps scheduling
    /// frames until every one lands. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    public void BeginFrame() => AnyActive = false;

    /// <summary>
    /// The pointer moved: the subtree follows raw. SIGNED — a swipe-to-reveal travels negative and a
    /// pull-to-refresh positive, and clamping to zero here would have made one of them impossible.
    /// The CALLER's Min/Max clamp it; this store only remembers.
    /// </summary>
    public void Drag(string path, float offset)
    {
        _glides.Remove(path);
        _active[path] = offset;
    }

    /// <summary>
    /// The finger lifted: glide from where it was left to where it should REST. That target is the
    /// caller's answer, not zero — a row that stays open after a swipe rests at -96, and gliding it
    /// back to zero would undo the gesture the moment it succeeded.
    /// </summary>
    public void Release(string path, float timeMs, float restOffset = 0f)
    {
        if (_active.Remove(path, out var from) && Math.Abs(from - restOffset) > 0.01f)
        {
            _glides[path] = new Glide { From = from, To = restOffset, StartMs = timeMs };
        }
    }

    /// <summary>Dismissed (or the subtree left): clear everything for the path immediately.</summary>
    public void Drop(string path)
    {
        _active.Remove(path);
        _glides.Remove(path);
    }

    /// <summary>
    /// The offset to PAINT WITH this frame. <paramref name="restOffset"/> is where the subtree sits
    /// when nothing is happening — the caller's state, which is why it is asked for every frame
    /// rather than remembered here.
    /// </summary>
    public float Resolve(string path, float timeMs, float restOffset = 0f)
    {
        if (_active.TryGetValue(path, out var offset)) return offset;

        if (_glides.TryGetValue(path, out var glide))
        {
            var t = Math.Clamp((timeMs - glide.StartMs) / Motion.BaseMs, 0f, 1f);
            if (t >= 1f)
            {
                _glides.Remove(path);
                return glide.To;
            }
            AnyActive = true;
            var eased = t * t * (3f - 2f * t); // smoothstep — the standard-curve stand-in
            return glide.From + (glide.To - glide.From) * eased;
        }

        return restOffset;
    }
}
