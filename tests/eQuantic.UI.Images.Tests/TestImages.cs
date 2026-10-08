using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace eQuantic.UI.Images.Tests;

/// <summary>
/// The images the tests feed the optimizer, made with SkiaSharp where it has an encoder, and
/// written byte by byte where it has none: an EXIF orientation, an animated GIF, a PNG whose header
/// asks for more pixels than anything should decode.
/// </summary>
internal static class TestImages
{
    /// <summary>An image of one colour, encoded in the given format.</summary>
    public static byte[] Solid(int width, int height, SKColor color,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return Encoded(bitmap, format);
    }

    /// <summary>An image whose left half is one colour and right half another, encoded in the given format.</summary>
    public static byte[] Halves(int width, int height, SKColor left, SKColor right,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(right);
            using var paint = new SKPaint { Color = left };
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height), paint);
        }
        return Encoded(bitmap, format);
    }

    /// <summary>
    /// A JPEG of four quadrants, each a whole number of the encoder's 16-pixel blocks so no block
    /// mixes two colours, and encoded at quality 100.
    /// </summary>
    public static byte[] Quadrants(int width, int height, SKColor topLeft, SKColor topRight,
        SKColor bottomLeft, SKColor bottomRight)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            var (w, h) = (width / 2f, height / 2f);
            Fill(canvas, new SKRect(0, 0, w, h), topLeft);
            Fill(canvas, new SKRect(w, 0, width, h), topRight);
            Fill(canvas, new SKRect(0, h, w, height), bottomLeft);
            Fill(canvas, new SKRect(w, h, width, height), bottomRight);
        }
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100)
            ?? throw new InvalidOperationException("SkiaSharp writes no JPEG");
        return data.ToArray();
    }

    /// <summary>A PNG of vertical stripes, <paramref name="stripe"/> columns wide, alternating two colours.</summary>
    public static byte[] Stripes(int width, int height, int stripe, SKColor first, SKColor second)
    {
        using var bitmap = new SKBitmap(width, height);
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                bitmap.SetPixel(x, y, x / stripe % 2 == 0 ? first : second);
        return Encoded(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>The name of the colour nearest a pixel, among the four the quadrant images use.</summary>
    public static string NameOf(SKColor pixel)
    {
        (string Name, SKColor Color)[] named =
            [("red", SKColors.Red), ("lime", SKColors.Lime), ("blue", SKColors.Blue), ("yellow", SKColors.Yellow)];
        return named.MinBy(n => Square(n.Color.Red - pixel.Red) + Square(n.Color.Green - pixel.Green)
            + Square(n.Color.Blue - pixel.Blue)).Name;

        static int Square(int value) => value * value;
    }

    public static MemoryStream Stream(byte[] bytes) => new(bytes);

    /// <summary>The stored size, read from the header: no orientation applied.</summary>
    public static (int Width, int Height) SizeOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes))
            ?? throw new InvalidOperationException("not an image SkiaSharp reads");
        return (codec.Info.Width, codec.Info.Height);
    }

    /// <summary>The format the bytes are in.</summary>
    public static SKEncodedImageFormat FormatOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes))
            ?? throw new InvalidOperationException("not an image SkiaSharp reads");
        return codec.EncodedFormat;
    }

    /// <summary>The colour of one pixel, as stored.</summary>
    public static SKColor PixelAt(byte[] bytes, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(bytes)
            ?? throw new InvalidOperationException("not an image SkiaSharp decodes");
        return bitmap.GetPixel(x, y);
    }

    /// <summary>
    /// A JPEG with an EXIF orientation: an APP1 segment holding one IFD entry (tag 0x0112, a SHORT),
    /// inserted after the SOI marker, which is where a camera writes it.
    /// </summary>
    public static byte[] WithOrientation(byte[] jpeg, ushort orientation)
    {
        var exif = new List<byte>();
        exif.AddRange(Encoding.ASCII.GetBytes("Exif\0\0"));
        exif.AddRange([(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00]); // little-endian TIFF, IFD at 8
        exif.AddRange([0x01, 0x00]); // one entry
        exif.AddRange([0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00]); // Orientation, SHORT, count 1
        exif.AddRange([(byte)orientation, (byte)(orientation >> 8), 0x00, 0x00]);
        exif.AddRange([0x00, 0x00, 0x00, 0x00]); // no next IFD

        var length = exif.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        segment.AddRange(exif);

        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    /// <summary>
    /// A JPEG with an APP13 segment of the given size inserted after its SOI marker: a header past
    /// what one read takes, as an editor's metadata makes it. Cut inside an APP13, Skia's codec
    /// answers InvalidInput rather than IncompleteInput.
    /// </summary>
    public static byte[] WithPadding(byte[] jpeg, int size)
    {
        var length = size + 2;
        return [.. jpeg[..2], 0xFF, 0xED, (byte)(length >> 8), (byte)length, .. new byte[size], .. jpeg[2..]];
    }

    /// <summary>
    /// A 1 × 1 GIF of two frames, red then blue. SkiaSharp writes no GIF, so the bytes are written
    /// here: each frame's LZW data is the three 3-bit codes clear, the colour's index and end.
    /// </summary>
    public static byte[] AnimatedGif() =>
    [
        .. "GIF89a"u8,
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, // 1 × 1, a global table of two colours
        0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF,       // red, blue
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // 100 ms
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00,             // index 0
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00,
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00,             // index 1
        0x3B,
    ];

    /// <summary>
    /// A PNG whose header declares the given size, with no pixels behind it: what a decompression
    /// bomb looks like to anything that trusts the header.
    /// </summary>
    public static byte[] PngHeaderOnly(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        return [0x89, .. "PNG\r\n\u001a\n"u8, .. Chunk("IHDR", ihdr), .. Chunk("IDAT", []), .. Chunk("IEND", [])];
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        var chunk = new byte[8 + data.Length + 4];
        BinaryPrimitives.WriteInt32BigEndian(chunk, data.Length);
        typed.CopyTo(chunk, 4);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32(typed));
        return chunk;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static void Fill(SKCanvas canvas, SKRect rect, SKColor color)
    {
        using var paint = new SKPaint { Color = color };
        canvas.DrawRect(rect, paint);
    }

    private static byte[] Encoded(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var data = bitmap.Encode(format, 90)
            ?? throw new InvalidOperationException($"SkiaSharp writes no {format}");
        return data.ToArray();
    }
}
