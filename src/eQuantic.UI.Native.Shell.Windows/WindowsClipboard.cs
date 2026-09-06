using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Windows clipboard, through the two methods a text field needs — <c>CF_UNICODETEXT</c>, the
/// format every Windows program reads and writes.
/// <para>
/// The clipboard is a shared, LOCKED resource: another process may hold it open at the moment of
/// the call, and <c>OpenClipboard</c> then answers false. A read that fails answers null rather
/// than throwing (a paste that finds nothing is an ordinary outcome), and both operations retry a
/// few times a few milliseconds apart, which is what every mature Windows app does and the reason
/// none of them show the "clipboard is busy" dialog any more.
/// </para>
/// <para>
/// Ownership: memory handed to <c>SetClipboardData</c> belongs to the SYSTEM from then on and must
/// not be freed; memory returned by <c>GetClipboardData</c> stays the system's and is only read
/// while the clipboard is open.
/// </para>
/// </summary>
public sealed class WindowsClipboard : ITextClipboard
{
    public string? Read()
    {
        if (!Open()) return null;
        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
            var memory = GetClipboardData(CF_UNICODETEXT);
            if (memory == IntPtr.Zero) return null;
            var pointer = GlobalLock(memory);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(memory);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // The new content is built BEFORE the clipboard is touched: emptying it first and then
        // failing to allocate would leave the person with nothing where their previous copy was.
        var bytes = (nuint)((text.Length + 1) * sizeof(char));
        var memory = GlobalAlloc(GMEM_MOVEABLE, bytes);
        if (memory == IntPtr.Zero) return;
        var pointer = GlobalLock(memory);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(memory);
            return;
        }
        unsafe
        {
            var destination = new Span<char>((void*)pointer, text.Length + 1);
            text.AsSpan().CopyTo(destination);
            destination[text.Length] = '\0';
        }
        GlobalUnlock(memory);

        if (!Open())
        {
            GlobalFree(memory);
            return;
        }
        try
        {
            EmptyClipboard();
            // On success the system owns the memory; on failure it is still ours to free.
            if (SetClipboardData(CF_UNICODETEXT, memory) == IntPtr.Zero) GlobalFree(memory);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool Open()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(5);
        }
        return false;
    }
}
