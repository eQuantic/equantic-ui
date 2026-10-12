using eQuantic.UI.Primitives;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The general <c>UIPasteboard</c>, through the two methods a text field needs. The host's fields
/// copy, cut and paste through it when a keyboard attached to the device presses ⌘C, X or V, and a
/// page's own Copy button asks for the same one as a service.
/// <para>
/// A read of text another app put there is the one iOS asks the person about ("Allow Paste"), as it
/// does for every app that reads the pasteboard; text this app copied reads without asking.
/// </para>
/// </summary>
internal sealed class IosClipboard : ITextClipboard
{
    public string? Read() => UIPasteboard.General.String;

    public void Write(string text) => UIPasteboard.General.String = text;
}
