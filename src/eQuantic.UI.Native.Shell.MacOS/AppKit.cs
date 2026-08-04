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

    /// <summary>
    /// Every point in a loop's cycle where drawing is allowed: before it dispatches sources, before
    /// it waits, and right after it wakes. A live resize's loop is BUSY — it never idles while the
    /// pointer is moving — so hooking only "about to wait" is hooking the one moment that never
    /// comes.
    /// </summary>
    internal const ulong ActivityDrawable = (1UL << 2) | (1UL << 5) | (1UL << 6);

    /// <summary>Every mode, including the tracking one AppKit runs a live resize in.</summary>
    internal const string CommonModes = "kCFRunLoopCommonModes";

    internal delegate void ObserverCallback(IntPtr observer, ulong activity, IntPtr info);

    internal delegate void TimerCallback(IntPtr timer, IntPtr info);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRunLoopTimerCreate")]
    internal static partial IntPtr CFRunLoopTimerCreate(IntPtr allocator, double fireDate,
        double interval, ulong flags, long order, IntPtr callback, IntPtr context);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRunLoopAddTimer")]
    internal static partial void CFRunLoopAddTimer(IntPtr runLoop, IntPtr timer, IntPtr mode);

    [LibraryImport(CoreFoundation, EntryPoint = "CFAbsoluteTimeGetCurrent")]
    internal static partial double CFAbsoluteTimeGetCurrent();

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
