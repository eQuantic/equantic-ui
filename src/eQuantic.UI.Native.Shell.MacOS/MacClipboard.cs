using eQuantic.UI.Native.Framework;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// NSPasteboard, through the two methods a text field needs.
/// <para>
/// `clearContents` before every write is the pasteboard's own contract — a write without it
/// APPENDS to whatever the previous owner declared, and readers see the stale flavour first.
/// </para>
/// </summary>
internal sealed class MacClipboard : ITextClipboard
{
    private static IntPtr General() =>
        Send(objc_getClass("NSPasteboard"), Sel("generalPasteboard"));

    public string? Read() =>
        FromNSString(Send(General(), Sel("stringForType:"), NSString("public.utf8-plain-text")));

    public void Write(string text)
    {
        var pasteboard = General();
        SendVoid(pasteboard, Sel("clearContents"));
        Send(pasteboard, Sel("setString:forType:"), NSString(text), NSString("public.utf8-plain-text"));
    }
}
