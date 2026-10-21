using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// What a scroll-wheel event asks the content to move, read from the <c>NSEvent</c> itself: both
/// axes, because a trackpad swipes sideways as readily as it scrolls, and macOS has already turned
/// Shift with a wheel into a horizontal movement. A trackpad's (and a Magic Mouse's) PRECISE deltas
/// are points and apply as they come; a wheel reports LINES, which glide. Kept apart from the window
/// so the selectors, the signs and the precise flag are tested against events Quartz builds, with no
/// window to send them to.
/// </summary>
internal static class MacWheel
{
    /// <summary>The travel across and down in dp, and whether it came in steps (a wheel's lines)
    /// rather than as a trackpad's points.</summary>
    internal static (float Across, float Down, bool Stepped) Read(IntPtr scrollEvent)
    {
        var precise = SendBool(scrollEvent, Sel("hasPreciseScrollingDeltas"));
        return (Travel(scrollEvent, "scrollingDeltaX", precise), Travel(scrollEvent, "scrollingDeltaY", precise),
            !precise);
    }

    /// <summary>
    /// How far one axis asked the content to move. AppKit's delta points where the CONTENT goes, so a
    /// swipe up (content up, the view moving down it) is negative and travels forward. Passing a
    /// wheel's line count straight through moved a page three pixels per notch, which is a hundred
    /// turns of the wheel to reach the bottom of anything: one line is <see cref="Touch.WheelLine"/>.
    /// </summary>
    private static float Travel(IntPtr scrollEvent, string axis, bool precise) =>
        Touch.WheelTravel((float)-SendDouble(scrollEvent, Sel(axis)), precise);
}
