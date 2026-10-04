using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The v3 map from a module eqc wrote to the C# it came from.
/// </summary>
public class SourceMapGenerator
{
    /// <summary>
    /// A v3 map from one generated file to every file its mappings come from, each segment naming its
    /// own (#490). A module is not always written from one file: a class takes the default its
    /// interface supplies, and that body converts from the interface's file. The map named one
    /// source, the class's, so the default's segments led to lines of the class's file that it does
    /// not have.
    /// </summary>
    /// <param name="generatedFileName">The generated file the map is for.</param>
    /// <param name="mappings">Each position of the generated file and the C# it came from.</param>
    /// <param name="describe">What the map calls a source file and carries of it. Files are numbered
    /// in the order the mappings first reach them, and two trees of one path are one file.</param>
    /// <param name="sourceRoot">Prepended to a source's name when it is resolved, relative to where
    /// the map itself is written: the way back from the map to the project the sources are named in.</param>
    public string Generate(string generatedFileName, IReadOnlyList<TypeScriptCodeBuilder.SourceMapping> mappings,
        Func<SyntaxTree, SourceMapSource> describe, string sourceRoot = "")
    {
        var sources = new List<SourceMapSource>();
        var numbered = new Dictionary<object, int>();
        var lines = new List<List<int[]>>();

        // The generated line is 1-based and the column 0-based; Roslyn's source positions are
        // already 0-based, as the format wants them.
        foreach (var mapping in mappings.OrderBy(m => m.GeneratedLine).ThenBy(m => m.GeneratedColumn))
        {
            // A tree with no path is a file of its own: two such trees are not one file.
            var file = mapping.Source.FilePath.Length > 0 ? mapping.Source.FilePath : (object)mapping.Source;
            if (!numbered.TryGetValue(file, out var source))
            {
                numbered[file] = source = sources.Count;
                sources.Add(describe(mapping.Source));
            }
            while (lines.Count < mapping.GeneratedLine) lines.Add([]);
            lines[mapping.GeneratedLine - 1].Add([mapping.GeneratedColumn, source, mapping.SourceLine, mapping.SourceColumn]);
        }

        return SourceMapWriter.Write(generatedFileName, sourceRoot,
            sources.Select(source => source.Name).ToList(), sources.Select(source => source.Content).ToList(),
            names: [], Base64Vlq.EncodeMappings(lines), debugId: null);
    }
}
