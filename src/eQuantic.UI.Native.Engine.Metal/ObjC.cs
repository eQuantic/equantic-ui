using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// Minimal Objective-C runtime interop for the Metal spike: <c>objc_msgSend</c> declared per exact
/// signature (the arm64 ABI needs typed trampolines, not varargs). This IS the "own slim bindings"
/// candidate of plan open-question 2 — the spike's findings decide whether it grows into the real
/// binding layer or Silk.NET-style bindings replace it.
/// </summary>
internal static partial class ObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr objc_getClass(string name);

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr sel_registerName(string name);

    // objc_msgSend — one typed extern per shape we call.
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, out IntPtr error);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, out IntPtr error);

    // texture2DDescriptorWithPixelFormat:width:height:mipmapped:
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr Send(IntPtr receiver, IntPtr selector, ulong pixelFormat, ulong width, ulong height,
        [MarshalAs(UnmanagedType.I1)] bool mipmapped);

    // setClearColor: (MTLClearColor — 4 doubles, HFA in v0–v3 on arm64)
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, MTLClearColor color);

    // setFragmentBytes:length:atIndex:
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr bytes, ulong length, ulong index);

    // drawPrimitives:vertexStart:vertexCount:
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, ulong primitiveType, ulong start, ulong count);

    // getBytes:bytesPerRow:fromRegion:mipmapLevel: (MTLRegion > 16 bytes → passed indirectly per ABI)
    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr bytes, ulong bytesPerRow,
        MTLRegion region, ulong mipmapLevel);

    [LibraryImport(MetalFramework)]
    internal static partial IntPtr MTLCreateSystemDefaultDevice();

    /// <summary>NSString from UTF-8 (autoreleased — retained by the callee where needed).</summary>
    internal static IntPtr NSString(string value)
    {
        var utf8 = Marshal.StringToHGlobalAnsi(value);
        try
        {
            return Send(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeHGlobal(utf8);
        }
    }

    internal static string? NSStringToManaged(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero) return null;
        var utf8 = Send(nsString, sel_registerName("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLClearColor
{
    public double Red, Green, Blue, Alpha;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLRegion
{
    public ulong X, Y, Z;
    public ulong Width, Height, Depth;
}
