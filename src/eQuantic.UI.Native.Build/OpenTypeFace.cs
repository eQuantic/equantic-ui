using System.Buffers.Binary;
using System.Text;

namespace eQuantic.UI.Build;

/// <summary>
/// What an OpenType file says about itself: its family, the weights it sets and whether it is the
/// italic cut. Read so an app declares nothing about the faces it ships: the file already states
/// all of it, and a declaration beside it would be a second answer to keep in step.
/// <list type="bullet">
/// <item>The family is the <c>name</c> table's typographic family (ID 16), or its family (ID 1)
/// when it has none. A variable font names the whole family in ID 16 and only a style group in ID 1.</item>
/// <item>The weight is <c>OS/2</c>'s <c>usWeightClass</c>, or the range of the <c>fvar</c> table's
/// <c>wght</c> axis for a variable font.</item>
/// <item>The italic is <c>OS/2</c>'s ITALIC or OBLIQUE bit, or the <c>head</c> table's when there is no <c>OS/2</c>.</item>
/// </list>
/// </summary>
/// <param name="Family">The family the file belongs to.</param>
/// <param name="MinWeight">The lightest weight it sets.</param>
/// <param name="MaxWeight">The heaviest weight it sets.</param>
/// <param name="Italic">Whether it is the italic cut.</param>
public sealed record OpenTypeFace(string Family, int MinWeight, int MaxWeight, bool Italic)
{
    /// <summary>
    /// Reads a TrueType (<c>.ttf</c>) or CFF (<c>.otf</c>) font. Throws <see cref="FormatException"/>,
    /// with the reason in words, for anything else: a collection, a web font, a file that is not a
    /// font, or one whose tables do not hold.
    /// </summary>
    public static OpenTypeFace Read(ReadOnlySpan<byte> font)
    {
        if (font.Length < 12) throw new FormatException("it is too short to be a font");
        var version = BinaryPrimitives.ReadUInt32BigEndian(font);
        switch (version)
        {
            case 0x00010000 or 0x4F54544F /* OTTO */ or 0x74727565 /* true */:
                break;
            case 0x74746366 /* ttcf */:
                throw new FormatException("it is a font collection; ship each face as a file of its own");
            case 0x774F4646 /* wOFF */ or 0x774F4632 /* wOF2 */:
                throw new FormatException("it is a web font (WOFF); ship the OpenType file it was made from, which every target reads");
            default:
                throw new FormatException("it is not an OpenType font");
        }

        var tables = Tables(font);
        if (!tables.TryGetValue("name", out var name)) throw new FormatException("it has no name table");
        var family = FamilyName(Slice(font, name))
            ?? throw new FormatException("its name table names no family");

        var os2 = tables.TryGetValue("OS/2", out var os2Table) ? Slice(font, os2Table) : default;
        var weight = os2.Length >= 6 ? BinaryPrimitives.ReadUInt16BigEndian(os2[4..]) : 400;
        if (weight is < 1 or > 1000) throw new FormatException($"its weight class, {weight}, is not a weight");
        // ITALIC (bit 0) or OBLIQUE (bit 9): a slanted roman is the cut italic text is set in too.
        var italic = os2.Length >= 64
            ? (BinaryPrimitives.ReadUInt16BigEndian(os2[62..]) & (1 | 1 << 9)) != 0
            : tables.TryGetValue("head", out var head) && Slice(font, head) is { Length: >= 46 } headBytes
                && (BinaryPrimitives.ReadUInt16BigEndian(headBytes[44..]) & 2) != 0;

        var (min, max) = (weight, weight);
        if (tables.TryGetValue("fvar", out var fvar) && WeightAxis(Slice(font, fvar)) is { } axis) (min, max) = axis;
        return new OpenTypeFace(family, min, max, italic);
    }

    private static Dictionary<string, (int Offset, int Length)> Tables(ReadOnlySpan<byte> font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font[4..]);
        if (12 + 16 * count > font.Length) throw new FormatException("its table directory runs past its end");
        var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var record = font.Slice(12 + 16 * i, 16);
            var tag = Encoding.ASCII.GetString(record[..4]);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            if (offset + (ulong)length > (ulong)font.Length)
                throw new FormatException($"its {tag} table runs past its end");
            tables[tag] = ((int)offset, (int)length);
        }
        return tables;
    }

    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> font, (int Offset, int Length) table) =>
        font.Slice(table.Offset, table.Length);

    /// <summary>
    /// The typographic family (ID 16) when the table has one, the family (ID 1) otherwise; in each,
    /// the Windows English record first, then any Unicode one, then a Macintosh Roman one. A Windows
    /// record is UTF-16BE whatever its encoding says of the font's characters: Unicode (1), the full
    /// repertoire (10) or Symbol (0), the one record a symbol font carries.
    /// </summary>
    private static string? FamilyName(ReadOnlySpan<byte> table)
    {
        if (table.Length < 6) throw new FormatException("its name table is too short");
        var count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
        var storage = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
        if (6 + 12 * count > table.Length) throw new FormatException("its name records run past the table");
        foreach (var id in (ReadOnlySpan<int>)[16, 1])
        {
            string? best = null;
            var rank = int.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var record = table.Slice(6 + 12 * i, 12);
                if (BinaryPrimitives.ReadUInt16BigEndian(record[6..]) != id) continue;
                var platform = BinaryPrimitives.ReadUInt16BigEndian(record);
                var encoding = BinaryPrimitives.ReadUInt16BigEndian(record[2..]);
                var language = BinaryPrimitives.ReadUInt16BigEndian(record[4..]);
                var length = BinaryPrimitives.ReadUInt16BigEndian(record[8..]);
                var offset = BinaryPrimitives.ReadUInt16BigEndian(record[10..]);
                var (decoder, order) = (platform, encoding) switch
                {
                    (3, 0 or 1 or 10) => (Encoding.BigEndianUnicode, language == 0x0409 ? 0 : 1),
                    (0, _) => (Encoding.BigEndianUnicode, 2),
                    (1, 0) => (MacRoman, 3),
                    _ => ((Encoding?)null, int.MaxValue),
                };
                if (decoder is null || order >= rank || storage + offset + length > table.Length) continue;
                var text = decoder.GetString(table.Slice(storage + offset, length)).Trim();
                if (text.Length == 0) continue;
                (best, rank) = (text, order);
            }
            if (best is not null) return best;
        }
        return null;
    }

    /// <summary>The Macintosh platform's Roman encoding (code page 10000), which is not Latin-1 past
    /// ASCII: an <c>é</c> is <c>0x8E</c> there and a control character in Latin-1.</summary>
    private static readonly Encoding MacRoman = MacRomanEncoding();

    private static Encoding MacRomanEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(10000);
    }

    /// <summary>The <c>wght</c> axis's range, rounded to whole weights, or null for a font with none.</summary>
    private static (int Min, int Max)? WeightAxis(ReadOnlySpan<byte> table)
    {
        if (table.Length < 16) throw new FormatException("its fvar table is too short");
        var axes = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
        var count = BinaryPrimitives.ReadUInt16BigEndian(table[8..]);
        var size = BinaryPrimitives.ReadUInt16BigEndian(table[10..]);
        if (size < 20 || axes + count * size > table.Length) throw new FormatException("its fvar axes run past the table");
        for (var i = 0; i < count; i++)
        {
            var axis = table.Slice(axes + i * size, size);
            if (Encoding.ASCII.GetString(axis[..4]) != "wght") continue;
            var min = (int)Math.Round(BinaryPrimitives.ReadInt32BigEndian(axis[4..]) / 65536.0);
            var max = (int)Math.Round(BinaryPrimitives.ReadInt32BigEndian(axis[12..]) / 65536.0);
            if (min < 1 || max > 1000 || min > max)
                throw new FormatException($"its weight axis, {min} to {max}, is not a range of weights");
            return (min, max);
        }
        return null;
    }
}
