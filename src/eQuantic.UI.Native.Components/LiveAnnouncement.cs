using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// SOMETHING A READER SHOULD SAY NOW, and where it came from. Produced when a live region's text
/// differs from the text it had last frame, and drained by the platform bridge that knows how to
/// post it — <c>NSAccessibilityPostNotification</c> on macOS, an announcement notification on
/// UIKit, and on Android nothing at all, because <c>accessibilityLiveRegion</c> on the container is
/// the platform posting it for you.
/// <para>
/// <see cref="Label"/> is the region's NAME and <see cref="Text"/> is what changed; they stay apart
/// because the platforms put them in different places, and joining them here would make every
/// bridge undo it. A region with no name has an empty label, which is the Banner and Toast case —
/// their text already reads as a sentence.
/// </para>
/// </summary>
/// <param name="Path">The region that changed — the same identity a press, a focus and a scroll use.</param>
/// <param name="Label">What the region is FOR, or empty when the content says it.</param>
/// <param name="Text">The announcement: the region's subtree, as a reader would read it.</param>
/// <param name="Urgency">How hard it interrupts. Not a rate limit — see the announcer's debounce.</param>
public readonly record struct LiveAnnouncement(
    string Path,
    string Label,
    string Text,
    LiveRegionUrgency Urgency);
