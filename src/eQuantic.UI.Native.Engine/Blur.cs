using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// A pyramid level's pixels in the GPU's own storage format: PREMULTIPLIED, sRGB-encoded RGBA
/// bytes. The CPU blur quantizes each level through this exactly because the GPU does — every
/// intermediate target is an sRGB8 render target — so the two implementations round at the same
/// places instead of drifting apart level by level.
/// </summary>
public sealed class BlurImage
{
    public BlurImage(int width, int height)
    {
        Width = width;
        Height = height;
        PremultipliedSrgb = new byte[width * height * 4];
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Tightly-packed premultiplied sRGB RGBA scanlines, top-down.</summary>
    public byte[] PremultipliedSrgb { get; }
}

/// <summary>
/// The NORMATIVE dual-Kawase blur (plan W3, decision D6b) — the CPU implementation the shader
/// entry points <c>blur_down</c>/<c>blur_up</c> transliterate tap for tap. Downsampling averages
/// 5 bilinear taps /8; upsampling 8 taps /12; offsets are half a DESTINATION texel. Sampling is
/// bilinear with clamp-to-edge over sRGB-DECODED texels (the GPU decodes before filtering — spec
/// behavior for sRGB formats), accumulation is float, and each level re-encodes to sRGB8.
/// </summary>
public static class Blur
{
    /// <summary>
    /// The pyramid depth for a blur radius (dp): each level roughly doubles the spread, so depth
    /// grows with log₂ of the radius, clamped to 1–4 (beyond four levels a UI blur reads as fog).
    /// This mapping is NORMATIVE — CSS equivalence is approximate by nature; what must be exact is
    /// that every rasterizer uses this same function.
    /// </summary>
    public static int Levels(float radius) =>
        Math.Clamp((int)MathF.Ceiling(MathF.Log2(MathF.Max(2f, radius) / 2f)), 1, 4);

    /// <summary>One downsample step: destination is half the source (floor, min 1).</summary>
    public static BlurImage Downsample(BlurImage source)
    {
        var destination = new BlurImage(Math.Max(1, source.Width / 2), Math.Max(1, source.Height / 2));
        var halfX = 0.5f / destination.Width;
        var halfY = 0.5f / destination.Height;

        ForEachPixel(destination, (u, v) =>
        {
            var sum = Sample(source, u, v) * 4f;
            sum += Sample(source, u - halfX, v - halfY);
            sum += Sample(source, u + halfX, v + halfY);
            sum += Sample(source, u + halfX, v - halfY);
            sum += Sample(source, u - halfX, v + halfY);
            return sum * (1f / 8f);
        });
        return destination;
    }

    /// <summary>One upsample step into an explicit destination size (the matching down level's).</summary>
    public static BlurImage Upsample(BlurImage source, int destinationWidth, int destinationHeight)
    {
        var destination = new BlurImage(destinationWidth, destinationHeight);
        var halfX = 0.5f / destinationWidth;
        var halfY = 0.5f / destinationHeight;

        ForEachPixel(destination, (u, v) =>
        {
            var sum = Sample(source, u - halfX * 2, v);
            sum += Sample(source, u - halfX, v + halfY) * 2f;
            sum += Sample(source, u, v + halfY * 2);
            sum += Sample(source, u + halfX, v + halfY) * 2f;
            sum += Sample(source, u + halfX * 2, v);
            sum += Sample(source, u + halfX, v - halfY) * 2f;
            sum += Sample(source, u, v - halfY * 2);
            sum += Sample(source, u - halfX, v - halfY) * 2f;
            return sum * (1f / 12f);
        });
        return destination;
    }

    /// <summary>The full pyramid for <paramref name="radius"/>: down N levels, back up to full size.</summary>
    public static BlurImage Apply(BlurImage source, float radius)
    {
        var levels = Levels(radius);
        var sizes = new (int W, int H)[levels];
        var current = source;
        for (var i = 0; i < levels; i++)
        {
            sizes[i] = (current.Width, current.Height);
            current = Downsample(current);
        }
        for (var i = levels - 1; i >= 0; i--)
            current = Upsample(current, sizes[i].W, sizes[i].H);
        return current;
    }

    private static void ForEachPixel(BlurImage destination, Func<float, float, LinearRgba> shade)
    {
        var pixels = destination.PremultipliedSrgb;
        var index = 0;
        for (var y = 0; y < destination.Height; y++)
        {
            var v = (y + 0.5f) / destination.Height;
            for (var x = 0; x < destination.Width; x++, index += 4)
            {
                var u = (x + 0.5f) / destination.Width;
                var color = shade(u, v);
                pixels[index + 0] = ColorSpace.LinearToSrgb(color.R);
                pixels[index + 1] = ColorSpace.LinearToSrgb(color.G);
                pixels[index + 2] = ColorSpace.LinearToSrgb(color.B);
                pixels[index + 3] = (byte)Math.Clamp((int)MathF.Round(color.A * 255f), 0, 255);
            }
        }
    }

    /// <summary>Bilinear tap with clamp-to-edge, filtering sRGB-DECODED (linear) premultiplied
    /// texels — the exact read the GPU's linear sampler performs on an sRGB view.</summary>
    private static LinearRgba Sample(BlurImage image, float u, float v)
    {
        var tx = u * image.Width - 0.5f;
        var ty = v * image.Height - 0.5f;
        var x0 = (int)MathF.Floor(tx);
        var y0 = (int)MathF.Floor(ty);
        var fx = tx - x0;
        var fy = ty - y0;

        var x1 = Math.Clamp(x0 + 1, 0, image.Width - 1);
        var y1 = Math.Clamp(y0 + 1, 0, image.Height - 1);
        x0 = Math.Clamp(x0, 0, image.Width - 1);
        y0 = Math.Clamp(y0, 0, image.Height - 1);

        var t00 = Texel(image, x0, y0);
        var t10 = Texel(image, x1, y0);
        var t01 = Texel(image, x0, y1);
        var t11 = Texel(image, x1, y1);

        var w00 = (1 - fx) * (1 - fy);
        var w10 = fx * (1 - fy);
        var w01 = (1 - fx) * fy;
        var w11 = fx * fy;
        return t00 * w00 + t10 * w10 + t01 * w01 + t11 * w11;
    }

    private static LinearRgba Texel(BlurImage image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        var pixels = image.PremultipliedSrgb;
        return new LinearRgba(
            ColorSpace.SrgbToLinear(pixels[i + 0]),
            ColorSpace.SrgbToLinear(pixels[i + 1]),
            ColorSpace.SrgbToLinear(pixels[i + 2]),
            pixels[i + 3] / 255f);
    }

    /// <summary>A float accumulator for premultiplied linear RGBA (alpha stays linear in sRGB
    /// formats, so it decodes as a plain byte fraction).</summary>
    private readonly record struct LinearRgba(float R, float G, float B, float A)
    {
        public static LinearRgba operator *(LinearRgba c, float s) => new(c.R * s, c.G * s, c.B * s, c.A * s);
        public static LinearRgba operator +(LinearRgba a, LinearRgba b) => new(a.R + b.R, a.G + b.G, a.B + b.B, a.A + b.A);
    }
}
