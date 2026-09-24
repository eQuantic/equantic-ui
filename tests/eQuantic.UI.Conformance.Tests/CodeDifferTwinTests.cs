using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The diff the code engine computes (<see cref="CodeDiffer"/>) answers the same on both sides: the
/// C# engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A diff
/// view draws what the web computes and an IDE on Photon what .NET computes, so two answers to one
/// pair of texts would be two different reviews of one change.
/// <para>
/// Compared change by change, words included, over three kinds of pair: random texts over a small
/// alphabet, where the shortest script is far from obvious and the search goes deepest; a real source
/// file under random edits, the shape a review has; and two long texts too far apart to search,
/// where the search gives up at a count of rounds and marks the whole range, which is where a clock
/// would have answered differently on each side.
/// </para>
/// </summary>
public class CodeDifferTwinTests
{
    /// <summary>Every change as one line: the lines on both sides, then each inner change's two
    /// ranges. The same text on both sides, or the two differ.</summary>
    private static string Describe(IReadOnlyList<CodeLineChange> changes) =>
        string.Join(" ", changes.Select(change =>
            $"{change.OriginalStart},{change.OriginalCount},{change.ModifiedStart},{change.ModifiedCount}"
            + string.Concat(change.Inner.Select(inner => $"|{Range(inner.Original)}/{Range(inner.Modified)}"))));

    private static string Range(CodeRange range) =>
        $"{range.Start.Line}:{range.Start.Column}-{range.End.Line}:{range.End.Column}";

    private static string Program(string runtime, IReadOnlyList<(string Original, string Modified)> pairs) => $$"""
        import { CodeDiffer, CodeDocument } from '{{runtime}}';
        const pairs = {{JsonSerializer.Serialize(pairs.Select(pair => new[] { pair.Original, pair.Modified }))}};
        const range = (r) => `${r.start.line}:${r.start.column}-${r.end.line}:${r.end.column}`;
        for (const [original, modified] of pairs) {
          const changes = CodeDiffer.compare(CodeDocument.fromText(original), CodeDocument.fromText(modified));
          console.log(changes.map((c) =>
            `${c.originalStart},${c.originalCount},${c.modifiedStart},${c.modifiedCount}`
            + c.inner.map((i) => `|${range(i.original)}/${range(i.modified)}`).join('')).join(' '));
        }
        """;

    private static void BothSidesAgree(IReadOnlyList<(string Original, string Modified)> pairs, string what)
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

        var web = JsExecutor.Run(Program(runtime, pairs), timeoutMs: 120_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        for (var i = 0; i < pairs.Count; i++)
        {
            var dotnet = Describe(CodeDiffer.Compare(CodeDocument.FromText(pairs[i].Original), CodeDocument.FromText(pairs[i].Modified)));
            var twin = i < web.Length ? web[i].TrimEnd('\r') : "(no answer)";
            if (dotnet == twin) continue;
            if (count++ < 5) differences.Append($"\n  pair {i}:\n    .NET    {dotnet}\n    the web {twin}");
        }

        count.Should().Be(0, $"{what} diff the same on both sides:{differences}");
    }

    [SkippableFact]
    public void RandomTexts_DiffTheSameOnBothSides()
    {
        var random = new Random(2026_09_24);
        var pairs = Enumerable.Range(0, 400)
            .Select(_ => (Lines(random, "abc"), Lines(random, "abc")))
            .ToList();

        BothSidesAgree(pairs, "random texts");
    }

    /// <summary>This file's own engine, under a few random edits at a time: lines removed and
    /// added, and words changed inside lines, which is what the inner changes are for.</summary>
    [SkippableFact]
    public void ARealFileUnderRandomEdits_DiffsTheSameOnBothSides()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Diff", "CodeDiffer.cs"))
            .Replace("\r\n", "\n");
        var random = new Random(2026_09_25);
        var pairs = Enumerable.Range(0, 60).Select(_ => (source, Edit(source, random))).ToList();

        BothSidesAgree(pairs, "edits of a real file");
    }

    /// <summary>
    /// Two long texts that share one line in thirty: the shortest script keeps those lines, and
    /// finding it takes more rounds than the search is given, so both sides stop at the same round
    /// and mark the whole range. A clock in place of the count would stop each side at a different
    /// point, and this would tell.
    /// </summary>
    [SkippableFact]
    public void TwoTextsTooFarApartToSearch_DiffTheSameOnBothSides()
    {
        var original = string.Join("\n", Enumerable.Range(0, 3_000).Select(i => i % 30 == 0 ? $"common {i}" : $"original {i}"));
        var modified = string.Join("\n", Enumerable.Range(0, 3_000).Select(i => i % 30 == 0 ? $"common {i}" : $"modified {i}"));

        BothSidesAgree([(original, modified)], "two texts too far apart to search");
    }

    private static string Lines(Random random, string alphabet)
    {
        var lines = new string[random.Next(0, 25)];
        for (var i = 0; i < lines.Length; i++) lines[i] = alphabet[random.Next(alphabet.Length)].ToString();
        return string.Join("\n", lines);
    }

    /// <summary>One to six edits: a line removed, a line added, or a word in a line replaced.</summary>
    private static string Edit(string text, Random random)
    {
        var lines = text.Split('\n').ToList();
        for (var edits = random.Next(1, 7); edits > 0; edits--)
        {
            var at = random.Next(lines.Count);
            switch (random.Next(3))
            {
                case 0 when lines.Count > 1:
                    lines.RemoveAt(at);
                    break;
                case 1:
                    lines.Insert(at, $"    var added{random.Next(100)} = {random.Next(1000)};");
                    break;
                default:
                    var words = lines[at].Split(' ');
                    words[random.Next(words.Length)] = $"word{random.Next(100)}";
                    lines[at] = string.Join(' ', words);
                    break;
            }
        }
        return string.Join("\n", lines);
    }
}
