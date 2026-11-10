using System.Buffers.Binary;
using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Build;

/// <summary>
/// The <c>.ico</c> format: a directory of images a reader picks from by size. Windows reads one as
/// an executable's icon, and every browser reads one at a site's <c>/favicon.ico</c>, so the
/// executable (<see cref="WindowsIcons"/>) and the web set (<see cref="WebIcons"/>) write the same
/// container with the sizes each wants.
/// <para>
/// The layout is the one Windows has read since Vista: an ICONDIR, one ICONDIRENTRY per image, then
/// the images. The small sizes are uncompressed 32-bit DIBs with the mandatory AND mask (every old
/// reader understands them); the 256 is a PNG, which is the only form the format allows at that
/// size and the form Explorer prefers.
/// </para>
/// </summary>
public static class IconFile
{
    /// <summary>The file, its images in the order given, each derived from the one source.</summary>
    public static byte[] Encode(int sourceSize, byte[] sourceRgba, IReadOnlyList<(int Size, bool Png)> representations)
    {
        var images = new List<byte[]>();
        foreach (var (size, png) in representations)
        {
            var pixels = size == sourceSize ? sourceRgba : Downscale.Box(sourceRgba, sourceSize, size);
            images.Add(png ? PngCodec.Encode(size, size, pixels) : Dib(size, pixels));
        }

        // ICONDIR (6 bytes) + ICONDIRENTRY (16 bytes) × N, then the images in order.
        var directory = 6 + 16 * representations.Count;
        var total = directory + images.Sum(image => image.Length);
        var file = new byte[total];
        var span = file.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[0..], 0);         // reserved
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 1);         // type: icon
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)representations.Count);

        var offset = directory;
        for (var i = 0; i < representations.Count; i++)
        {
            var (size, _) = representations[i];
            var entry = span[(6 + 16 * i)..];
            entry[0] = (byte)(size == 256 ? 0 : size);                 // 0 means 256
            entry[1] = (byte)(size == 256 ? 0 : size);
            entry[2] = 0;                                              // colour count: none (true colour)
            entry[3] = 0;                                              // reserved
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1);   // planes
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);  // bits per pixel
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], images[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry[12..], offset);
            images[i].CopyTo(span[offset..]);
            offset += images[i].Length;
        }

        return file;
    }

    /// <summary>
    /// A 32-bit BGRA DIB as an icon image: a BITMAPINFOHEADER whose height is DOUBLED (the XOR
    /// colour bitmap and the AND mask stacked), bottom-up rows, then a 1-bit mask padded to 32-bit
    /// rows. The mask is all zeros — "opaque, defer to alpha" — which is what every 32-bit icon
    /// ships and what makes the alpha channel the one that counts.
    /// </summary>
    private static byte[] Dib(int size, byte[] rgba)
    {
        var maskStride = (size + 31) / 32 * 4;
        var colourBytes = size * size * 4;
        var maskBytes = maskStride * size;
        var image = new byte[40 + colourBytes + maskBytes];
        var header = image.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(header[0..], 40);              // biSize
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], size);             // biWidth
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], size * 2);         // biHeight: XOR + AND
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);              // biPlanes
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 32);             // biBitCount
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 0);              // BI_RGB
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], (uint)(colourBytes + maskBytes));

        // Bottom-up, BGRA.
        for (var y = 0; y < size; y++)
        {
            var sourceRow = (size - 1 - y) * size * 4;
            var destinationRow = 40 + y * size * 4;
            for (var x = 0; x < size; x++)
            {
                var s = sourceRow + x * 4;
                var d = destinationRow + x * 4;
                image[d] = rgba[s + 2];
                image[d + 1] = rgba[s + 1];
                image[d + 2] = rgba[s];
                image[d + 3] = rgba[s + 3];
            }
        }
        return image;
    }
}
