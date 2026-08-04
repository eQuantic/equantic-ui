using eQuantic.UI.Native.Components;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The shared shapes, as AppKit spells them.
/// <para>
/// Applied on every move rather than only on a change. Remembering the last shape looks like the
/// obvious saving and is wrong: AppKit resets the pointer to the arrow on its own — when the app
/// loses focus, when another app sets one, when a window closes — and the cache would then agree
/// that the right shape was already showing while the user looks at an arrow over a text field.
/// Setting a cursor is one message; being right is worth more.
/// </para>
/// </summary>
internal static class Cursors
{
    internal static void Apply(CursorShape shape)
    {
        var selector = shape switch
        {
            CursorShape.Pointer => "pointingHandCursor",
            CursorShape.Text => "IBeamCursor",
            CursorShape.NotAllowed => "operationNotAllowedCursor",
            _ => "arrowCursor",
        };

        var cursor = Send(objc_getClass("NSCursor"), Sel(selector));
        if (cursor != IntPtr.Zero) SendVoid(cursor, Sel("set"));
    }
}
