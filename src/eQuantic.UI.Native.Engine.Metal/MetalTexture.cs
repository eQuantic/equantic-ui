using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>Anything the encoder can bind as a fragment texture. Render targets implement it too —
/// the RHI says a render target IS a texture, and compositing a layer depends on that being literal
/// rather than aspirational.</summary>
internal interface IMetalBindable
{
    IntPtr Texture { get; }
}

/// <summary>An uploaded sampled texture (A8 → <c>R8Unorm</c>, Rgba8 → <c>RGBA8Unorm_sRGB</c>),
/// shared storage. Lifetime is per-process (the documented ObjC fence).</summary>
public sealed class MetalTexture : IRhiTexture, IMetalBindable
{
    IntPtr IMetalBindable.Texture => Texture;
    internal IntPtr Texture { get; }

    internal MetalTexture(IntPtr texture, int width, int height)
    {
        Texture = texture;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public void Dispose()
    {
        // Per-process leak fence — see MetalDevice.Dispose.
    }
}

/// <summary>
/// An offscreen Metal render target (<c>RGBA8Unorm_sRGB</c>, shared storage — Apple Silicon
/// unified memory; discrete-GPU Macs would need Managed storage + a synchronize blit, outside the
/// current fence). The texture stores PREMULTIPLIED sRGB-encoded pixels (blending output), so
/// readback funnels through <see cref="RhiReadback.UnpremultiplySrgb"/> to produce the
/// straight-alpha golden interchange format.
/// </summary>
public sealed class MetalRenderTarget : IRhiRenderTarget, IMetalBindable
{
    IntPtr IMetalBindable.Texture => Texture;
    internal IntPtr Texture { get; }

    internal MetalRenderTarget(IntPtr texture, int width, int height)
    {
        Texture = texture;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public void ReadPixelsSrgb(Span<byte> destinationRgba)
    {
        var required = Width * Height * 4;
        if (destinationRgba.Length < required)
            throw new ArgumentException($"Destination needs {required} bytes, got {destinationRgba.Length}.", nameof(destinationRgba));

        var bytesPerRow = (ulong)(Width * 4);
        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            ObjC.SendVoid(Texture, ObjC.sel_registerName("getBytes:bytesPerRow:fromRegion:mipmapLevel:"),
                buffer, bytesPerRow,
                new MTLRegion { X = 0, Y = 0, Z = 0, Width = (ulong)Width, Height = (ulong)Height, Depth = 1 },
                0);

            unsafe
            {
                RhiReadback.UnpremultiplySrgb(new ReadOnlySpan<byte>((void*)buffer, required), destinationRgba);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        // Per-process leak fence — see MetalDevice.Dispose.
    }
}
