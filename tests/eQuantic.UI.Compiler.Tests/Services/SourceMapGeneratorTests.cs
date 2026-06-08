using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// Regression tests for the source-map column bugs: generated columns used to be
/// collapsed to a constant and source columns were off by one. Mappings are stored
/// 0-based (GeneratedColumn / SourceLine / SourceColumn); GeneratedLine is 1-based.
/// </summary>
public class SourceMapGeneratorTests
{
    [Fact]
    public void EncodesGeneratedAndSourceColumns_ZeroBased_NoCollapse()
    {
        var mappings = new List<TypeScriptCodeBuilder.SourceMapping>
        {
            new() { GeneratedLine = 1, GeneratedColumn = 0, SourceLine = 0, SourceColumn = 0, SourceFile = "a.cs" },
            new() { GeneratedLine = 1, GeneratedColumn = 4, SourceLine = 0, SourceColumn = 10, SourceFile = "a.cs" },
            new() { GeneratedLine = 2, GeneratedColumn = 8, SourceLine = 5, SourceColumn = 2, SourceFile = "a.cs" },
        };

        var json = new SourceMapGenerator().Generate("out.js", "a.cs", mappings);
        var segments = DecodeMappings(ExtractMappings(json));

        // Line 0 has two segments at distinct generated columns (not collapsed to one value).
        segments[0][0].GeneratedColumn.Should().Be(0);
        segments[0][0].SourceColumn.Should().Be(0); // 0-based: was off-by-one (emitted 0-based-1 => -1/SC)
        segments[0][1].GeneratedColumn.Should().Be(4);
        segments[0][1].SourceColumn.Should().Be(10);

        // Line 1 resets the generated column base, source line/col are cumulative.
        segments[1][0].GeneratedColumn.Should().Be(8);
        segments[1][0].SourceLine.Should().Be(5);
        segments[1][0].SourceColumn.Should().Be(2);
    }

    private static string ExtractMappings(string json)
    {
        var match = Regex.Match(json, "\"mappings\"\\s*:\\s*\"([^\"]*)\"");
        match.Success.Should().BeTrue("source map must contain a mappings field");
        return match.Groups[1].Value;
    }

    private record Segment(int GeneratedColumn, int SourceFile, int SourceLine, int SourceColumn);

    /// <summary>Decodes a V3 mappings string into absolute (reconstructed) segment values.</summary>
    private static List<List<Segment>> DecodeMappings(string mappings)
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
