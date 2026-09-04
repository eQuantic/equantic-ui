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
        Rooted(path);
        // A file OR a folder: a folder is the common case here, and File.Exists alone says no to one.
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        // `selectFile:inFileViewerRootedAtPath:` with an EMPTY root is the documented way to say
        // "wherever it lives" — a root of the file's own directory opens a second window rooted
        // there instead, which is not what "show me where this is" means.
        return SendBool(Workspace(), Sel("selectFile:inFileViewerRootedAtPath:"),
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
        return url != IntPtr.Zero && SendBool(Workspace(), Sel("openURL:"), url);
    }

    /// <inheritdoc />
    public bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        // A relative Uri is not something the system declined — no system could ever have been
        // asked. Answering false would send the developer looking at LaunchServices for a mistake
        // that is in their own call, which is the exact shape of lie this class exists to avoid.
        if (!url.IsAbsoluteUri)
            throw new ArgumentException("A URL to open must be absolute — nothing can route "
                + $"\"{url.OriginalString}\" without a scheme.", nameof(url));
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
    /// <inheritdoc />
    public bool CanOpen(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri) return false;
        // `URLForApplicationToOpenURL:` is the QUERY form of openURL: — nil when nothing claims the
        // scheme, and no dialog either way. The action form puts up "There is no application set to
        // open the URL…" on the person's screen, which is exactly what a check must not do.
        var native = Send(AppKit.Class("NSURL"), Sel("URLWithString:"), NSString(url.AbsoluteUri));
        return native != IntPtr.Zero
            && Send(Workspace(), Sel("URLForApplicationToOpenURL:"), native) != IntPtr.Zero;
    }

    private static IntPtr Workspace() => Send(AppKit.Class("NSWorkspace"), Sel("sharedWorkspace"));

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
