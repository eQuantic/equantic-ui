using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What an <see cref="InView"/>'s position keeps: whether it was on screen the last frame it was drawn.
/// <para>
/// The node is rebuilt every pass, so it cannot remember anything itself — and the contract is that
/// the callback fires on the TRANSITIONS. Without somewhere to keep the last answer, a table of
/// contents would be told "this heading is visible" sixty times a second, which is a callback every
/// caller would have to debounce. Kept on the position (<see cref="NodeStates"/>), so an
/// <see cref="InView"/> drawn at the place of another reports its own first sight.
/// </para>
/// </summary>
internal sealed class InViewState
{
    /// <summary>Whether it was on screen the last frame it was drawn.</summary>
    public bool Visible;

    /// <summary>
    /// Records what the frame saw at <paramref name="at"/> and answers whether it CHANGED — the only
    /// moment worth telling anyone about. A position seen for the first time counts as a change when
    /// it is visible, so a component learns about what is already on screen when it mounts.
    /// </summary>
    public static bool Changed(Element? at, bool visible)
    {
        if (NodeStates.Find<InViewState>(at) is { } seen)
        {
            if (seen.Visible == visible) return false;
            seen.Visible = visible;
            return true;
        }

        // No node's position, nothing to remember an answer by, and so nothing to report.
        if (NodeStates.For<InViewState>(at) is not { } first) return false;

        // First sight: NOT visible is the assumed state, so only the other answer is news. A table
        // of contents watching thirty headings would otherwise hear "not visible" thirty times the
        // moment it mounts, which tells it nothing it did not already assume.
        first.Visible = visible;
        return visible;
    }
}
