using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Windows shell as the answer to every one of these: <c>SHOpenFolderAndSelectItems</c> for
/// showing where a file is, <c>ShellExecuteEx</c> for handing a file or a URL to whatever owns it,
/// and the association API for asking without acting.
/// <para>
/// Each call answers with what the shell answered, unchanged. False means the system declined —
/// the path vanished, no app claims the scheme — and inventing an exception for it would turn an
/// ordinary outcome into one every call site has to guard. An argument the system could never have
/// been handed (a relative path, a relative URL) is the caller's mistake and throws, as the
/// contract says.
/// </para>
/// </summary>
public sealed unsafe class WindowsWorkspace : IWorkspace
{
    private const uint SEE_MASK_NOASYNC = 0x00000100;
    private const uint SEE_MASK_FLAG_NO_UI = 0x00000400;
    private const int SW_SHOWNORMAL = 1;
    private const uint ASSOCF_IS_PROTOCOL = 0x00001000;
    private const uint ASSOCSTR_COMMAND = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct SHELLEXECUTEINFOW
    {
        public uint Size;
        public uint Mask;
        public IntPtr Hwnd;
        public char* Verb;
        public char* File;
        public char* Parameters;
        public char* Directory;
        public int Show;
        public IntPtr Instance;
        public IntPtr IdList;
        public char* Class;
        public IntPtr ClassKey;
        public uint HotKey;
        public IntPtr IconOrMonitor;
        public IntPtr Process;
    }

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW")]
    private static extern int ShellExecuteEx(SHELLEXECUTEINFOW* info);

    [DllImport("shell32.dll", EntryPoint = "ILCreateFromPathW", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPath(string path);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr idList);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, IntPtr* items, uint flags);

    [DllImport("shlwapi.dll", EntryPoint = "AssocQueryStringW", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint kind, string association, string? extra,
        char* buffer, uint* size);

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
        return Execute(path);
    }

    /// <inheritdoc />
    public bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        // A relative Uri is not something the system declined — no system could ever have been
        // asked. Answering false would send the developer looking at the shell for a mistake that
        // is in their own call.
        if (!url.IsAbsoluteUri)
            throw new ArgumentException("A URL to open must be absolute — nothing can route "
                + $"\"{url.OriginalString}\" without a scheme.", nameof(url));
        return Execute(url.AbsoluteUri);
    }

    /// <inheritdoc />
    public bool CanOpen(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
            throw new ArgumentException("A URL to check must be absolute — nothing can route "
                + $"\"{url.OriginalString}\" without a scheme.", nameof(url));
        // The QUERY form: which command opens this protocol. S_OK with a command means something
        // claims it; anything else means nothing does — and no dialog either way, which is exactly
        // what a check must not put on the person's screen.
        uint size = 0;
        var probe = AssocQueryString(ASSOCF_IS_PROTOCOL, ASSOCSTR_COMMAND, url.Scheme, "open", null, &size);
        // S_FALSE with a size means "here is how big the answer is" — a handler exists.
        return probe is Com.S_OK or 1 && size > 1;
    }

    private static bool Execute(string target)
    {
        Com.EnsureInitialized();
        fixed (char* verb = "open")
        fixed (char* file = target)
        {
            var info = new SHELLEXECUTEINFOW
            {
                Size = (uint)sizeof(SHELLEXECUTEINFOW),
                // No UI of the shell's own on failure: a false here IS the report, and the contract
                // promises the caller decides what the person sees.
                Mask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI,
                Verb = verb,
                File = file,
                Show = SW_SHOWNORMAL,
            };
            return ShellExecuteEx(&info) != 0;
        }
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
