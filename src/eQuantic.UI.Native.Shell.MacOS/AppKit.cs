using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.MacOS;

[StructLayout(LayoutKind.Sequential)]
internal struct CGPoint
{
    public double X, Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGSize
{
    public double Width, Height;

    public CGSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGRect
{
    public double X, Y, Width, Height;

    public CGRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>
/// Slim typed AppKit bindings — the Metal-spike pattern (open question 2's answer): straight
/// <c>objc_msgSend</c> P/Invokes, ONLY the selectors the shell uses, zero third-party packages.
/// arm64-only for now: Apple Silicon has a single msgSend entry point (no _stret/_fpret variants);
/// the x64 struct-return split joins with the packaging milestone if it's ever needed.
/// Lifetime: window/layer/app objects live for the process (the documented spike leak fence).
/// </summary>
internal static partial class AppKit
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";

    // Loading the framework registers the ObjC classes (NSApplication, CAMetalLayer via QuartzCore
    // which AppKit links). Called once before any objc_getClass.
    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string path, int mode);

    internal static void LoadFrameworks() => dlopen(AppKitFramework, 2 /* RTLD_NOW */);

    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, long arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, double arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector,
        ulong mask, IntPtr untilDate, IntPtr mode, [MarshalAs(UnmanagedType.I1)] bool dequeue);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector,
        CGRect rect, ulong styleMask, ulong backing, [MarshalAs(UnmanagedType.I1)] bool defer);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, double arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, CGSize size);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool SendBool(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial ulong SendULong(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial double SendDouble(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial CGPoint SendPoint(IntPtr receiver, IntPtr selector);

    // Large-struct return rides x8 on arm64 — the single msgSend entry point handles it.
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial CGRect SendRect(IntPtr receiver, IntPtr selector);

    internal static IntPtr Sel(string name) => sel_registerName(name);

    internal static IntPtr NSString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(objc_getClass("NSString"), Sel("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }
}
