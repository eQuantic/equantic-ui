using System.Buffers.Binary;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// How big an encoded image is, read from its HEADER — a few dozen bytes, without decoding
/// megabytes of pixels. A caller usually wants the dimensions to decide what to do next (a slot to
/// lay out, a size to warn about before uploading), and decoding first would cost far more than the
/// answer is worth.
/// <para>
/// Unknown formats answer (0, 0) rather than throwing: not knowing how big a picture is has never
/// been a reason to refuse to hand it over.
/// </para>
/// </summary>
public static class ImageHeader
{
    public static (int Width, int Height) Measure(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16) return (0, 0);

        // PNG: the IHDR chunk is always first, at a fixed offset.
        if (bytes[..8].SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }))
            return (BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]),
                    BinaryPrimitives.ReadInt32BigEndian(bytes[20..24]));

        // GIF: little-endian, right after the signature.
        if (bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
            return (BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..8]),
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..10]));

        if (bytes[0] == 0xFF && bytes[1] == 0xD8) return Jpeg(bytes);

        return (0, 0);
    }

    /// <summary>
    /// JPEG keeps its size in a START-OF-FRAME marker, and where that sits depends on everything
    /// before it — thumbnails, colour profiles, whatever the camera felt like adding. So the
    /// segments are walked until one of the SOF markers turns up.
    /// </summary>
    private static (int, int) Jpeg(ReadOnlySpan<byte> bytes)
    {
        var at = 2;
        while (at + 9 < bytes.Length)
        {
            if (bytes[at] != 0xFF) { at++; continue; }         // resync: padding is legal here
            var marker = bytes[at + 1];
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7) { at += 2; continue; }

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 2)..(at + 4)]);
            // C0..CF are the frame headers, except C4 (Huffman), C8 (extension) and CC (arithmetic).
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 7)..(at + 9)]),
                        BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 5)..(at + 7)]));

            if (length < 2) return (0, 0);                      // malformed; stop rather than spin
            at += 2 + length;
        }
        return (0, 0);
    }
}
