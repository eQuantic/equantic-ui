using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Apple;

[StructLayout(LayoutKind.Sequential)]
public struct CGPoint
{
    public double X, Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct CGSize
{
    public double Width, Height;

    public CGSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct CGRect
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
/// The ObjC runtime, as thin as it goes: straight <c>objc_msgSend</c> P/Invokes and ONLY the shapes
/// the shells send — zero third-party packages, on macOS and iOS alike. arm64-only: Apple Silicon
/// has a single msgSend entry point (no _stret/_fpret variants); the x64 struct-return split joins
/// with the packaging milestone if it is ever needed.
/// </summary>
public static partial class ObjC
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr sel_registerName(string name);

    // ---- Defining a class at runtime -----------------------------------------------------------
    // Some things AppKit only tells a SUBCLASS. A window shell with no classes of its own can poll
    // for most of them, but not for the ones that happen inside a loop it never gets back from —
    // a live resize being the one that matters. Three calls make a subclass with one method
    // overridden, which is all that is ever needed here.

    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr objc_allocateClassPair(IntPtr superclass, string name, nuint extraBytes);

    [LibraryImport(ObjCLib)]
    public static partial void objc_registerClassPair(IntPtr cls);

    [LibraryImport(ObjCLib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool class_addMethod(IntPtr cls, IntPtr name, IntPtr implementation, string types);

    [LibraryImport(ObjCLib)]
    public static partial IntPtr class_getInstanceMethod(IntPtr cls, IntPtr name);

    [LibraryImport(ObjCLib)]
    public static partial IntPtr method_getImplementation(IntPtr method);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector, long arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector, double arg1);

    // Two shapes only a WINDOW sends. They live here with the rest because a message shape is not
    // platform knowledge — and a P/Invoke taking a struct from another assembly would drag the
    // whole assembly's marshalling settings along with it.
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector,
        ulong mask, IntPtr untilDate, IntPtr mode, [MarshalAs(UnmanagedType.I1)] bool dequeue);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector,
        CGRect rect, ulong styleMask, ulong backing, [MarshalAs(UnmanagedType.I1)] bool defer);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, double arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, CGSize size);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool SendBool(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial ulong SendULong(IntPtr receiver, IntPtr selector);

    /// <summary>For the small integer returns — a key code is an <c>unsigned short</c>, and reading
    /// it as a wider type leaves whatever the high bits happened to hold.</summary>
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial ushort SendUShort(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial double SendDouble(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial CGPoint SendPoint(IntPtr receiver, IntPtr selector);

    // Large-struct return rides x8 on arm64 — the single msgSend entry point handles it.
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial CGRect SendRect(IntPtr receiver, IntPtr selector);

    public static IntPtr Sel(string name) => sel_registerName(name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial long SendLong(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool SendBool(IntPtr receiver, IntPtr selector, long arg1, IntPtr arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(IntPtr receiver, IntPtr selector, long arg1, IntPtr arg2, IntPtr arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr Send(IntPtr receiver, IntPtr selector, nuint arg1);

    /// <summary>The managed string behind an NSString, or null when there is none.</summary>
    public static string? FromNSString(IntPtr value) =>
        value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(value, Sel("UTF8String")));

    /// <summary>An NSArray of NSStrings — what several AppKit setters take.</summary>
    public static IntPtr StringArray(IReadOnlyList<string> values)
    {
        var array = Send(objc_getClass("NSMutableArray"), Sel("array"));
        foreach (var value in values) SendVoid(array, Sel("addObject:"), NSString(value));
        return array;
    }

    public static IntPtr NSString(string value)
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
