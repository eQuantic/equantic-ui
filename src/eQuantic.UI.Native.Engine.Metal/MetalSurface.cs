using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// An offscreen Metal render target (<c>RGBA8Unorm_sRGB</c>, shared storage — Apple Silicon unified
/// memory; discrete-GPU Macs would need Managed storage + a synchronize blit, out of the spike's
/// fence). The texture stores PREMULTIPLIED sRGB-encoded pixels (blending output), so readback
/// un-premultiplies through linear space to produce the straight-alpha golden interchange format —
/// the same final conversion the Reference surface applies, just after an 8-bit round trip.
/// </summary>
public sealed class MetalSurface : IRenderSurface
{
    internal IntPtr Texture { get; }

    internal MetalSurface(IntPtr texture, int width, int height)
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
                var premultiplied = new ReadOnlySpan<byte>((void*)buffer, required);
                for (var i = 0; i < required; i += 4)
                {
                    var a8 = premultiplied[i + 3];
                    if (a8 == 0)
                    {
                        destinationRgba[i] = destinationRgba[i + 1] = destinationRgba[i + 2] = destinationRgba[i + 3] = 0;
                        continue;
                    }

                    // sRGB-decode the premultiplied channels (alpha stays linear in *_sRGB formats),
                    // un-premultiply in linear space, re-encode — ReferenceSurface.ReadPixelsSrgb's math.
                    var invA = 255f / a8;
                    destinationRgba[i + 0] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultiplied[i + 0]) * invA);
                    destinationRgba[i + 1] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultiplied[i + 1]) * invA);
                    destinationRgba[i + 2] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultiplied[i + 2]) * invA);
                    destinationRgba[i + 3] = a8;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        // Spike: the texture leaks per-process (no release; see MetalBackend.Dispose note).
    }
}
