namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Which <see cref="Primitives.InView"/> nodes are ON SCREEN, remembered across frames.
/// <para>
/// The node is rebuilt every pass, so it cannot remember anything itself — and the contract is that
/// the callback fires on the TRANSITIONS. Without somewhere to keep the last answer, a table of
/// contents would be told "this heading is visible" sixty times a second, which is a callback every
/// caller would have to debounce.
/// </para>
/// <para>
/// Keyed by layout PATH rather than by node reference, for the same reason the reconciler is: the
/// instance is new every build, the position is not.
/// </para>
/// </summary>
public sealed class InViewStore
{
    private readonly Dictionary<string, bool> _visible = new();

    /// <summary>
    /// Records what the frame saw and answers whether it CHANGED — the only moment worth telling
    /// anyone about. A path seen for the first time counts as a change when it is visible, so a
    /// component learns about what is already on screen when it mounts.
    /// </summary>
    public bool Changed(string path, bool visible)
    {
        if (_visible.TryGetValue(path, out var previous))
        {
            if (previous == visible) return false;
            _visible[path] = visible;
            return true;
        }

        // First sight: NOT visible is the assumed state, so only the other answer is news. A table
        // of contents watching thirty headings would otherwise hear "not visible" thirty times the
        // moment it mounts, which tells it nothing it did not already assume.
        _visible[path] = visible;
        return visible;
    }

    /// <summary>Forgets everything — a surface closing, or a test starting clean.</summary>
    public void Clear() => _visible.Clear();
}
