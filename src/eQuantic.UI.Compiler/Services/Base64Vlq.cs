using System;
using System.Collections.Generic;
using System.Text;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The Base64 VLQ a v3 source map spells its mappings in: a value's sign in its lowest bit, five
/// bits a digit, and a sixth saying another digit follows. A line is a run of segments separated by
/// commas, and lines by semicolons. The generated column restarts on every line; the source, its
/// line and column, and the name are deltas across the whole map.
/// </summary>
internal static class Base64Vlq
{
    private const string Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static readonly int[] DigitValues = BuildDigitValues();

    private static int[] BuildDigitValues()
    {
        var values = new int[128];
        Array.Fill(values, -1);
        for (var i = 0; i < Digits.Length; i++)
            values[Digits[i]] = i;
        return values;
    }

    internal static void Encode(StringBuilder into, int value)
    {
        var vlq = value < 0 ? ((-value) << 1) | 1 : value << 1;
        do
        {
            var digit = vlq & 0x1F;
            vlq >>= 5;
            if (vlq > 0) digit |= 0x20;
            into.Append(Digits[digit]);
        } while (vlq > 0);
    }

    /// <summary>
    /// A map's mappings, per generated line, each segment as ABSOLUTE values: the generated column,
    /// then the source, its line and its column, then the name, as far as the segment has them (one,
    /// four or five fields).
    /// </summary>
    internal static List<List<int[]>> DecodeMappings(string mappings)
    {
        var lines = new List<List<int[]>>();
        var line = new List<int[]>();
        var fields = new List<int>(5);
        int column = 0, source = 0, sourceLine = 0, sourceColumn = 0, name = 0;

        void EndSegment()
        {
            if (fields.Count == 0) return;
            column += fields[0];
            if (fields.Count >= 4)
            {
                source += fields[1];
                sourceLine += fields[2];
                sourceColumn += fields[3];
            }
            if (fields.Count == 5) name += fields[4];
            line.Add(fields.Count switch
            {
                1 => [column],
                4 => [column, source, sourceLine, sourceColumn],
                5 => [column, source, sourceLine, sourceColumn, name],
                _ => throw new FormatException($"a mapping segment has {fields.Count} fields; a v3 map has 1, 4 or 5"),
            });
            fields.Clear();
        }

        for (var i = 0; i < mappings.Length;)
        {
            var c = mappings[i];
            if (c == ';')
            {
                EndSegment();
                lines.Add(line);
                line = [];
                column = 0;
                i++;
                continue;
            }
            if (c == ',')
            {
                EndSegment();
                i++;
                continue;
            }

            int value = 0, shift = 0, digit;
            do
            {
                if (i >= mappings.Length) throw new FormatException("a mapping value ends in the middle");
                var ch = mappings[i++];
                digit = ch < 128 ? DigitValues[ch] : -1;
                if (digit < 0) throw new FormatException($"'{ch}' is not a Base64 VLQ digit");
                value += (digit & 0x1F) << shift;
                shift += 5;
            } while ((digit & 0x20) != 0);
            fields.Add((value & 1) == 1 ? -(value >> 1) : value >> 1);
        }

        EndSegment();
        lines.Add(line);
        return lines;
    }

    /// <summary>The mappings for <paramref name="lines"/> of absolute segments, the inverse of
    /// <see cref="DecodeMappings"/>. Empty lines at the end are not written.</summary>
    internal static string EncodeMappings(IReadOnlyList<IReadOnlyList<int[]>> lines)
    {
        var count = lines.Count;
        while (count > 0 && lines[count - 1].Count == 0) count--;

        var mappings = new StringBuilder();
        int source = 0, sourceLine = 0, sourceColumn = 0, name = 0;
        for (var l = 0; l < count; l++)
        {
            if (l > 0) mappings.Append(';');
            var column = 0;
            for (var s = 0; s < lines[l].Count; s++)
            {
                var segment = lines[l][s];
                if (s > 0) mappings.Append(',');
                Encode(mappings, segment[0] - column);
                column = segment[0];
                if (segment.Length < 4) continue;
                Encode(mappings, segment[1] - source);
                Encode(mappings, segment[2] - sourceLine);
                Encode(mappings, segment[3] - sourceColumn);
                source = segment[1];
                sourceLine = segment[2];
                sourceColumn = segment[3];
                if (segment.Length < 5) continue;
                Encode(mappings, segment[4] - name);
                name = segment[4];
            }
        }
        return mappings.ToString();
    }
}
