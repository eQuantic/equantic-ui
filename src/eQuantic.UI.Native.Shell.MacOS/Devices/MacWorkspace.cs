using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// <c>NSWorkspace</c>, which is the Mac's one answer to both of these and has been since before any
/// of this was a framework.
/// <para>
/// Each call answers with what NSWorkspace answered, unchanged. A false here means the system
/// declined (the path vanished, nothing claims that kind of file), and inventing an exception for
/// it would turn an ordinary outcome into one every call site has to guard. A link is
/// <see cref="MacUrlLauncher"/>'s.
/// </para>
/// </summary>
public sealed class MacWorkspace : IWorkspace
{
    /// <inheritdoc />
    public bool Reveal(string path)
    {
        Rooted(path);
        // A file OR a folder: a folder is the common case here, and File.Exists alone says no to one.
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        // `selectFile:inFileViewerRootedAtPath:` with an EMPTY root is the documented way to say
        // "wherever it lives" — a root of the file's own directory opens a second window rooted
        // there instead, which is not what "show me where this is" means.
        return SendBool(SharedWorkspace.Get(), Sel("selectFile:inFileViewerRootedAtPath:"),
            NSString(path), NSString(""));
    }

    /// <inheritdoc />
    public bool OpenFile(string path)
    {
        Rooted(path);
        // A FILE, as the contract says. `openURL:` on a folder would open it in Finder, which is
        // Reveal's job and not this one's — and a caller who meant Reveal would never find out.
        if (!File.Exists(path)) return false;
        var url = Send(AppKit.Class("NSURL"), Sel("fileURLWithPath:"), NSString(path));
        return url != IntPtr.Zero && SendBool(SharedWorkspace.Get(), Sel("openURL:"), url);
    }

    /// <summary>
    /// A relative path is refused, not resolved. Resolving it — here or in NSWorkspace — means
    /// against the PROCESS's working directory, which for an app launched from Finder is <c>/</c>
    /// and for the same app launched from a terminal is wherever the terminal was: the same click
    /// works while developing and fails once installed. The first version of this called
    /// <see cref="Path.GetFullPath(string)"/> and claimed to avoid that; it does the same thing.
    /// The paths an app holds come from a scan or a picker and are absolute, so a relative one is
    /// a mistake in the call, and it is named as one.
    /// </summary>
    private static void Rooted(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathRooted(path))
            throw new ArgumentException($"\"{path}\" is relative. A path handed to the system must "
                + "be absolute — resolved against a working directory it would point somewhere "
                + "different in Finder than in a terminal.", nameof(path));
    }
}
