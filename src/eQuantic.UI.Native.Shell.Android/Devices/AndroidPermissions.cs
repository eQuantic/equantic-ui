using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Three answers from Android's two. The platform says "granted" or "not", and a contract needs to
/// tell "asking shows the prompt" from "asking does nothing": <see cref="PermissionState.Denied"/>
/// sends a person to the system's settings, and saying it while the prompt would still appear is as
/// wrong as a button that asks and gets no prompt.
/// <para>
/// REFUSED FOR GOOD is two facts together. The last answer the prompt gave was a refusal, which the
/// shell remembers; and Android will not show the prompt again, which it says by no longer asking
/// for a rationale. Either alone means something else: a first refusal still leaves one more prompt,
/// and "no rationale" is also what a permission never asked for looks like.
/// </para>
/// <para>
/// Only an ANSWER is remembered. A prompt that went away unanswered refused nothing, and a grant
/// clears the record, so a grant "only this time" that has since expired reads as askable, which it
/// is. The record lives outside the app's backup: restored onto a new phone, it would call a
/// permission nobody was asked for refused.
/// </para>
/// </summary>
internal static class AndroidPermissions
{
    private const string Directory = "eq.permissions";

    internal static PermissionState State(PhotonActivity activity, string permission)
    {
        if (activity.CheckSelfPermission(permission) == global::Android.Content.PM.Permission.Granted)
            return PermissionState.Granted;
        return File.Exists(Record(activity, permission)) && !activity.ShouldShowRequestPermissionRationale(permission)
            ? PermissionState.Denied
            : PermissionState.NotDetermined;
    }

    /// <summary>Shows the prompt when Android still shows one. True when the permission is granted.</summary>
    internal static async Task<bool> RequestAsync(PhotonActivity activity, string permission,
        CancellationToken cancellationToken)
    {
        var answer = await activity.RequestPermissionsAsync([permission], cancellationToken);
        if (answer is { } granted) Remember(activity, permission, refused: !granted);
        return answer == true;
    }

    private static void Remember(PhotonActivity activity, string permission, bool refused)
    {
        var record = Record(activity, permission);
        if (!refused)
        {
            // Asked of the file, not of Delete: with nothing ever refused its directory is not
            // there, and Delete answers a missing directory with an exception.
            if (File.Exists(record)) File.Delete(record);
            return;
        }
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        File.WriteAllBytes(record, []);
    }

    /// <summary>One empty file per permission refused, where the system's backup does not look.</summary>
    private static string Record(PhotonActivity activity, string permission) =>
        Path.Combine(activity.NoBackupFilesDir!.AbsolutePath, Directory, permission + ".refused");
}
