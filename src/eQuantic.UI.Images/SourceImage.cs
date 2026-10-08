using SkiaSharp;

namespace eQuantic.UI.Images;

/// <summary>
/// A source image as the optimizer reads it: its bytes and a codec over them, refused before a
/// pixel is decoded unless it is one of the web's formats (<see cref="ImageFormats"/>) and fits the
/// pixels a decode may spend.
/// <para>
/// SkiaSharp's encoders write no EXIF, so the orientation a camera recorded is applied to the
/// pixels here, and every size this type reports is the size as displayed. The pixels are
/// converted to sRGB, as the web expects an image without a profile to be.
/// </para>
/// </summary>
internal sealed class SourceImage : IDisposable
{
    /// <summary>
    /// The most pixels a source may hold: sharp's default (16,383 × 16,383), which the Next.js
    /// optimizer inherits. At four bytes a pixel that is a gigabyte, so a file of a few kilobytes
    /// cannot ask for more than that, and a larger one is refused from its header.
    /// </summary>
    internal const long MaxPixels = 16_383L * 16_383L;

    private readonly SKData _data;
    private readonly SKCodec _codec;

    private SourceImage(SKData data, SKCodec codec)
    {
        _data = data;
        _codec = codec;
    }

    /// <summary>The bytes as they were read.</summary>
    public byte[] Bytes => _data.ToArray();

    /// <summary>Whether the source holds more than one frame, as an animated GIF or WebP does.</summary>
    public bool IsAnimated => _codec.FrameCount > 1;

    /// <summary>The width and height as displayed, the orientation applied.</summary>
    public SKSizeI Size => Displayed(_codec.Info.Width, _codec.Info.Height, _codec.EncodedOrigin);

    /// <summary>Reads a source whole, refusing one that is not a web format or holds too many pixels.</summary>
    /// <exception cref="InvalidDataException">The source is not one of the formats read, or holds more than <see cref="MaxPixels"/> pixels.</exception>
    public static async Task<SourceImage> ReadAsync(Stream source)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        var data = SKData.CreateCopy(buffer.GetBuffer(), (ulong)buffer.Length);
        var codec = SKCodec.Create(data);
        try
        {
            if (codec is null || ImageFormats.ContentTypeOf(codec.EncodedFormat) is null)
                throw new InvalidDataException($"The source is not a {ImageFormats.Names} image.");

            var (width, height) = (codec.Info.Width, codec.Info.Height);
            if ((long)width * height > MaxPixels)
                throw new InvalidDataException(
                    $"The source holds {width} × {height} pixels, more than the {MaxPixels:N0} an image may hold.");

            return new SourceImage(data, codec);
        }
        catch
        {
            codec?.Dispose();
            data.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The size a source is displayed at, read from its header alone. The stream is read
    /// asynchronously, since a request body refuses a synchronous read (and a throw inside Skia's
    /// read callback takes the process down), in blocks that double until the codec has what it
    /// needs: a PNG's first 64 bytes, a JPEG's markers up to its first scan, its EXIF and an
    /// editor's metadata included. Nothing is decoded, so the pixel ceiling does not apply, and the
    /// stream is left open for its owner.
    /// </summary>
    /// <exception cref="InvalidDataException">The source is not one of the formats read.</exception>
    public static async Task<SKSizeI> MeasureAsync(Stream source)
    {
        var header = new MemoryStream();
        while (true)
        {
            var block = new byte[Math.Max(16 * 1024, (int)header.Length)];
            var read = await source.ReadAtLeastAsync(block, block.Length, throwOnEndOfStream: false);
            header.Write(block, 0, read);

            // Bytes that do not open with one of the five signatures are refused at once, not read
            // to their end.
            if (ImageFormats.ContentTypeOf(header.GetBuffer().AsSpan(0, (int)header.Length)) is null)
                break;

            using var data = SKData.CreateCopy(header.GetBuffer(), (ulong)header.Length);
            using var codec = SKCodec.Create(data);
            if (codec is not null)
            {
                if (ImageFormats.ContentTypeOf(codec.EncodedFormat) is null)
                    break;
                return Displayed(codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
            }

            // Short of its header, a codec answers IncompleteInput, or InvalidInput for a JPEG cut
            // inside a segment it does not keep (an APP13, a comment), so it reads on whatever it
            // answered, and only a source that ended without a header is refused.
            if (read < block.Length)
                break;
        }

        throw new InvalidDataException($"The source is not a {ImageFormats.Names} image.");
    }

    /// <summary>The first frame's pixels in sRGB, oriented as displayed.</summary>
    /// <exception cref="InvalidDataException">The pixels could not be decoded.</exception>
    public SKImage Decode()
    {
        var stored = _codec.Info;
        var info = new SKImageInfo(stored.Width, stored.Height, SKImageInfo.PlatformColorType,
            stored.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());

        using var bitmap = SKBitmap.Decode(_codec, info)
            ?? throw new InvalidDataException("The source's pixels could not be decoded.");
        // Immutable, the image shares the decoded pixels; mutable, it would copy every one of them.
        bitmap.SetImmutable();

        var origin = _codec.EncodedOrigin;
        if (origin == SKEncodedOrigin.TopLeft)
            return SKImage.FromBitmap(bitmap);

        var size = Size;
        using var surface = SKSurface.Create(info.WithSize(size.Width, size.Height));
        // A quarter turn or a mirror lands every pixel on a pixel, so nearest copies them exactly.
        using var pixels = SKImage.FromBitmap(bitmap);
        surface.Canvas.SetMatrix(Orientation(origin, stored.Width, stored.Height));
        surface.Canvas.DrawImage(pixels, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return surface.Snapshot();
    }

    /// <summary>
    /// The image at the given size, a new one the caller disposes. Each axis is halved with a
    /// linear filter while it is twice its target or more, which averages each pair of pixels, and
    /// the rest of the way is Mitchell's cubic: a cubic alone reads four pixels along an axis however
    /// far it shrinks it, and drops every other one.
    /// </summary>
    public static SKImage Scaled(SKImage image, int width, int height)
    {
        var current = image;
        SKImage? made = null;
        try
        {
            // An axis that cannot halve any more stays as it is while the other goes on, so a
            // banner keeps averaging along its length after its height has stopped.
            while (true)
            {
                var halfWidth = current.Width / 2 >= width ? current.Width / 2 : current.Width;
                var halfHeight = current.Height / 2 >= height ? current.Height / 2 : current.Height;
                if (halfWidth == current.Width && halfHeight == current.Height)
                    break;

                var halved = Drawn(current, halfWidth, halfHeight, new SKSamplingOptions(SKFilterMode.Linear));
                made?.Dispose();
                made = current = halved;
            }

            if (current.Width == width && current.Height == height)
            {
                // Mitchell is not interpolating, so a pass at 1:1 would soften every pixel: the
                // halving that landed on the size is the answer, and an image already at it is
                // copied exactly.
                if (made is null)
                    return Drawn(current, width, height, new SKSamplingOptions(SKFilterMode.Nearest));

                var result = made;
                made = null;
                return result;
            }

            return Drawn(current, width, height, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        finally
        {
            made?.Dispose();
        }
    }

    public void Dispose()
    {
        _codec.Dispose();
        _data.Dispose();
    }

    private static SKImage Drawn(SKImage image, int width, int height, SKSamplingOptions sampling)
    {
        using var surface = SKSurface.Create(
            new SKImageInfo(width, height, image.ColorType, image.AlphaType, image.ColorSpace));
        surface.Canvas.DrawImage(image, new SKRect(0, 0, width, height), sampling);
        return surface.Snapshot();
    }

    private static SKSizeI Displayed(int width, int height, SKEncodedOrigin origin) =>
        Transposes(origin) ? new SKSizeI(height, width) : new SKSizeI(width, height);

    private static bool Transposes(SKEncodedOrigin origin) => origin is SKEncodedOrigin.LeftTop
        or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>The matrix that draws stored pixels as displayed, Skia's SkEncodedOriginToMatrix.</summary>
    private static SKMatrix Orientation(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}
