using Android.Util;
using Android.Views;
using eQuantic.UI.Native.Components;

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
/// <para>
/// A field that hides what is typed (a password) keeps it out of the log too: its text, and every
/// key that types something, are written as how long the text is, never as what it says. A log is
/// what a bug report carries off the device.
/// </para>
/// </summary>
internal static class PhotonInputTrace
{
    private const string Tag = "PhotonInput";

    /// <summary>Whether the trace is on now.</summary>
    internal static bool On => Log.IsLoggable(Tag, LogPriority.Debug);

    internal static void Write(string line) => Log.Debug(Tag, line);

    /// <summary>Text the keyboard sent or the field holds: quoted, or only its length when the
    /// field hides it.</summary>
    internal static string Text(string? text, bool hidden) =>
        hidden ? $"<{text?.Length ?? 0} hidden>" : $"\"{text}\"";

    /// <summary>A field's state: its text as <see cref="Text"/> writes it, its selection and its
    /// composition.</summary>
    internal static string Value(TextEditingValue value, bool hidden) =>
        $"{Text(value.Text, hidden)} selection {value.SelectionBase}..{value.SelectionExtent}"
        + $" composing {value.ComposingStart}..{value.ComposingEnd}";

    /// <summary>A key the keyboard sent: its code, unless it types something into a field that
    /// hides it, where the code would say what was typed.</summary>
    internal static string Key(KeyEvent? e, bool hidden) =>
        e is null ? "null"
        : hidden && e.IsPrintingKey ? $"{e.Action} <hidden>"
        : $"{e.Action} {e.KeyCode}";
}
