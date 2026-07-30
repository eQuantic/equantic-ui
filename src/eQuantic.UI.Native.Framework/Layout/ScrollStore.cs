namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The host-owned SCROLL STATE (scroll compositor v1): current offsets per ScrollView, keyed by the
/// node's stable layout PATH (trees rebuild every frame; paths don't). Layout resolves the effective
/// offset from here — the node's programmatic <c>Offset</c> is only the untouched default — and the
/// host mutates it from pointer/wheel input, clamped to the frame's measured max.
/// </summary>
public sealed class ScrollStore
{
    private readonly Dictionary<string, float> _offsets = new();

    /// <summary>The stored offset, or null when the host never scrolled this view.</summary>
    public float? Get(string path) => _offsets.TryGetValue(path, out var offset) ? offset : null;

    /// <summary>Adjusts an offset by <paramref name="delta"/> (positive scrolls toward the content
    /// end), clamped to [0, <paramref name="maxOffset"/>]. Returns true when the value changed.</summary>
    public bool ScrollBy(string path, float delta, float maxOffset, float fallback = 0)
    {
        var current = Get(path) ?? fallback;
        var next = Math.Clamp(current + delta, 0, MathF.Max(0, maxOffset));
        if (next == current) return false;
        _offsets[path] = next;
        return true;
    }
}
