using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a <see cref="Presence"/>'s position keeps: when its entrance began, and the last paint of its
/// subtree. When the position leaves the tree, that paint is handed to the clock that ran the entrance,
/// which replays it as the exit.
/// </summary>
internal sealed class PresenceState : IDisposable
{
    /// <summary>The clock whose entrance this is, which takes the exit.</summary>
    public PresenceClock? Clock;

    /// <summary>The frame time the entrance began at, or NaN before the first measure.</summary>
    public float EnteredAtMs = float.NaN;

    /// <summary>How the subtree enters, and so how its exit leaves.</summary>
    public PresenceMotion Kind;

    /// <summary>The subtree's draw commands, as the last frame that drew it drew them.</summary>
    public DrawCommand[]? LastPaint;

    /// <summary>The position left the tree: its last paint leaves as an exit.</summary>
    public void Dispose()
    {
        if (Clock is { } clock && LastPaint is { } paint) clock.Depart(Kind, paint);
        Clock = null;
        LastPaint = null;
    }
}
