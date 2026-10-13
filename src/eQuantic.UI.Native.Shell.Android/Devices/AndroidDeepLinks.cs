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
    internal static DeepLinkRelay Relay { get; } = new();

    /// <summary>The link an intent carries, or null. Only an intent that says "open this" carries
    /// one: the launcher's own intent has no data, and neither has a share or a search.</summary>
    internal static string? LinkOf(Intent? intent) =>
        intent?.Action == Intent.ActionView ? intent.DataString : null;
}
