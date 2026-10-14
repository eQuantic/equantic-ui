using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Windows shell as the answer to both of these: <c>SHOpenFolderAndSelectItems</c> for showing
/// where a file is, and <c>ShellExecuteEx</c> for handing a file to whatever owns it.
/// <para>
/// Each call answers with what the shell answered, unchanged. False means the system declined (the
/// path vanished, nothing claims that kind of file), and inventing an exception for it would turn
/// an ordinary outcome into one every call site has to guard. An argument the system could never
/// have been handed (a relative path) is the caller's mistake and throws, as the contract says. A
/// link is <see cref="WindowsUrlLauncher"/>'s.
/// </para>
/// </summary>
public sealed unsafe class WindowsWorkspace : IWorkspace
{
    [DllImport("shell32.dll", EntryPoint = "ILCreateFromPathW", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPath(string path);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr idList);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, IntPtr* items, uint flags);

    /// <inheritdoc />
    public bool Reveal(string path)
    {
        Rooted(path);
        // A file OR a folder: a folder is the common case here, and File.Exists alone says no to one.
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        Com.EnsureInitialized();
        // The item's own id list, with NO children named, is the documented way to say "open the
        // folder this lives in and select it" — a folder as the FOLDER argument would open it
        // instead, which is OpenFile's job and not this one's.
        var item = ILCreateFromPath(Path.GetFullPath(path));
        if (item == IntPtr.Zero) return false;
        try
        {
            return SHOpenFolderAndSelectItems(item, 0, null, 0) >= 0;
        }
        finally
        {
            ILFree(item);
        }
    }

    /// <inheritdoc />
    public bool OpenFile(string path)
    {
        Rooted(path);
        // A FILE, as the contract says. Opening a folder would open Explorer, which is Reveal's job
        // — and a caller who meant Reveal would never find out.
        if (!File.Exists(path)) return false;
        return ShellOpen.Execute(path);
    }

    /// <summary>
    /// A relative path is refused, not resolved. Resolving it means against the PROCESS's working
    /// directory, which for an app launched from the Start menu is System32 and for the same app
    /// launched from a terminal is wherever the terminal was: the same click works while developing
    /// and fails once installed. The paths an app holds come from a scan or a picker and are
    /// absolute, so a relative one is a mistake in the call, and it is named as one.
    /// </summary>
    private static void Rooted(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathRooted(path))
            throw new ArgumentException($"\"{path}\" is relative. A path handed to the system must "
                + "be absolute — resolved against a working directory it would point somewhere "
                + "different from the Start menu than from a terminal.", nameof(path));
    }
}
