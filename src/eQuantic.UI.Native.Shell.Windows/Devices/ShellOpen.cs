using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// <c>ShellExecuteEx</c> with the verb <c>open</c>: the one way the Windows shell is handed
/// something to route to whatever owns it, a file by its kind or a URL by its scheme. Shared by the
/// two capabilities that hand something over, <see cref="WindowsWorkspace"/> and
/// <see cref="WindowsUrlLauncher"/>.
/// </summary>
internal static unsafe class ShellOpen
{
    private const uint SEE_MASK_NOASYNC = 0x00000100;
    private const uint SEE_MASK_FLAG_NO_UI = 0x00000400;
    private const int SW_SHOWNORMAL = 1;

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

    /// <summary>Hands <paramref name="target"/>, a rooted path or an absolute URL, to the shell, and
    /// answers whether the shell took it.</summary>
    public static bool Execute(string target)
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
}
