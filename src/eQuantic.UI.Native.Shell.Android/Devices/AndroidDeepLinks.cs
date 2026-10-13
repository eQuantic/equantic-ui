using Android.Content;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The links this app is opened with, as Android hands them over: the data of the intent that
/// started the activity, and of each intent it is handed while it runs.
/// <para>
/// ONE relay for the process, as on the desktop shells and for the same reason: the link arrives in
/// <c>onCreate</c>, before the app's container or its first tree exists, and whoever asks the
/// container for <see cref="IDeepLinks"/> later must be answered by the one that was told.
/// </para>
/// </summary>
internal static class AndroidDeepLinks
{
    private static bool _told;

    internal static DeepLinkRelay Relay { get; } = new();

    /// <summary>
    /// The activity was created, and this says what that start came with. Android creates an
    /// activity for three different reasons, and hands every one of them the intent that FIRST
    /// started it:
    /// <list type="bullet">
    /// <item>a start: from a link, or from the icon. The intent says which.</item>
    /// <item>a return from the recent apps, to an activity that ended or a process the system took
    /// back. The intent is the old one, marked as coming from history: the person tapped the app,
    /// not the link again, so it is a start with nothing.</item>
    /// <item>a re-creation in a process that is still alive (the font size changed). It is not a
    /// start at all, and what the app was started with is still what it was.</item>
    /// </list>
    /// </summary>
    internal static void Created(Intent? intent, IReadOnlyList<string> schemes, bool restoring)
    {
        if (restoring && _told) return;
        _told = true;

        var fromHistory = intent is not null && (intent.Flags & ActivityFlags.LaunchedFromHistory) != 0;
        Relay.StartedWith(restoring || fromHistory ? null : LinkOf(intent, schemes));
    }

    /// <summary>
    /// The link an intent carries, or null. Only an intent that says "open this" carries one, and
    /// only on a scheme the app DECLARED: the launcher is exported, so any app can aim an intent at
    /// it by name with whatever data it likes, and the filter in the manifest is not asked then.
    /// On the Apple shells the system delivers nothing the app did not register for; here the shell
    /// holds the same line.
    /// </summary>
    internal static string? LinkOf(Intent? intent, IReadOnlyList<string> schemes) =>
        intent?.Action == Intent.ActionView
        && intent.Scheme is { } scheme
        && schemes.Contains(scheme, StringComparer.OrdinalIgnoreCase)
            ? intent.DataString
            : null;
}
