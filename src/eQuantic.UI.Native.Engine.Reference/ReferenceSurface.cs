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

    /// <summary>
    /// Composites <paramref name="layer"/> over THIS surface at <paramref name="alpha"/> — the
    /// group-opacity EndLayer blend. Premultiplied color scales uniformly by alpha (all four
    /// channels), then a single source-over per pixel.
    /// </summary>
    internal void CompositeOver(ReferenceSurface layer, float alpha)
    {
        for (var i = 0; i < _pixels.Length; i++)
        {
            var src = layer._pixels[i];
            if (src.A <= 0) continue;
            var scaled = new LinearColor(src.R * alpha, src.G * alpha, src.B * alpha, src.A * alpha);
            _pixels[i] = scaled.Over(_pixels[i]);
        }
    }

    /// <summary>
    /// The backdrop-blur composite (W3): source-overs <paramref name="blurred"/> — the pyramid's
    /// output, in the GPU storage format (premultiplied sRGB8) — onto this surface, masked by
    /// <paramref name="region"/>'s anti-aliased coverage. Mirrors what the GPU's clipped
    /// layer_composite draw does: decode to linear, scale by coverage, one source-over per pixel.
    /// </summary>
    internal void CompositeBlurredBackdrop(BlurImage blurred, in RRect region)
    {
        var x0 = Math.Max(0, (int)MathF.Floor(region.Rect.Left - 1));
        var y0 = Math.Max(0, (int)MathF.Floor(region.Rect.Top - 1));
        var x1 = Math.Min(Width, (int)MathF.Ceiling(region.Rect.Right + 1));
        var y1 = Math.Min(Height, (int)MathF.Ceiling(region.Rect.Bottom + 1));
        var halfSize = new Size(region.Rect.Width / 2, region.Rect.Height / 2);
        var bytes = blurred.PremultipliedSrgb;

        for (var py = y0; py < y1; py++)
        {
            for (var px = x0; px < x1; px++)
            {
                var device = new Point(px + 0.5f, py + 0.5f);
                var coverage = Sdf.Coverage(Sdf.RoundedRect(device - region.Rect.Center, halfSize, region.Radii));
                if (coverage <= 0) continue;

                var i = (py * Width + px) * 4;
                var src = new LinearColor(
                    ColorSpace.SrgbToLinear(bytes[i + 0]) * coverage,
                    ColorSpace.SrgbToLinear(bytes[i + 1]) * coverage,
                    ColorSpace.SrgbToLinear(bytes[i + 2]) * coverage,
                    bytes[i + 3] / 255f * coverage);
                _pixels[py * Width + px] = src.Over(_pixels[py * Width + px]);
            }
        }
    }

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

    /// <summary>
    /// Reads back PREMULTIPLIED sRGB-encoded RGBA — the GPU render targets' own storage format,
    /// and the format <see cref="BlurImage"/> operates on. The straight-alpha
    /// <see cref="ReadPixelsSrgb"/> stays the golden interchange; this one exists so the CPU blur
    /// twin quantizes at exactly the points the GPU does.
    /// </summary>
    public void ReadPixelsPremultipliedSrgb(Span<byte> destinationRgba)
    {
        var required = Width * Height * 4;
        if (destinationRgba.Length < required)
            throw new ArgumentException($"Destination needs {required} bytes, got {destinationRgba.Length}.", nameof(destinationRgba));

        for (int i = 0, p = 0; i < _pixels.Length; i++, p += 4)
        {
            var pixel = _pixels[i];
            destinationRgba[p + 0] = ColorSpace.LinearToSrgb(pixel.R);
            destinationRgba[p + 1] = ColorSpace.LinearToSrgb(pixel.G);
            destinationRgba[p + 2] = ColorSpace.LinearToSrgb(pixel.B);
            destinationRgba[p + 3] = (byte)Math.Clamp((int)MathF.Round(pixel.A * 255f), 0, 255);
        }
    }

    public void Dispose()
    {
        // CPU buffer — nothing unmanaged; GPU surfaces will release textures here.
    }
}
