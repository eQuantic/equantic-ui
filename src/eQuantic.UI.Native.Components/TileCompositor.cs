using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Makes the ONE tile a <see cref="DrawingPattern"/> or an <see cref="ImagePattern"/> repeats (#1937),
/// at its size in device pixels, as an Rgba8 texture a <see cref="DrawCommandKind.TiledTexture"/>
/// draws. Both kinds composite in premultiplied linear light, the space the engine blends in, and store
/// straight sRGB, the format every Rgba8 texture of the engine holds.
/// </summary>
internal sealed class TileCompositor
{
    /// <summary>The compositor every painter shares: it keeps nothing between two tiles.</summary>
    public static readonly TileCompositor Shared = new();

    /// <summary>
    /// <paramref name="artwork"/> at <paramref name="width"/> by <paramref name="height"/> pixels, its
    /// viewBox stretched over the whole texture: each shape's fill and stroke rasterized by the host's
    /// <paramref name="rasterizer"/> as <c>EmitDrawing</c> rasterizes them, filled with the paint a
    /// <see cref="Drawing"/> gives it (<see cref="ArtworkPaint"/>, <paramref name="tint"/> answering
    /// <c>currentColor</c>), and laid over the ones before it src-over, so the tile is one picture and
    /// its opacity never blends two overlapping shapes twice. Null when no shape painted a pixel.
    /// </summary>
    public TextureData? Drawing(IIconRasterizer rasterizer, VectorDrawing artwork, int width, int height, Color tint)
    {
        var canvas = new LinearColor[width * height];
        var paints = new ArtworkPaint(artwork, new Rect(0, 0, width, height));
        var painted = false;
        foreach (var shape in artwork.Shapes)
        {
            if (shape.Fill.Paints)
                painted |= Layer(shape, IconGlyphStyle.Fill, shape.Fill);
            if (shape.Stroke.Paints)
                painted |= Layer(shape, IconGlyphStyle.Stroke, shape.Stroke);
        }
        return painted ? Store(canvas, width, height) : null;

        bool Layer(VectorShape shape, IconGlyphStyle style, VectorPaint paint)
        {
            // The glyph a Drawing rasterizes for the same shape, at the tile's device size as its dp at scale 1.
            var glyph = new IconGlyph("", shape.Path, style, artwork.ViewBox, shape.StrokeWidth, shape.EvenOdd);
            if (rasterizer.Rasterize(glyph, width, height, 1) is not { Width: > 0, Height: > 0 } raster) return false;
            var fill = paints.For(shape, paint, tint);
            var any = false;
            for (var y = 0; y < height; y++)
            {
                // A rasterizer answers the size it was asked for; one that rounds differently is read across
                // the tile proportionally rather than misread.
                var row = Math.Min(raster.Height - 1, y * raster.Height / height) * raster.Width;
                for (var x = 0; x < width; x++)
                {
                    var coverage = raster.Alpha[row + Math.Min(raster.Width - 1, x * raster.Width / width)] / 255f;
                    if (coverage <= 0) continue;
                    var local = new Point(x + 0.5f, y + 0.5f);
                    // The colour from the byte ramp, the alpha unquantized: the Reference backend's rule.
                    var source = ColorSpace.ToPremultipliedLinear(fill.ColorAt(local) with { A = 255 },
                        coverage * fill.AlphaFactorAt(local));
                    var i = y * width + x;
                    canvas[i] = source.Over(canvas[i]);
                    any = true;
                }
            }
            return any;
        }
    }

    /// <summary>
    /// <paramref name="decoded"/>, an Rgba8 image, resampled ONCE to <paramref name="width"/> by
    /// <paramref name="height"/> pixels in premultiplied linear light: an area average where it shrinks by
    /// more than two times on an axis, which a bilinear tap would alias, and bilinear otherwise.
    /// </summary>
    public TextureData Raster(TextureData decoded, int width, int height)
    {
        var canvas = new LinearColor[width * height];
        var scaleX = decoded.Width / (float)width;
        var scaleY = decoded.Height / (float)height;
        var average = scaleX > 2 || scaleY > 2;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                canvas[y * width + x] = average
                    ? Average(decoded, x * scaleX, (x + 1) * scaleX, y * scaleY, (y + 1) * scaleY)
                    : Bilinear(decoded, (x + 0.5f) * scaleX - 0.5f, (y + 0.5f) * scaleY - 0.5f);
        return Store(canvas, width, height);
    }

    /// <summary>The mean of the image over the footprint [x0, x1) × [y0, y1), each texel weighted by how much
    /// of it the footprint covers.</summary>
    private static LinearColor Average(TextureData image, float x0, float x1, float y0, float y1)
    {
        float r = 0, g = 0, b = 0, a = 0, total = 0;
        for (var ty = (int)MathF.Floor(y0); ty < (int)MathF.Ceiling(y1) && ty < image.Height; ty++)
        {
            var wy = MathF.Min(y1, ty + 1) - MathF.Max(y0, ty);
            if (wy <= 0) continue;
            for (var tx = (int)MathF.Floor(x0); tx < (int)MathF.Ceiling(x1) && tx < image.Width; tx++)
            {
                var w = (MathF.Min(x1, tx + 1) - MathF.Max(x0, tx)) * wy;
                if (w <= 0) continue;
                var texel = Texel(image, tx, ty);
                r += texel.R * w; g += texel.G * w; b += texel.B * w; a += texel.A * w;
                total += w;
            }
        }
        return total <= 0 ? LinearColor.Transparent : new LinearColor(r / total, g / total, b / total, a / total);
    }

    /// <summary>The image at texel position (<paramref name="x"/>, <paramref name="y"/>), the centres at whole
    /// numbers, filtered between its four neighbours and held at the edges.</summary>
    private static LinearColor Bilinear(TextureData image, float x, float y)
    {
        var x0 = MathF.Floor(x);
        var y0 = MathF.Floor(y);
        var ax = x - x0;
        var ay = y - y0;
        var left = Math.Clamp((int)x0, 0, image.Width - 1);
        var right = Math.Clamp((int)x0 + 1, 0, image.Width - 1);
        var top = Math.Clamp((int)y0, 0, image.Height - 1);
        var bottom = Math.Clamp((int)y0 + 1, 0, image.Height - 1);
        var above = Lerp(Texel(image, left, top), Texel(image, right, top), ax);
        var below = Lerp(Texel(image, left, bottom), Texel(image, right, bottom), ax);
        return Lerp(above, below, ay);
    }

    private static LinearColor Texel(TextureData image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        var pixels = image.Alpha;
        return ColorSpace.ToPremultipliedLinear(new Color(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]));
    }

    private static LinearColor Lerp(LinearColor a, LinearColor b, float t) => new(
        a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

    /// <summary>Premultiplied linear pixels as straight sRGB bytes, the Rgba8 texture's format.</summary>
    private static TextureData Store(LinearColor[] canvas, int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (var i = 0; i < canvas.Length; i++)
        {
            var pixel = canvas[i];
            if (pixel.A <= 0) continue;
            var alpha = MathF.Min(pixel.A, 1f);
            bytes[i * 4 + 0] = ColorSpace.LinearToSrgb(pixel.R / alpha);
            bytes[i * 4 + 1] = ColorSpace.LinearToSrgb(pixel.G / alpha);
            bytes[i * 4 + 2] = ColorSpace.LinearToSrgb(pixel.B / alpha);
            bytes[i * 4 + 3] = (byte)MathF.Round(alpha * 255f);
        }
        return TextureData.Rgba(width, height, bytes);
    }
}
