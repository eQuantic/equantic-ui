using System.Runtime.InteropServices;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// The W4 image service on Windows — the Windows Imaging Component, SYSTEM only (the zero
/// third-party rule; the twin of ImageIO on the Mac): any format the OS decodes (PNG, JPEG, BMP,
/// GIF, TIFF, HEIF where the codec is installed, …) lands as straight sRGB RGBA for the engine's
/// Rgba8 texture path. Stateless — the HOST caches decoded textures per source. v1 sources are local
/// file paths and data URIs; URLs and async loading states are the documented fence.
/// </summary>
public sealed unsafe class WicImageLoader : IImageLoader, IDisposable
{
    private readonly void* _factory;
    private bool _disposed;

    public WicImageLoader()
    {
        _factory = Wic.CreateFactory();
    }

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
        if (file is null || file.Length == 0) return null;
        return Decode(file);
    }

    /// <summary>The encoded bytes as straight RGBA, or null when WIC has no decoder for them.</summary>
    public RgbaImage? Decode(byte[] encoded)
    {
        void* stream = null;
        void* decoder = null;
        void* frame = null;
        void* converter = null;
        // WIC reads the memory lazily, so the bytes stay pinned for the whole decode.
        var handle = GCHandle.Alloc(encoded, GCHandleType.Pinned);
        try
        {
            if (Wic.CreateStream(_factory, &stream) < 0) return null;
            if (Wic.InitializeFromMemory(stream, (byte*)handle.AddrOfPinnedObject(), (uint)encoded.Length) < 0) return null;
            if (Wic.CreateDecoderFromStream(_factory, stream, null, Wic.DecodeMetadataCacheOnDemand, &decoder) < 0) return null;
            if (Wic.GetFrame(decoder, 0, &frame) < 0) return null;

            uint width, height;
            if (Wic.GetSize(frame, &width, &height) < 0 || width == 0 || height == 0) return null;

            // Straight (not premultiplied) RGBA is the engine's Rgba8 contract, and WIC converts to
            // it directly — no un-premultiply pass, unlike CoreGraphics.
            if (Wic.CreateFormatConverter(_factory, &converter) < 0) return null;
            var format = Wic.PixelFormat32bppRGBA;
            if (Wic.Initialize(converter, frame, &format, Wic.DitherTypeNone, null, 0.0, Wic.PaletteTypeCustom) < 0)
                return null;

            var stride = width * 4;
            var rgba = new byte[stride * height];
            fixed (byte* pixels = rgba)
            {
                if (Wic.CopyPixels(converter, null, stride, (uint)rgba.Length, pixels) < 0) return null;
            }
            return new RgbaImage((int)width, (int)height, rgba);
        }
        finally
        {
            Com.Release(ref converter);
            Com.Release(ref frame);
            Com.Release(ref decoder);
            Com.Release(ref stream);
            handle.Free();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Com.Release(_factory);
    }
}
