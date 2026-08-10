using Android.Content;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// <c>SharedPreferences</c> — what Android means by "the app's preferences", stored in the app's
/// own sandbox and removed with it.
/// <para>
/// Private mode, and one named file for the framework's callers rather than the activity's default:
/// the default file is keyed on the activity's package name and is also where anything else in the
/// process writes, so a named one keeps an app's own preferences from colliding with a library's.
/// </para>
/// <para>
/// <c>Apply</c>, never <c>Commit</c>: apply writes memory immediately and the disk asynchronously,
/// which is what a preference wants. Commit blocks the calling thread on I/O, and a preference save
/// on the UI thread is a dropped frame nobody attributes to the preference.
/// </para>
/// <para>
/// No activity means no context, and a store with no context reports nothing rather than crashing —
/// the same way every capability here reports a platform it cannot reach.
/// </para>
/// </summary>
internal sealed class AndroidAppStorage : IAppStorage
{
    private const string File = "equantic.ui.app-storage";

    private static ISharedPreferences? Preferences =>
        PhotonActivity.Current?.GetSharedPreferences(File, FileCreationMode.Private);

    public string? Get(string key) => Preferences?.GetString(key, null);

    public void Set(string key, string value)
    {
        var editor = Preferences?.Edit();
        if (editor is null) return;
        editor.PutString(key, value);
        editor.Apply();
    }

    public void Remove(string key)
    {
        var editor = Preferences?.Edit();
        if (editor is null) return;
        editor.Remove(key);
        editor.Apply();
    }
}
