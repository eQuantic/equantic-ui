using System.Text.Json;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// Regression tests for the source-map column bugs: generated columns used to be
/// collapsed to a constant and source columns were off by one. Mappings are stored
/// 0-based (GeneratedColumn / SourceLine / SourceColumn); GeneratedLine is 1-based.
/// </summary>
public class SourceMapGeneratorTests
{
    private static readonly SyntaxTree A = CSharpSyntaxTree.ParseText("class A {}", path: "a.cs");
    private static readonly SyntaxTree B = CSharpSyntaxTree.ParseText("interface B {}", path: "b.cs");

    private static SourceMapSource Named(SyntaxTree tree) => new(tree.FilePath, tree.GetText().ToString());

    private static TypeScriptCodeBuilder.SourceMapping At(int line, int column, int sourceLine, int sourceColumn, SyntaxTree source) =>
        new() { GeneratedLine = line, GeneratedColumn = column, SourceLine = sourceLine, SourceColumn = sourceColumn, Source = source };

    [Fact]
    public void EncodesGeneratedAndSourceColumns_ZeroBased_NoCollapse()
    {
        List<TypeScriptCodeBuilder.SourceMapping> mappings = [At(1, 0, 0, 0, A), At(1, 4, 0, 10, A), At(2, 8, 5, 2, A)];

        var json = new SourceMapGenerator().Generate("out.js", mappings, Named);
        var segments = SourceMapMappings.Decode(SourceMapMappings.Extract(json));

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

    /// <summary>A module written from two files names both, in the order its mappings reach them, and
    /// each segment the one it came from (#490): the second file's index was never written.</summary>
    [Fact]
    public void TwoFiles_AreTwoSources_AndEachSegmentNamesItsOwn()
    {
        List<TypeScriptCodeBuilder.SourceMapping> mappings = [At(1, 0, 0, 0, A), At(2, 4, 7, 2, B), At(3, 4, 3, 1, A), At(3, 9, 8, 6, B)];

        var json = new SourceMapGenerator().Generate("out.js", mappings, Named);
        using var map = JsonDocument.Parse(json);
        var segments = SourceMapMappings.Decode(SourceMapMappings.Extract(json));

        map.RootElement.GetProperty("sources").EnumerateArray().Select(source => source.GetString()).Should().Equal("a.cs", "b.cs");
        map.RootElement.GetProperty("sourcesContent").EnumerateArray().Select(content => content.GetString())
            .Should().Equal("class A {}", "interface B {}");
        segments[0].Select(segment => (segment.SourceFile, segment.SourceLine)).Should().Equal((0, 0));
        segments[1].Select(segment => (segment.SourceFile, segment.SourceLine)).Should().Equal((1, 7));
        segments[2].Select(segment => (segment.SourceFile, segment.SourceLine, segment.SourceColumn)).Should().Equal((0, 3, 1), (1, 8, 6));
    }

    /// <summary>The compiler parses a file a second time beside the compilation's own tree of it, and
    /// the two trees are one file.</summary>
    [Fact]
    public void TwoTreesOfOnePath_AreOneSource()
    {
        var again = CSharpSyntaxTree.ParseText("class A {}", path: "a.cs");

        var json = new SourceMapGenerator().Generate("out.js", [At(1, 0, 0, 0, A), At(2, 0, 1, 0, again)], Named);
        using var map = JsonDocument.Parse(json);

        map.RootElement.GetProperty("sources").EnumerateArray().Select(source => source.GetString()).Should().Equal("a.cs");
        SourceMapMappings.Decode(SourceMapMappings.Extract(json)).SelectMany(line => line)
            .Should().OnlyContain(segment => segment.SourceFile == 0);
    }

    /// <summary>Every control character, in a source's name and in its content: the map escaped five
    /// characters by hand and wrote the rest raw, and its names not at all (#525).</summary>
    [Fact]
    public void EveryControlCharacter_InANameAndAContent_ReadsBackAsWritten()
    {
        var odd = string.Concat(Enumerable.Range(0, 0x20).Select(code => (char)code)) + "\"\\/";

        var json = new SourceMapGenerator().Generate(odd + ".ts", [At(1, 0, 0, 0, A)],
            _ => new SourceMapSource(odd, odd), sourceRoot: odd);
        using var map = JsonDocument.Parse(json);

        map.RootElement.GetProperty("file").GetString().Should().Be(odd + ".ts");
        map.RootElement.GetProperty("sourceRoot").GetString().Should().Be(odd);
        map.RootElement.GetProperty("sources")[0].GetString().Should().Be(odd);
        map.RootElement.GetProperty("sourcesContent")[0].GetString().Should().Be(odd);
    }

    /// <summary>A map that carries no C# (External) has no <c>sourcesContent</c> at all.</summary>
    [Fact]
    public void ASourceWithNoContent_WritesNoSourcesContent()
    {
        var json = new SourceMapGenerator().Generate("out.js", [At(1, 0, 0, 0, A)], tree => new SourceMapSource(tree.FilePath, null));
        using var map = JsonDocument.Parse(json);

        map.RootElement.TryGetProperty("sourcesContent", out _).Should().BeFalse();
    }
}
