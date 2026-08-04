using System.Runtime.InteropServices;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The W4 image service on macOS — ImageIO + CoreGraphics, SYSTEM frameworks only (the zero
/// third-party rule): any format the OS decodes (PNG, JPEG, HEIC, …) lands as straight sRGB RGBA
/// for the engine's Rgba8 texture path. Stateless — the HOST caches decoded textures per source.
/// v1 sources are local file paths; URLs/async loading states are the documented fence.
/// </summary>
public sealed partial class CoreGraphicsImageLoader : IImageLoader
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ImageIO = "/System/Library/Frameworks/ImageIO.framework/ImageIO";

    [LibraryImport(CoreFoundation)]
    private static partial IntPtr CFDataCreate(IntPtr allocator, IntPtr bytes, long length);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(IntPtr cf);

    [LibraryImport(ImageIO)]
    private static partial IntPtr CGImageSourceCreateWithData(IntPtr data, IntPtr options);

    [LibraryImport(ImageIO)]
    private static partial IntPtr CGImageSourceCreateImageAtIndex(IntPtr source, long index, IntPtr options);

    [LibraryImport(CoreGraphics)]
    private static partial nuint CGImageGetWidth(IntPtr image);

    [LibraryImport(CoreGraphics)]
    private static partial nuint CGImageGetHeight(IntPtr image);

    [LibraryImport(CoreGraphics)]
    private static partial void CGImageRelease(IntPtr image);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGColorSpaceCreateDeviceRGB();

    [LibraryImport(CoreGraphics)]
    private static partial void CGColorSpaceRelease(IntPtr space);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGBitmapContextCreate(IntPtr data, nuint width, nuint height,
        nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGBitmapContextGetData(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextRelease(IntPtr context);

    private static byte[]? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public RgbaImage? Load(string source)
    {
        var file = DataUri.TryDecode(source, out var inline) ? inline : ReadFile(source);
        if (file is null) return null;

        IntPtr cfData = IntPtr.Zero, imageSource = IntPtr.Zero, image = IntPtr.Zero;
        IntPtr colorSpace = IntPtr.Zero, context = IntPtr.Zero;
        var handle = GCHandle.Alloc(file, GCHandleType.Pinned);
        try
        {
            cfData = CFDataCreate(IntPtr.Zero, handle.AddrOfPinnedObject(), file.LongLength);
            imageSource = CGImageSourceCreateWithData(cfData, IntPtr.Zero);
            if (imageSource == IntPtr.Zero) return null;
            image = CGImageSourceCreateImageAtIndex(imageSource, 0, IntPtr.Zero);
            if (image == IntPtr.Zero) return null;

            var width = (int)CGImageGetWidth(image);
            var height = (int)CGImageGetHeight(image);
            if (width <= 0 || height <= 0) return null;

            // RGBA8888 premultiplied-last: R,G,B,A byte order in memory, row 0 = the visual top.
            colorSpace = CGColorSpaceCreateDeviceRGB();
            context = CGBitmapContextCreate(IntPtr.Zero, (nuint)width, (nuint)height,
                8, (nuint)(width * 4), colorSpace, 1 /* kCGImageAlphaPremultipliedLast */);
            if (context == IntPtr.Zero) return null;

            CGContextDrawImage(context, new CGRect(0, 0, width, height), image);
            var data = CGBitmapContextGetData(context);
            if (data == IntPtr.Zero) return null;

            var rgba = new byte[width * height * 4];
            Marshal.Copy(data, rgba, 0, rgba.Length);

            // CG hands back PREMULTIPLIED; the engine's Rgba8 contract is STRAIGHT sRGB.
            for (var i = 0; i < rgba.Length; i += 4)
            {
                var a = rgba[i + 3];
                if (a is 0 or 255) continue;
                rgba[i] = (byte)Math.Min(255, rgba[i] * 255 / a);
                rgba[i + 1] = (byte)Math.Min(255, rgba[i + 1] * 255 / a);
                rgba[i + 2] = (byte)Math.Min(255, rgba[i + 2] * 255 / a);
            }
            return new RgbaImage(width, height, rgba);
        }
        finally
        {
            if (context != IntPtr.Zero) CGContextRelease(context);
            if (colorSpace != IntPtr.Zero) CGColorSpaceRelease(colorSpace);
            if (image != IntPtr.Zero) CGImageRelease(image);
            if (imageSource != IntPtr.Zero) CFRelease(imageSource);
            if (cfData != IntPtr.Zero) CFRelease(cfData);
            handle.Free();
        }
    }
}
