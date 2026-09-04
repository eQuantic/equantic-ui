using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// <c>NSWorkspace</c>, which is the Mac's one answer to every one of these and has been since
/// before any of this was a framework.
/// <para>
/// Each call answers with what NSWorkspace answered, unchanged. A false here means the system
/// declined — the path vanished, no app claims the scheme — and inventing an exception for it would
/// turn an ordinary outcome into one every call site has to guard.
/// </para>
/// </summary>
public sealed class MacWorkspace : IWorkspace
{
    /// <inheritdoc />
    public bool Reveal(string path)
    {
        // `selectFile:inFileViewerRootedAtPath:` with an EMPTY root is the documented way to say
        // "wherever it lives" — a root of the file's own directory opens a second window rooted
        // there instead, which is not what "show me where this is" means.
        if (!Exists(path)) return false;
        return SendBool(Workspace(), Sel("selectFile:inFileViewerRootedAtPath:"),
            NSString(Full(path)), NSString(""));
    }

    /// <inheritdoc />
    public bool OpenFile(string path)
    {
        if (!Exists(path)) return false;
        var url = Send(AppKit.Class("NSURL"), Sel("fileURLWithPath:"), NSString(Full(path)));
        return url != IntPtr.Zero && SendBool(Workspace(), Sel("openURL:"), url);
    }

    /// <inheritdoc />
    public bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        // AbsoluteUri and not ToString(): the second gives back what the app typed, and a space or
        // an accent that was never percent-encoded produces a null NSURL and a silent no-op.
        var native = Send(AppKit.Class("NSURL"), Sel("URLWithString:"), NSString(url.AbsoluteUri));
        return native != IntPtr.Zero && SendBool(Workspace(), Sel("openURL:"), native);
    }

    /// <summary>
    /// The shared workspace, with AppKit LOADED first. Nothing here can assume the runner already
    /// did it: a capability is resolved from the container and the container is built before the
    /// window, and a test or a tool may hold one with no window at all. Without the dlopen,
    /// <c>AppKit.Class("NSWorkspace")</c> answers nil and every method returns false — which looks
    /// exactly like "the system declined" and is not. Found by a probe that revealed /Applications
    /// and was told no.
    /// </summary>
    private static IntPtr Workspace() => Send(AppKit.Class("NSWorkspace"), Sel("sharedWorkspace"));

    /// <summary>A file OR a directory: a folder is the common case for Reveal, and
    /// <see cref="File.Exists(string)"/> alone answers false for one.</summary>
    private static bool Exists(string path) =>
        !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));

    /// <summary>
    /// Absolute, because NSWorkspace resolves a relative path against the PROCESS's working
    /// directory — which for an app launched from Finder is <c>/</c>, and for the same app launched
    /// from a terminal is wherever the terminal was. The same click would work while developing and
    /// fail once installed.
    /// </summary>
    private static string Full(string path) => Path.GetFullPath(path);
}
