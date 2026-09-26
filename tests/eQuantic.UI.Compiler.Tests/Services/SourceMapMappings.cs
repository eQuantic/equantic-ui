using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>A v3 map's <c>mappings</c>, decoded into absolute segments, one list per generated line —
/// what a debugger reads, for the tests that ask where a position leads.</summary>
internal static class SourceMapMappings
{
    public record Segment(int GeneratedColumn, int SourceFile, int SourceLine, int SourceColumn);

    public static string Extract(string json)
    {
        var match = Regex.Match(json, "\"mappings\"\\s*:\\s*\"([^\"]*)\"");
        match.Success.Should().BeTrue("source map must contain a mappings field");
        return match.Groups[1].Value;
    }

    /// <summary>Decodes a V3 mappings string into absolute (reconstructed) segment values.</summary>
    public static List<List<Segment>> Decode(string mappings)
    {
        var result = new List<List<Segment>>();
        int srcFile = 0, srcLine = 0, srcCol = 0;

        foreach (var lineGroup in mappings.Split(';'))
        {
            int genCol = 0; // generated column resets on each line
            var line = new List<Segment>();
            if (lineGroup.Length > 0)
            {
                foreach (var seg in lineGroup.Split(','))
                {
                    var values = DecodeVlq(seg);
                    genCol += values[0];
                    if (values.Count >= 4)
                    {
                        srcFile += values[1];
                        srcLine += values[2];
                        srcCol += values[3];
                    }
                    line.Add(new Segment(genCol, srcFile, srcLine, srcCol));
                }
            }
            result.Add(line);
        }

        return result;
    }

    private const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static List<int> DecodeVlq(string segment)
    {
        var values = new List<int>();
        int shift = 0, value = 0;

        foreach (var c in segment)
        {
            int digit = Base64Chars.IndexOf(c);
            bool hasContinuation = (digit & 0x20) != 0;
            digit &= 0x1F;
            value += digit << shift;

            if (hasContinuation)
            {
                shift += 5;
            }
            else
            {
                bool negative = (value & 1) == 1;
                value >>= 1;
                values.Add(negative ? -value : value);
                shift = 0;
                value = 0;
            }
        }

        return values;
    }
}
