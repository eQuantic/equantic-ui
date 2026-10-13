using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace eQuantic.UI.Sdk.Tasks;

/// <summary>
/// Makes <see cref="Link"/> a symbolic link to the folder <see cref="Target"/>, through the system's
/// own call. It was a shell script, <c>ln -s</c> behind a test on macOS and Linux and <c>mklink /D</c>
/// on Windows, which is not a program but a command of <c>cmd.exe</c>, so the folder's path went
/// through a shell on every system (#841).
/// </summary>
/// <remarks>
/// Something already at <see cref="Link"/> stays: a link from an earlier build, or a folder the
/// developer keeps there. A link that cannot be made, on a Windows that allows no symbolic link
/// without Developer Mode, is said at high importance and does not fail the build, as the script's
/// ignored exit code did not.
/// </remarks>
public sealed class LinkDirectory : Microsoft.Build.Utilities.Task
{
    private const int AlreadyThereOnUnix = 17;
    private const int AlreadyThereOnWindows = 183;
    private const uint DirectoryFlag = 0x1;
    private const uint UnprivilegedFlag = 0x2;

    /// <summary>The link to make.</summary>
    [Required]
    public string Link { get; set; } = "";

    /// <summary>The folder it leads to: a relative path is read from the link's own folder.</summary>
    [Required]
    public string Target { get; set; } = "";

    /// <inheritdoc />
    public override bool Execute()
    {
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var made = windows
            ? CreateSymbolicLinkW(Link, Target, DirectoryFlag | UnprivilegedFlag) != 0
            : symlink(Target, Link) == 0;
        if (made) return true;

        var error = Marshal.GetLastWin32Error();
        if (error == (windows ? AlreadyThereOnWindows : AlreadyThereOnUnix)) return true;
        Log.LogMessage(MessageImportance.High, "{0} could not be linked to {1}: {2}",
            Link, Target, new Win32Exception(error).Message);
        return true;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int symlink(string target, string linkPath);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern byte CreateSymbolicLinkW(string linkPath, string target, uint flags);
}
