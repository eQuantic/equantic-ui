using System.Buffers.Binary;
using System.IO.Compression;

namespace eQuantic.UI.Native.Engine.Tests.Golden;

/// <summary>
/// A minimal, dependency-free PNG codec for the golden-image harness (8-bit RGBA only). PNG is chosen
/// so goldens are reviewable in editors/GitHub. The encoder writes filter-0 scanlines compressed with
/// <see cref="ZLibStream"/>; the decoder handles any of the five standard scanline filters so goldens
/// re-saved by external tools still load. Zero NuGet dependencies — the repo's no-external-runtime
/// philosophy applied to test infra.
/// </summary>
public static class PngCodec
{
    private static ReadOnlySpan<byte> Signature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width * height * 4} bytes of RGBA, got {rgba.Length}.");

        using var output = new MemoryStream();
        output.Write(Signature);

        // IHDR: width, height, bit depth 8, color type 6 (truecolor + alpha), deflate, filter 0, no interlace.
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        WriteChunk(output, "IHDR", ihdr);

        // IDAT: zlib-wrapped scanlines, each prefixed with filter byte 0 (None).
        using (var idat = new MemoryStream())
        {
            using (var zlib = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
            {
                var stride = width * 4;
                for (var y = 0; y < height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(rgba.Slice(y * stride, stride));
                }
            }
            WriteChunk(output, "IDAT", idat.ToArray());
        }

        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    public static (int Width, int Height, byte[] Rgba) Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG file.");

        var width = 0;
        var height = 0;
        using var idat = new MemoryStream();

        var offset = 8;
        while (offset + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            var type = System.Text.Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            var data = png.Slice(offset + 8, length);

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    if (data[8] != 8 || data[9] != 6 || data[12] != 0)
                        throw new NotSupportedException("Golden PNGs must be 8-bit RGBA, non-interlaced.");
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    return (width, height, Unfilter(Inflate(idat.ToArray()), width, height));
            }

            offset += 8 + length + 4; // length + type + data + crc
        }
        throw new InvalidDataException("PNG has no IEND chunk.");
    }

    private static byte[] Inflate(byte[] zlibData)
    {
        using var input = new MemoryStream(zlibData);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Reverses PNG scanline filtering (types 0–4: None, Sub, Up, Average, Paeth); 4 bytes/pixel.</summary>
    private static byte[] Unfilter(byte[] raw, int width, int height)
    {
        const int bpp = 4;
        var stride = width * bpp;
        if (raw.Length != (stride + 1) * height)
            throw new InvalidDataException("Unexpected PNG scanline data length.");

        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var src = y * (stride + 1) + 1;
            var dst = y * stride;
            for (var x = 0; x < stride; x++)
            {
                int value = raw[src + x];
                var left = x >= bpp ? pixels[dst + x - bpp] : 0;
                var up = y > 0 ? pixels[dst + x - stride] : 0;
                var upLeft = (y > 0 && x >= bpp) ? pixels[dst + x - stride - bpp] : 0;
                value += filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter}."),
                };
                pixels[dst + x] = (byte)value;
            }
        }
        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        System.Text.Encoding.ASCII.GetBytes(type, header[4..]);
        output.Write(header);
        output.Write(data);

        var crc = Crc32.Compute(header[4..], data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    /// <summary>CRC-32 (PNG polynomial 0xEDB88320), table-driven.</summary>
    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        public static uint Compute(ReadOnlySpan<byte> part1, ReadOnlySpan<byte> part2)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in part1) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (var b in part2) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
