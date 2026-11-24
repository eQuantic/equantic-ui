using System.Runtime.InteropServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// POSTS WHAT A LIVE REGION JUST SAID, the AppKit way. The host decides WHETHER to announce and
/// WHAT — a frame-to-frame diff with its own debounce (<c>PhotonHost.TakeAnnouncements</c>) — and
/// this decides only how the platform is told.
///
/// <para>
/// An announcement is not an element, which is why it is not part of the tree
/// <see cref="PhotonAccessibility"/> builds. VoiceOver reads it wherever the user happens to be and
/// moves nothing: AppKit models that as a NOTIFICATION with a user-info payload
/// (<c>NSAccessibilityAnnouncementRequestedNotification</c>), posted against the window rather than
/// an element, and the two mechanisms never meet.
/// </para>
///
/// <para>
/// COMPILE-VERIFIED, NOT RUN, in the environment this landed from — there is no macOS host and no
/// shell test project in this repository, so what is pinned here is the urgency mapping's shape and
/// the call's arity. Said plainly rather than implied, because a bridge nobody executed is exactly
/// the kind of code that reads finished and posts nothing.
/// </para>
/// </summary>
internal static class PhotonAnnouncements
{
    // AppKit's own priority constants (NSAccessibilityPriorityLevel). Low is for things a user may
    // miss without consequence, High interrupts whatever is being read — which is what assertive
    // means, and the reason the vocabulary has only two words where AppKit has three: nothing in
    // the SDK asks to be announced and then ignored.
    private const long PriorityMedium = 50;
    private const long PriorityHigh = 90;

    /// <summary>What AppKit should be told, for an urgency the vocabulary states.</summary>
    internal static long PriorityOf(LiveRegionUrgency urgency) =>
        urgency == LiveRegionUrgency.Assertive ? PriorityHigh : PriorityMedium;

    /// <summary>
    /// Posts each announcement against <paramref name="window"/>. The window rather than the content
    /// view: an announcement belongs to the thing that has focus in the user's session, and a view
    /// Photon draws every pixel of is not something VoiceOver otherwise addresses.
    /// </summary>
    internal static void Post(IntPtr window, IReadOnlyList<LiveAnnouncement> announcements)
    {
        if (window == IntPtr.Zero || announcements.Count == 0) return;
        var notification = AppKitNotifications.Symbol("NSAccessibilityAnnouncementRequestedNotification");
        var announcementKey = AppKitNotifications.Symbol("NSAccessibilityAnnouncementKey");
        var priorityKey = AppKitNotifications.Symbol("NSAccessibilityPriorityKey");
        // Nothing rather than something wrong. A notification posted with a key AppKit does not
        // read SUCCEEDS and says nothing, which is indistinguishable from working — so if the
        // framework did not give up its own names, this bridge does not guess at them.
        if (notification == IntPtr.Zero || announcementKey == IntPtr.Zero || priorityKey == IntPtr.Zero)
            return;
        for (var i = 0; i < announcements.Count; i++)
        {
            var announcement = announcements[i];
            if (announcement.Text.Length == 0) continue;
            // The NAME is context, not the announcement — "Upload status" beside "Upload complete".
            // Joined here rather than in the host because this is where a platform's convention
            // lives; UIKit and TalkBack put the two in different places.
            var spoken = announcement.Label.Length == 0
                ? announcement.Text
                : $"{announcement.Label}: {announcement.Text}";

            var info = Send(objc_getClass("NSMutableDictionary"), Sel("dictionary"));
            SendVoid(info, Sel("setObject:forKey:"), NSString(spoken), announcementKey);
            SendVoid(info, Sel("setObject:forKey:"),
                Send(objc_getClass("NSNumber"), Sel("numberWithLong:"), PriorityOf(announcement.Urgency)),
                priorityKey);
            AppKitNotifications.PostNotificationWithUserInfo(window, notification, info);
        }
    }
}
