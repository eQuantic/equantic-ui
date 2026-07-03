using eQuantic.UI.Primitives;
namespace eQuantic.UI.Native.Engine.Reference;

/// <summary>
/// A CPU render target: a linear-space, PREMULTIPLIED float RGBA buffer — the same intermediate a GPU
/// uses when blending into an sRGB render target. <see cref="ReadPixelsSrgb"/> un-premultiplies and
/// sRGB-encodes at the end, mirroring the hardware's output conversion.
/// </summary>
public sealed class ReferenceSurface : IRenderSurface
{
    private readonly LinearColor[] _pixels;

    public ReferenceSurface(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);
        Width = width;
        Height = height;
        _pixels = new LinearColor[width * height];
    }

    public int Width { get; }
    public int Height { get; }

    internal void Fill(LinearColor color) => Array.Fill(_pixels, color);

    internal void BlendOver(int x, int y, LinearColor src) =>
        _pixels[y * Width + x] = src.Over(_pixels[y * Width + x]);

    public void ReadPixelsSrgb(Span<byte> destinationRgba)
    {
        var required = Width * Height * 4;
        if (destinationRgba.Length < required)
            throw new ArgumentException($"Destination needs {required} bytes, got {destinationRgba.Length}.", nameof(destinationRgba));

        for (var i = 0; i < _pixels.Length; i++)
        {
            var p = _pixels[i];
            // Un-premultiply (straight alpha for the interchange format); alpha 0 → transparent black.
            var a = p.A;
            var invA = a > 0 ? 1f / a : 0f;
            var o = i * 4;
            destinationRgba[o + 0] = ColorSpace.LinearToSrgb(p.R * invA);
            destinationRgba[o + 1] = ColorSpace.LinearToSrgb(p.G * invA);
            destinationRgba[o + 2] = ColorSpace.LinearToSrgb(p.B * invA);
            destinationRgba[o + 3] = (byte)MathF.Round(Math.Clamp(a, 0f, 1f) * 255f);
        }
    }

    public void Dispose()
    {
        // CPU buffer — nothing unmanaged; GPU surfaces will release textures here.
    }
}
