using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// All that is AppKit's alone: loading the framework, which registers NSApplication and — through
/// the QuartzCore it links — CAMetalLayer. Everything else a window sends is the ordinary ObjC
/// runtime, shared with every other Apple shell.
/// </summary>
internal static partial class AppKit
{
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string path, int mode);

    internal static void LoadFrameworks() => dlopen(AppKitFramework, 2 /* RTLD_NOW */);

    // ---- The run loop, so a frame can be drawn from INSIDE someone else's loop -----------------

    /// <summary>Before waiting — the moment a nested loop is about to go idle, which is exactly
    /// when there is time to draw.</summary>
    internal const ulong ActivityBeforeWaiting = 1UL << 5;

    /// <summary>Every mode, including the tracking one AppKit runs a live resize in.</summary>
    internal const string CommonModes = "kCFRunLoopCommonModes";

    internal delegate void ObserverCallback(IntPtr observer, ulong activity, IntPtr info);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRunLoopObserverCreate")]
    internal static partial IntPtr CFRunLoopObserverCreate(IntPtr allocator, ulong activities,
        [MarshalAs(UnmanagedType.Bool)] bool repeats, long order, IntPtr callback, IntPtr context);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRunLoopAddObserver")]
    internal static partial void CFRunLoopAddObserver(IntPtr runLoop, IntPtr observer, IntPtr mode);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRunLoopGetCurrent")]
    internal static partial IntPtr CFRunLoopGetCurrent();

    [LibraryImport(CoreFoundation, EntryPoint = "CFStringCreateWithCString", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
}
