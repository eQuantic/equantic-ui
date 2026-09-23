using System.Text;
using System.Text.Json;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// The map composition eqc does itself (#356): bun's map leads from the JavaScript to the
/// TypeScript eqc wrote, eqc's own leads from the TypeScript to the C#, and the composed map leads
/// from the JavaScript to the C#. It replaced a script over <c>@ampproject/remapping</c> that bun's
/// auto-install fetched from npm; the dashboard sample's thirteen maps compose to the same segments,
/// sources and contents either way, measured when it was replaced.
/// <para>
/// The inner map is the one <see cref="SourceMapGenerator"/> writes, and the segments are read back
/// with a decoder of this file's own, so the codec is checked by something it did not write.
/// </para>
/// </summary>
public class SourceMapComposerTests
{
    private const string Module = "../../obj/eQuantic/ts/Home.ts";

    /// <summary>eqc's map for Home.ts, as eqc writes it: TypeScript line 0 from C# (3,4) at column 0
    /// and from (4,8) at column 6, TypeScript line 1 from (7,0).</summary>
    private static readonly string Inner = new SourceMapGenerator().Generate("Home.ts", "Screens/Home.cs",
    [
        new TypeScriptCodeBuilder.SourceMapping { GeneratedLine = 1, GeneratedColumn = 0, SourceLine = 3, SourceColumn = 4 },
        new TypeScriptCodeBuilder.SourceMapping { GeneratedLine = 1, GeneratedColumn = 6, SourceLine = 4, SourceColumn = 8 },
        new TypeScriptCodeBuilder.SourceMapping { GeneratedLine = 2, GeneratedColumn = 0, SourceLine = 7, SourceColumn = 0 },
    ], "class Home {}", "../../../");

    private static string Outer(string mappings, string? debugId = "4f1b") => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["version"] = 3,
        ["sources"] = new[] { Module },
        ["sourcesContent"] = new[] { "export class Home {}" },
        ["names"] = Array.Empty<string>(),
        ["mappings"] = mappings,
        ["debugId"] = debugId,
    });

    private static string? InnerOf(string source) => source == Module ? Inner : null;

    [Fact]
    public void ATraceLeadsToTheCSharp_NamedFromTheModule_WithItsContent()
    {
        // Three JS positions on one line: TS (0,0), TS (0,7), which falls in the segment starting
        // at column 6, and TS (1,2), which falls in the one starting at 0.
        var composed = Parse(SourceMapComposer.Compose(Outer(Line([[0, 0, 0, 0], [5, 0, 0, 7], [9, 0, 1, 2]])), InnerOf));

        composed.Sources.Should().Equal("../../Screens/Home.cs");
        composed.Contents.Should().Equal("class Home {}");
        composed.Lines.Should().HaveCount(1);
        composed.Lines[0].Should().BeEquivalentTo(new[]
        {
            new[] { 0, 0, 3, 4 }, new[] { 5, 0, 4, 8 }, new[] { 9, 0, 7, 0 },
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void BunsDebugId_Survives()
    {
        using var map = JsonDocument.Parse(SourceMapComposer.Compose(Outer(Line([[0, 0, 0, 0]])), InnerOf));

        map.RootElement.GetProperty("debugId").GetString().Should().Be("4f1b",
            "an error reporter matches a map to its module by the debugId, and the script dropped it");
    }

    [Fact]
    public void ASourceThatCarriesNoMap_StaysAsTheModuleNamesIt()
    {
        var composed = Parse(SourceMapComposer.Compose(Outer(Line([[0, 0, 0, 0], [3, 0, 0, 2]])), _ => null));

        composed.Sources.Should().Equal(Module);
        composed.Contents.Should().Equal("export class Home {}");
        composed.Lines[0].Should().BeEquivalentTo(new[] { new[] { 0, 0, 0, 0 }, new[] { 3, 0, 0, 2 } },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void APositionNothingLeadsFrom_IsDropped()
    {
        // TypeScript line 2 has no mapping at all, so nothing leads from there to the C#.
        var composed = Parse(SourceMapComposer.Compose(Outer(Line([[0, 0, 0, 0], [4, 0, 2, 0]])), InnerOf));

        composed.Lines[0].Should().BeEquivalentTo(new[] { new[] { 0, 0, 3, 4 } }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ASegmentThatRepeatsTheOneBefore_AddsNothing()
    {
        // TS (0,1) and (0,3) both fall in the segment starting at column 0: one C# position.
        var composed = Parse(SourceMapComposer.Compose(Outer(Line([[0, 0, 0, 1], [2, 0, 0, 3], [4, 0, 0, 6]])), InnerOf));

        composed.Lines[0].Should().BeEquivalentTo(new[] { new[] { 0, 0, 3, 4 }, new[] { 4, 0, 4, 8 } },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void AMapWithNothingToTrace_RoundTripsItsMappings()
    {
        // Negative deltas, values past one VLQ digit, several lines and a sourceless segment: what
        // the codec reads it writes back.
        int[][][] lines =
        [
            [[0, 0, 0, 0], [40, 0, 3, 17], [41]],
            [],
            [[2, 0, 1, 900], [700, 0, 250, 3]],
        ];
        var composed = Parse(SourceMapComposer.Compose(Outer(Encode(lines)), _ => null));

        composed.Lines.Should().HaveCount(3);
        for (var line = 0; line < lines.Length; line++)
            composed.Lines[line].Should().BeEquivalentTo(lines[line], options => options.WithStrictOrdering());
    }

    // ---- a codec of this file's own ---------------------------------------------------------------

    private const string Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static string Encode(int[][][] lines)
    {
        var text = new StringBuilder();
        int source = 0, sourceLine = 0, sourceColumn = 0;
        for (var l = 0; l < lines.Length; l++)
        {
            if (l > 0) text.Append(';');
            var column = 0;
            for (var s = 0; s < lines[l].Length; s++)
            {
                var segment = lines[l][s];
                if (s > 0) text.Append(',');
                Value(text, segment[0] - column);
                column = segment[0];
                if (segment.Length == 1) continue;
                Value(text, segment[1] - source);
                Value(text, segment[2] - sourceLine);
                Value(text, segment[3] - sourceColumn);
                (source, sourceLine, sourceColumn) = (segment[1], segment[2], segment[3]);
            }
        }
        return text.ToString();
    }

    private static string Line(int[][] segments) => Encode([segments]);

    private static void Value(StringBuilder text, int value)
    {
        var vlq = value < 0 ? (-value << 1) | 1 : value << 1;
        do
        {
            var digit = vlq & 31;
            vlq >>= 5;
            text.Append(Digits[vlq > 0 ? digit | 32 : digit]);
        } while (vlq > 0);
    }

    private sealed record Map(string[] Sources, string?[] Contents, List<List<int[]>> Lines);

    private static Map Parse(string json)
    {
        using var map = JsonDocument.Parse(json);
        var root = map.RootElement;
        var sources = root.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToArray();
        var contents = root.TryGetProperty("sourcesContent", out var c)
            ? c.EnumerateArray().Select(s => s.GetString()).ToArray()
            : [];

        var lines = new List<List<int[]>> { new() };
        var fields = new List<int>();
        int column = 0, source = 0, sourceLine = 0, sourceColumn = 0;
        void End()
        {
            if (fields.Count == 0) return;
            column += fields[0];
            if (fields.Count == 1)
            {
                lines[^1].Add([column]);
            }
            else
            {
                (source, sourceLine, sourceColumn) = (source + fields[1], sourceLine + fields[2], sourceColumn + fields[3]);
                lines[^1].Add([column, source, sourceLine, sourceColumn]);
            }
            fields.Clear();
        }
        var mappings = root.GetProperty("mappings").GetString()!;
        for (var i = 0; i < mappings.Length;)
        {
            if (mappings[i] is ';' or ',')
            {
                End();
                if (mappings[i] == ';')
                {
                    lines.Add([]);
                    column = 0;
                }
                i++;
                continue;
            }
            int value = 0, shift = 0, digit;
            do
            {
                digit = Digits.IndexOf(mappings[i++]);
                value += (digit & 31) << shift;
                shift += 5;
            } while ((digit & 32) != 0);
            fields.Add((value & 1) == 1 ? -(value >> 1) : value >> 1);
        }
        End();
        return new Map(sources, contents, lines);
    }
}
