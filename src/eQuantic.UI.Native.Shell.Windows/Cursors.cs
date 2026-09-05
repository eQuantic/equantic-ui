using eQuantic.UI.Native.Components;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The shared shapes, as Windows spells them — the system cursors, loaded once each.
/// <para>
/// Applied from <c>WM_SETCURSOR</c>, which Windows sends on every pointer move over the client
/// area precisely so an app can answer "this shape, here": the shell asks the host what sits under
/// the pointer and sets it, and answers TRUE so DefWindowProc does not put the class arrow back a
/// moment later.
/// </para>
/// </summary>
internal static class Cursors
{
    private static readonly Dictionary<CursorShape, IntPtr> Loaded = new();

    internal static void Apply(CursorShape shape)
    {
        if (!Loaded.TryGetValue(shape, out var cursor))
        {
            var id = shape switch
            {
                CursorShape.Pointer => IDC_HAND,
                CursorShape.Text => IDC_IBEAM,
                CursorShape.NotAllowed => IDC_NO,
                CursorShape.Crosshair => IDC_CROSS,
                CursorShape.ColResize => IDC_SIZEWE,
                CursorShape.RowResize => IDC_SIZENS,
                _ => IDC_ARROW,
            };
            cursor = LoadCursorW(IntPtr.Zero, (IntPtr)id);
            Loaded[shape] = cursor;
        }
        if (cursor != IntPtr.Zero) SetCursor(cursor);
    }
}
