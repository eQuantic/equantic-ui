using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// AppKit's accessibility notifications, named by the strings AppKit itself exports: what the window
/// tells an assistive app happened, an announcement (<see cref="PhotonAnnouncements"/>) or the keyboard
/// moving (<see cref="PhotonAccessibility.FocusMoved"/>).
/// </summary>
internal static partial class AppKitNotifications
{
    /// <summary>Each notification as it is posted, the element and the name: what a test reads, since AppKit
    /// keeps what it is told to itself.</summary>
    internal static event Action<IntPtr, string>? Posted;

    /// <summary>Posts AppKit's notification <paramref name="name"/> on <paramref name="element"/>, or nothing
    /// when AppKit does not export that name.</summary>
    internal static void Post(IntPtr element, string name)
    {
        Posted?.Invoke(element, name);
        var notification = Symbol(name);
        if (element == IntPtr.Zero || notification == IntPtr.Zero) return;
        PostNotification(element, notification);
    }

    /// <summary>
    /// APPKIT'S OWN STRINGS, read from the framework rather than written here. The first version
    /// spelled them as literals — `AXAnnouncementKey`, `AXAnnouncementRequested`, `AXPriority` —
    /// with a comment arguing that a literal is safer than a symbol lookup because a failed lookup
    /// would announce nothing silently. Review pointed out that the key literal was wrong, and that
    /// argument is what made the mistake possible: a WRONG literal also announces nothing silently,
    /// and unlike a failed lookup there is nothing that can notice.
    /// <para>
    /// So the values come from the exported symbols, which cannot be wrong by construction, and a
    /// lookup that fails posts NOTHING rather than a payload under a key AppKit does not read —
    /// loud in the only way available here, since a notification with the wrong key succeeds.
    /// </para>
    /// <para>
    /// An exported `NSString * const` is a POINTER-SIZED SLOT holding the object, so the symbol's
    /// address must be dereferenced once; using it directly would pass the slot as if it were the
    /// string.
    /// </para>
    /// </summary>
    internal static IntPtr Symbol(string name)
    {
        if (_appKit == IntPtr.Zero)
            _appKit = DlOpen("/System/Library/Frameworks/AppKit.framework/AppKit", RtldLazy);
        if (_appKit == IntPtr.Zero) return IntPtr.Zero;
        var slot = DlSym(_appKit, name);
        return slot == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(slot);
    }

    private const int RtldLazy = 1;
    private static IntPtr _appKit;

    [LibraryImport("/usr/lib/libSystem.dylib", EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr DlOpen(string path, int mode);

    [LibraryImport("/usr/lib/libSystem.dylib", EntryPoint = "dlsym", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr DlSym(IntPtr handle, string symbol);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit",
        EntryPoint = "NSAccessibilityPostNotification")]
    private static partial void PostNotification(IntPtr element, IntPtr notification);

    [LibraryImport("/System/Library/Frameworks/AppKit.framework/AppKit",
        EntryPoint = "NSAccessibilityPostNotificationWithUserInfo")]
    internal static partial void PostNotificationWithUserInfo(IntPtr element, IntPtr notification, IntPtr userInfo);
}
