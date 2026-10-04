using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler.Tests.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every statement inside a lambda's block in the shared components maps to its own C# line, or is
/// counted, per file, in a baseline that may only shrink (#384). A lambda's block maps statement by
/// statement wherever it reaches the writer as IR; one converted inside a translation that still
/// writes text loses its marks there, and a breakpoint on its lines binds nowhere. Nothing showed
/// which: the map tests read lines picked by hand. The count is the measure, and a new text seam
/// cannot lose a lambda's lines without failing here.
/// <para>
/// Read from the maps the pipeline writes, the way a debugger reads them: a statement is mapped when
/// some segment leads to the line it starts on. Regenerate with EQ_UPDATE_LAMBDA_MAP_BASELINE=1.
/// </para>
/// </summary>
public class LambdaStatementMapTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static readonly string BaselinePath =
        Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "lambda-statement-map.baseline.txt");

    [Fact]
    public void AStatementInALambdasBlock_MapsToItsLine_OrIsCountedInTheBaseline()
    {
        var root = RepoRoot();
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in SharedComponentTranspilationTests.TranspiledSharedComponents()
                     .Where(module => File.Exists(module.SourcePath))
                     .GroupBy(module => module.SourcePath))
        {
            var mapped = file
                .Where(module => module.Result.SourceMap is not null)
                .SelectMany(module => SourceMapMappings.Decode(SourceMapMappings.Extract(module.Result.SourceMap!)))
                .SelectMany(line => line)
                .Select(segment => segment.SourceLine)
                .ToHashSet();
            var unmapped = LambdaStatements(file.Key).Count(line => !mapped.Contains(line));
            if (unmapped > 0)
                counts[Path.GetRelativePath(root, file.Key).Replace(Path.DirectorySeparatorChar, '/')] = unmapped;
        }

        var report = "# Statements inside a lambda's block, per shared source, that map to no C# line of their own (#384).\n"
            + "# The count may only shrink. Regenerate with EQ_UPDATE_LAMBDA_MAP_BASELINE=1.\n"
            + $"# total: {counts.Values.Sum()}\n"
            + string.Concat(counts.Select(entry => $"{entry.Key} {entry.Value}\n"));
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_LAMBDA_MAP_BASELINE") == "1")
        {
            File.WriteAllText(BaselinePath, report);
            return;
        }

        Assert.True(File.Exists(BaselinePath),
            "No committed baseline: run once with EQ_UPDATE_LAMBDA_MAP_BASELINE=1 and commit the file.");
        var committed = File.ReadAllLines(BaselinePath)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' '))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]), StringComparer.Ordinal);

        var grew = counts.Where(entry => entry.Value > committed.GetValueOrDefault(entry.Key)).ToList();
        Assert.True(grew.Count == 0,
            "A lambda's block lost its lines' mapping where the baseline says it had them — a translation "
            + "on its way wrote it as text:\n  "
            + string.Join("\n  ", grew.Select(entry => $"{entry.Key}: {entry.Value}, the baseline {committed.GetValueOrDefault(entry.Key)}")));
        var shrank = committed.Where(entry => counts.GetValueOrDefault(entry.Key) < entry.Value).ToList();
        Assert.True(shrank.Count == 0,
            "More of a lambda's lines map than the baseline says — regenerate it "
            + "(EQ_UPDATE_LAMBDA_MAP_BASELINE=1) so the number moves on record:\n  "
            + string.Join("\n  ", shrank.Select(entry => $"{entry.Key}: {counts.GetValueOrDefault(entry.Key)}, the baseline {entry.Value}")));
    }

    /// <summary>The 0-based line each statement of a lambda's or a <c>delegate</c>'s block starts on:
    /// every statement a debugger stops at, nested ones included, and none that writes no line of
    /// its own (a block's brace, an empty statement).</summary>
    private static IEnumerable<int> LambdaStatements(string path) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot().DescendantNodes()
            .OfType<AnonymousFunctionExpressionSyntax>()
            .Select(function => function.Block)
            .OfType<BlockSyntax>()
            .SelectMany(block => block.DescendantNodes().OfType<StatementSyntax>())
            .Where(statement => statement is not (BlockSyntax or EmptyStatementSyntax))
            .Distinct()
            .Select(statement => statement.GetLocation().GetLineSpan().StartLinePosition.Line);
}
