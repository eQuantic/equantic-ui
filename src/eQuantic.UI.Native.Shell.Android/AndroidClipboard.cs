using Android.Content;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Android's <c>ClipboardManager</c>, through the two methods a text field needs. The host's fields
/// copy, cut and paste through it when a keyboard attached to the phone presses Ctrl+C, X or V, and
/// a page's own Copy button asks for the same one as a service.
/// <para>
/// A paste takes the first item, coerced to text: a link or a URI copied elsewhere pastes as what it
/// reads as, which is what a native field does. Since Android 10 only the app in front may read the
/// clipboard, and a paste comes from the person using this one.
/// </para>
/// <para>
/// No activity means no clipboard: it reads nothing and writes nowhere rather than crashing, the
/// way every capability here reports a platform it cannot reach.
/// </para>
/// </summary>
internal sealed class AndroidClipboard : ITextClipboard
{
    private static ClipboardManager? Manager =>
        PhotonActivity.Current?.GetSystemService(Context.ClipboardService) as ClipboardManager;

    public string? Read()
    {
        if (PhotonActivity.Current is not { } activity || Manager?.PrimaryClip is not { ItemCount: > 0 } clip)
            return null;
        return clip.GetItemAt(0)?.CoerceToText(activity);
    }

    public void Write(string text)
    {
        if (Manager is { } manager) manager.PrimaryClip = ClipData.NewPlainText("", text);
    }
}
