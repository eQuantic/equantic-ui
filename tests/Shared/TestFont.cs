using System.Buffers.Binary;
using System.Text;

namespace eQuantic.UI.Tests;

/// <summary>
/// A TrueType font built in code, for the tests that need a face no machine has installed.
/// <para>
/// No font file is committed, for its licence and its size. This writes a small, valid one instead:
/// the printable ASCII range, every glyph a box <c>advance</c> units wide on a 1000-unit em, under a
/// family name chosen by the test. Wide on purpose: text measured in it cannot be mistaken for the
/// platform's face, so a test proves which face set it by its width.
/// </para>
/// <para>
/// The tables are the ones every engine requires of a TrueType font (<c>OS/2</c>, <c>cmap</c>,
/// <c>glyf</c>, <c>head</c>, <c>hhea</c>, <c>hmtx</c>, <c>loca</c>, <c>maxp</c>, <c>name</c>,
/// <c>post</c>), with their checksums and the head's adjustment. <c>fvar</c> is added only when a
/// weight axis is asked for, and only a reader is ever handed that file: without the variation
/// tables behind it, it is not a font an engine should be given.
/// </para>
/// </summary>
internal static class TestFont
{
    private const int UnitsPerEm = 1000;
    private const int Ascender = 800;
    private const int Descender = -200;
    private const char First = ' ';
    private const char Last = '~';

    /// <summary>The platform and the encoding a font's name records are written for.</summary>
    public enum NameRecords
    {
        /// <summary>Windows, Unicode (3, 1), in UTF-16BE: what nearly every font carries.</summary>
        Windows,
        /// <summary>Windows, Symbol (3, 0), in UTF-16BE: the only records a symbol font carries.</summary>
        WindowsSymbol,
        /// <summary>Macintosh, Roman (1, 0), in Mac Roman: the only records an old Mac font carries.</summary>
        Macintosh,
    }

    /// <summary>A TrueType file in <paramref name="family"/>, at <paramref name="weight"/>, italic or not,
    /// and flagged OBLIQUE rather than ITALIC when <paramref name="oblique"/> says so, its names written
    /// as <paramref name="names"/> says.</summary>
    public static byte[] Build(string family, int weight = 400, bool italic = false, int advance = 1000,
        (int Min, int Max)? weightAxis = null, bool oblique = false, NameRecords names = NameRecords.Windows,
        IReadOnlyList<int>? statWeights = null)
    {
        var glyphCount = 1 + (Last - First + 1);
        var (glyf, loca) = Glyphs(glyphCount, advance);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["OS/2"] = Os2(weight, italic, advance, oblique),
            ["cmap"] = Cmap(),
            ["glyf"] = glyf,
            ["head"] = Head(italic, weight >= 700, advance),
            ["hhea"] = Hhea(glyphCount, advance),
            ["hmtx"] = Hmtx(glyphCount, advance),
            ["loca"] = loca,
            ["maxp"] = Maxp(glyphCount),
            ["name"] = Name(family, italic, weight >= 700, names),
            ["post"] = Post(italic),
        };
        if (weightAxis is { } axis) tables["fvar"] = Fvar(axis.Min, weight, axis.Max);
        if (statWeights is { } positions) tables["STAT"] = Stat(positions);
        return Assemble(tables);
    }

    private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var power = 1;
        var log = 0;
        while (power * 2 <= count) { power *= 2; log++; }

        var font = new Writer();
        font.U32(0x00010000);
        font.U16(count);
        font.U16(power * 16);
        font.U16(log);
        font.U16(count * 16 - power * 16);

        var offset = 12 + 16 * count;
        var placed = new List<(string Tag, byte[] Data, int Offset)>();
        foreach (var (tag, data) in tables)
        {
            placed.Add((tag, data, offset));
            offset += (data.Length + 3) & ~3;
        }
        foreach (var (tag, data, at) in placed)
        {
            font.Bytes(Encoding.ASCII.GetBytes(tag));
            font.U32(Checksum(data));
            font.U32(at);
            font.U32(data.Length);
        }
        foreach (var (_, data, _) in placed)
        {
            font.Bytes(data);
            font.Pad4();
        }

        var bytes = font.ToArray();
        // The head's adjustment makes the whole file sum to the magic number, and the head's own
        // checksum in the directory is taken with the adjustment at zero, which it was above.
        var head = placed.Single(table => table.Tag == "head").Offset;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(head + 8), unchecked(0xB1B0AFBA - Checksum(bytes)));
        return bytes;
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var j = 0; j < 4; j++) word = (word << 8) | (i + j < data.Length ? data[i + j] : 0u);
            sum = unchecked(sum + word);
        }
        return sum;
    }

    private static byte[] Head(bool italic, bool bold, int advance)
    {
        var w = new Writer();
        w.U16(1); w.U16(0);
        w.U32(0x00010000);              // fontRevision
        w.U32(0);                       // checkSumAdjustment, written last
        w.U32(0x5F0F3CF5);
        w.U16(0x0003);                  // baseline at y=0, left sidebearing at x=0
        w.U16(UnitsPerEm);
        w.I64(0); w.I64(0);             // created, modified
        w.I16(0); w.I16(0); w.I16((short)advance); w.I16(Ascender);
        w.U16((bold ? 1 : 0) | (italic ? 2 : 0));
        w.U16(8);                       // lowestRecPPEM
        w.I16(2);                       // fontDirectionHint
        w.I16(1);                       // long loca offsets
        w.I16(0);
        return w.ToArray();
    }

    private static byte[] Hhea(int glyphCount, int advance)
    {
        var w = new Writer();
        w.U16(1); w.U16(0);
        w.I16(Ascender); w.I16(Descender); w.I16(0);
        w.U16(advance);
        w.I16(0); w.I16(0); w.I16((short)advance);
        w.I16(1); w.I16(0); w.I16(0);
        w.I16(0); w.I16(0); w.I16(0); w.I16(0);
        w.I16(0);
        w.U16(glyphCount);
        return w.ToArray();
    }

    private static byte[] Maxp(int glyphCount)
    {
        var w = new Writer();
        w.U32(0x00010000);
        w.U16(glyphCount);
        w.U16(4); w.U16(1);             // maxPoints, maxContours
        w.U16(0); w.U16(0);             // composite points and contours
        w.U16(2);                       // maxZones
        for (var i = 0; i < 8; i++) w.U16(0);
        return w.ToArray();
    }

    private static byte[] Os2(int weight, bool italic, int advance, bool oblique)
    {
        var bold = weight >= 700;
        var w = new Writer();
        w.U16(4);
        w.I16((short)advance);
        w.U16(weight);
        w.U16(5);                       // usWidthClass: medium
        w.U16(0);                       // fsType: installable
        w.I16(650); w.I16(600); w.I16(0); w.I16(75);
        w.I16(650); w.I16(600); w.I16(0); w.I16(350);
        w.I16(50); w.I16(300);
        w.I16(0);
        w.Bytes(new byte[10]);          // panose
        w.U32(1); w.U32(0); w.U32(0); w.U32(0);
        w.Bytes("EQTF"u8.ToArray());
        w.U16((italic ? 1 : 0) | (bold ? 1 << 5 : 0) | (!italic && !bold && !oblique ? 1 << 6 : 0) | 1 << 7 | (oblique ? 1 << 9 : 0));
        w.U16(First); w.U16(Last);
        w.I16(Ascender); w.I16(Descender); w.I16(0);
        w.U16(Ascender); w.U16(-Descender);
        w.U32(1); w.U32(0);
        w.I16(500); w.I16(700); w.U16(0); w.U16(' '); w.U16(1);
        return w.ToArray();
    }

    private static byte[] Hmtx(int glyphCount, int advance)
    {
        var w = new Writer();
        for (var i = 0; i < glyphCount; i++)
        {
            w.U16(advance);
            w.I16(i == 1 ? (short)0 : (short)50);
        }
        return w.ToArray();
    }

    /// <summary>One segment from the space to the tilde, mapped to glyphs 1 onward, and the closing one.</summary>
    private static byte[] Cmap()
    {
        var sub = new Writer();
        sub.U16(4);
        sub.U16(32);                    // length
        sub.U16(0);                     // language
        sub.U16(4); sub.U16(4); sub.U16(1); sub.U16(0);
        sub.U16(Last); sub.U16(0xFFFF);
        sub.U16(0);
        sub.U16(First); sub.U16(0xFFFF);
        sub.U16(1 - First); sub.U16(1);
        sub.U16(0); sub.U16(0);

        var w = new Writer();
        w.U16(0);
        w.U16(2);
        w.U16(0); w.U16(3); w.U32(20);  // Unicode, BMP
        w.U16(3); w.U16(1); w.U32(20);  // Windows, Unicode BMP
        w.Bytes(sub.ToArray());
        return w.ToArray();
    }

    /// <summary>
    /// Glyph 0 and every printable character a box, the space empty. Each box is on the em's
    /// baseline and stands 700 units tall, set 50 in from either side of its advance.
    /// </summary>
    private static (byte[] Glyf, byte[] Loca) Glyphs(int glyphCount, int advance)
    {
        var glyf = new Writer();
        var loca = new Writer();
        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            loca.U32(glyf.Length);
            if (glyph == 1) continue;   // the space: no outline at all
            short left = 50, right = (short)(advance - 50), top = 700;
            glyf.I16(1);
            glyf.I16(left); glyf.I16(0); glyf.I16(right); glyf.I16(top);
            glyf.U16(3);                // the last point of the one contour
            glyf.U16(0);                // no instructions
            for (var i = 0; i < 4; i++) glyf.U8(0x01);
            glyf.I16(left); glyf.I16(0); glyf.I16((short)(right - left)); glyf.I16(0);
            glyf.I16(0); glyf.I16(top); glyf.I16(0); glyf.I16((short)-top);
            glyf.Pad4();
        }
        loca.U32(glyf.Length);
        return (glyf.ToArray(), loca.ToArray());
    }

    private static byte[] Name(string family, bool italic, bool bold, NameRecords platform)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var macRoman = System.Text.Encoding.GetEncoding(10000);
        var style = (bold, italic) switch
        {
            (true, true) => "Bold Italic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };
        var postScript = family.Replace(" ", "") + "-" + style.Replace(" ", "");
        string[] names = [family, style, postScript, family + " " + style, "Version 1.000", postScript];
        var mac = platform == NameRecords.Macintosh;
        var strings = new Writer();
        var records = new Writer();
        for (var i = 0; i < names.Length; i++)
        {
            var text = mac ? macRoman.GetBytes(names[i]) : Encoding.BigEndianUnicode.GetBytes(names[i]);
            if (mac) { records.U16(1); records.U16(0); records.U16(0); }
            else { records.U16(3); records.U16(platform == NameRecords.WindowsSymbol ? 0 : 1); records.U16(0x0409); }
            records.U16(i + 1);
            records.U16(text.Length); records.U16(strings.Length);
            strings.Bytes(text);
        }
        var w = new Writer();
        w.U16(0);
        w.U16(names.Length);
        w.U16(6 + 12 * names.Length);
        w.Bytes(records.ToArray());
        w.Bytes(strings.ToArray());
        return w.ToArray();
    }

    private static byte[] Post(bool italic)
    {
        var w = new Writer();
        w.U32(0x00030000);
        w.U32(italic ? unchecked((uint)(-12 << 16)) : 0);
        w.I16(-100); w.I16(50);
        w.U32(0);
        w.U32(0); w.U32(0); w.U32(0); w.U32(0);
        return w.ToArray();
    }

    private static byte[] Fvar(int min, int value, int max)
    {
        var w = new Writer();
        w.U16(1); w.U16(0);
        w.U16(16); w.U16(2);
        w.U16(1); w.U16(20);
        w.U16(0); w.U16(8);
        w.Bytes("wght"u8.ToArray());
        w.U32((uint)(min << 16)); w.U32((uint)(value << 16)); w.U32((uint)(max << 16));
        w.U16(0); w.U16(256);
        return w.ToArray();
    }

    /// <summary>
    /// A STAT table (version 1.1) with one design axis, <c>wght</c>, and one position on it for each of
    /// <paramref name="weights"/> (axis value format 1): one is a static face's own weight, as Google's
    /// instances carry it, several describe a family rather than the face.
    /// </summary>
    private static byte[] Stat(IReadOnlyList<int> weights)
    {
        var w = new Writer();
        w.U16(1); w.U16(1);
        w.U16(8); w.U16(1);
        w.U32(20);
        w.U16(weights.Count);
        w.U32(28);
        w.U16(2);
        w.Bytes("wght"u8.ToArray()); w.U16(256); w.U16(0);
        for (var i = 0; i < weights.Count; i++) w.U16(2 * weights.Count + 12 * i);
        foreach (var weight in weights)
        {
            w.U16(1); w.U16(0); w.U16(0); w.U16(256);
            w.U32((uint)(weight << 16));
        }
        return w.ToArray();
    }

    /// <summary>Big-endian, as every OpenType table is.</summary>
    private sealed class Writer
    {
        private readonly MemoryStream _stream = new();

        public int Length => (int)_stream.Length;

        public void U8(byte value) => _stream.WriteByte(value);

        public void U16(int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(bytes, unchecked((ushort)value));
            _stream.Write(bytes);
        }

        public void I16(short value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteInt16BigEndian(bytes, value);
            _stream.Write(bytes);
        }

        public void U32(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            _stream.Write(bytes);
        }

        public void U32(int value) => U32(unchecked((uint)value));

        public void I64(long value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            _stream.Write(bytes);
        }

        public void Bytes(byte[] value) => _stream.Write(value);

        public void Pad4()
        {
            while (_stream.Length % 4 != 0) _stream.WriteByte(0);
        }

        public byte[] ToArray() => _stream.ToArray();
    }
}
