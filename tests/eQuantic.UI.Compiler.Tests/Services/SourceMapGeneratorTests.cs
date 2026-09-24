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
}
