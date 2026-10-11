using Android.Util;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// What the keyboard and the shell say to each other, written to the log when asked for: every call
/// the keyboard makes on the input connection, the state each one leaves, and what the shell tells
/// the keyboard back. Keyboards differ (how one batches, composes, autocorrects or deletes), and this
/// is how a keyboard's behaviour on a real device is read.
/// <para>
/// Off unless Android's own switch for a log tag turns it on:
/// <c>adb shell setprop log.tag.PhotonInput DEBUG</c>, read again when the next edit begins, and
/// <c>adb logcat -s PhotonInput</c> to read it. Nothing is formatted while it is off.
/// </para>
/// </summary>
internal static class PhotonInputTrace
{
    private const string Tag = "PhotonInput";

    /// <summary>Whether the trace is on now.</summary>
    internal static bool On => Log.IsLoggable(Tag, LogPriority.Debug);

    internal static void Write(string line) => Log.Debug(Tag, line);
}
