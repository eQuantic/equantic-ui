using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using Foundation;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// POSTS WHAT A LIVE REGION JUST SAID, the UIKit way (#1081; the macOS twin is that shell's
/// <c>PhotonAnnouncements</c>). The host decides whether to announce and what, a frame-to-frame diff with
/// its own quiet window (<see cref="PhotonHost.TakeAnnouncements"/>), and this decides only how UIKit is
/// told: <c>UIAccessibility.Announcement</c> with the words, queued behind what VoiceOver is reading when
/// the region is polite and interrupting it when assertive, the one distinction UIKit draws.
/// <para>
/// The queue is DRAINED after every frame, VoiceOver running or not: the host keeps what it owes until a
/// shell takes it, and a phone that never drained grew it for the life of the app.
/// </para>
/// </summary>
internal static class PhotonAnnouncements
{
    internal static void Post(IReadOnlyList<LiveAnnouncement> announcements)
    {
        if (announcements.Count == 0 || !UIAccessibility.IsVoiceOverRunning) return;
        for (var i = 0; i < announcements.Count; i++)
        {
            var announcement = announcements[i];
            if (announcement.Text.Length == 0) continue;
            // The NAME is context, not the announcement, joined as the macOS shell joins it.
            var spoken = announcement.Label.Length == 0
                ? announcement.Text
                : $"{announcement.Label}: {announcement.Text}";
            using var attributes = new NSDictionary(UIView.SpeechAttributeQueueAnnouncement,
                NSNumber.FromBoolean(announcement.Urgency == LiveRegionUrgency.Polite));
            using var text = new NSAttributedString(spoken, attributes);
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, text);
        }
    }
}
