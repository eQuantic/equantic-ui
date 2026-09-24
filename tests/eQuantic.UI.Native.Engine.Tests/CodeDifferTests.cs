using System.Diagnostics;
using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The diff the engine computes (slice 2b of docs/CODE-EDITOR-PLAN.md): the lines of two texts, and
/// within each changed region the words. Pinned three ways: by hand on the shapes a diff view shows,
/// against a dynamic programme on random texts (the script transforms one text into the other, and it
/// is as short as a longest common subsequence says it can be), and against git itself on real files
/// of this repository's history.
/// </summary>
public class CodeDifferTests
{
    private static IReadOnlyList<CodeLineChange> Compare(string original, string modified) =>
        CodeDiffer.Compare(CodeDocument.FromText(original), CodeDocument.FromText(modified));

    [Fact]
    public void TwoEqualTextsDifferNowhere()
    {
        Compare("a\nb\nc", "a\nb\nc").Should().BeEmpty();
    }

    [Fact]
    public void AnInsertedLineIsOneChangeWithNothingRemoved()
    {
        var change = Compare("a\nb\nc", "a\nb\nx\nc").Should().ContainSingle().Subject;

        change.Should().BeEquivalentTo(new { OriginalStart = 2, OriginalCount = 0, ModifiedStart = 2, ModifiedCount = 1 });
        change.Inner.Should().BeEmpty("the whole region is the change");
    }

    [Fact]
    public void ARemovedLineIsOneChangeWithNothingAdded()
    {
        Compare("a\nb\nc", "a\nc").Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { OriginalStart = 1, OriginalCount = 1, ModifiedStart = 1, ModifiedCount = 0 });
    }

    [Fact]
    public void AChangedLineMarksTheWordThatChanged()
    {
        var change = Compare("var x = 1;\nvar y = 2;", "var x = 1;\nvar z = 2;").Should().ContainSingle().Subject;

        change.Should().BeEquivalentTo(new { OriginalStart = 1, OriginalCount = 1, ModifiedStart = 1, ModifiedCount = 1 });
        var inner = change.Inner.Should().ContainSingle().Subject;
        inner.Original.Should().Be(new CodeRange(new CodePosition(1, 4), new CodePosition(1, 5)));
        inner.Modified.Should().Be(new CodeRange(new CodePosition(1, 4), new CodePosition(1, 5)));
    }

    [Fact]
    public void ALineSplitInTwoReadsAsTheBreakItIs()
    {
        var change = Compare("call(a, b);", "call(a,\n     b);").Should().ContainSingle().Subject;

        change.Inner.Should().ContainSingle().Which.Should().Be(new CodeInnerChange(
            new CodeRange(new CodePosition(0, 7), new CodePosition(0, 8)),
            new CodeRange(new CodePosition(0, 7), new CodePosition(1, 5))),
            "a space became a line break and an indent");
    }

    [Fact]
    public void AnEmojiIsNeverCutInHalf()
    {
        var change = Compare("x\U0001F600", "x\U0001F601").Should().ContainSingle().Subject;

        change.Inner.Should().ContainSingle().Which.Original.Should().Be(
            new CodeRange(new CodePosition(0, 1), new CodePosition(0, 3)), "both halves of the pair, though the first is the same");
    }

    [Fact]
    public void AnEmptySideIsAllAddedOrAllRemoved()
    {
        Compare("", "a\nb").Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { OriginalStart = 0, OriginalCount = 1, ModifiedStart = 0, ModifiedCount = 2 },
                "an empty document is one empty line");
        CodeDiffer.CompareLines(Array.Empty<string>(), ["a"]).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { OriginalStart = 0, OriginalCount = 0, ModifiedStart = 0, ModifiedCount = 1 });
    }

    /// <summary>
    /// On random texts over a small alphabet, where the shortest script is far from obvious: the
    /// changes turn the original into the modified text, and remove and add exactly as many lines as
    /// the longest common subsequence leaves.
    /// </summary>
    [Fact]
    public void TheScriptIsRightAndAsShortAsItCanBe_OnRandomTexts()
    {
        var random = new Random(2026_09_24);
        for (var round = 0; round < 500; round++)
        {
            var original = RandomLines(random);
            var modified = RandomLines(random);

            var changes = CodeDiffer.CompareLines(original, modified);

            Apply(original, modified, changes).Should().Equal(modified, $"round {round}");
            changes.Sum(c => c.OriginalCount + c.ModifiedCount).Should().Be(
                original.Length + modified.Length - 2 * Lcs(original, modified), $"round {round} is minimal");
        }
    }

    /// <summary>
    /// Every pair of texts of up to four lines over three letters, 14,641 of them: the script
    /// rebuilds the modified text and is as short as the longest common subsequence allows. The small
    /// shapes are where a walk's first rounds read the diagonals its setup wrote (two different
    /// two-line texts among them), and a random sample reaches each only by chance.
    /// </summary>
    [Fact]
    public void EverySmallPair_IsRightAndAsShortAsItCanBe()
    {
        var texts = AllTexts(4, "abc");
        var wrong = new List<string>();
        foreach (var original in texts)
        {
            foreach (var modified in texts)
            {
                var changes = CodeDiffer.CompareLines(original, modified);
                var rebuilt = Apply(original, modified, changes);
                var length = changes.Sum(c => c.OriginalCount + c.ModifiedCount);
                if (rebuilt.SequenceEqual(modified)
                    && length == original.Length + modified.Length - 2 * Lcs(original, modified)) continue;
                wrong.Add($"[{string.Join(",", original)}] against [{string.Join(",", modified)}]");
            }
        }

        texts.Should().HaveCount(121);
        wrong.Should().BeEmpty("every small pair diffs right and minimal");
    }

    /// <summary>Every text of up to <paramref name="longest"/> lines, each line one letter of
    /// <paramref name="alphabet"/>, the empty text first.</summary>
    private static List<string[]> AllTexts(int longest, string alphabet)
    {
        var texts = new List<string[]> { Array.Empty<string>() };
        var level = new List<string[]> { Array.Empty<string>() };
        for (var length = 1; length <= longest; length++)
        {
            var next = new List<string[]>();
            foreach (var text in level)
                foreach (var letter in alphabet)
                    next.Add([.. text, letter.ToString()]);
            texts.AddRange(next);
            level = next;
        }
        return texts;
    }

    /// <summary>
    /// Against git on real files: the version of a file before and after a commit on main, and the
    /// lines added and removed as <c>git diff --minimal --numstat</c> counts them. The minimal count is
    /// unique even where the script is not.
    /// </summary>
    [SkippableTheory]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Code/Editing/CodeEditorController.cs")]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Components/CodeBlock.cs")]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Code/Languages/CurlyBraceLanguage.cs")]
    [InlineData("1b255b81", "src/eQuantic.UI.Compiler/CodeGen/Strategies/Primitives/PrimitiveStaticStrategy.cs")]
    [InlineData("1b255b81", "src/eQuantic.UI.Compiler/CodeGen/Ir/JsExprWriter.cs")]
    public void TheCountsAreGitsOwn_OnRealFiles(string commit, string path)
    {
        var before = Git($"show {commit}^:{path}");
        var after = Git($"show {commit}:{path}");
        Skip.If(before is null || after is null, "git or the commit is not available here.");

        var directory = Directory.CreateTempSubdirectory("eq-diff-");
        try
        {
            var a = Path.Combine(directory.FullName, "a");
            var b = Path.Combine(directory.FullName, "b");
            File.WriteAllText(a, before);
            File.WriteAllText(b, after);
            var numstat = Git($"diff --no-index --minimal --numstat -- \"{a}\" \"{b}\"", allowDifferences: true)!;
            var fields = numstat.Split('\t');
            var (gitAdded, gitRemoved) = (int.Parse(fields[0]), int.Parse(fields[1]));

            var changes = Compare(before!, after!);

            changes.Sum(c => c.ModifiedCount).Should().Be(gitAdded, "lines added");
            changes.Sum(c => c.OriginalCount).Should().Be(gitRemoved, "lines removed");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Two long texts with nothing in common cost a shortest script the square of their length. Past
    /// a fixed number of rounds the two ranges are a rewrite, and the script may be longer than the
    /// shortest: it is still right, and it is found at once.
    /// </summary>
    [Fact]
    public void TwoLongUnrelatedTextsAreComparedAtOnce_AndRightly()
    {
        var original = Enumerable.Range(0, 10_000).Select(i => $"original line {i}").ToArray();
        var modified = Enumerable.Range(0, 10_000).Select(i => $"modified line {i}").ToArray();

        var clock = Stopwatch.StartNew();
        var changes = CodeDiffer.CompareLines(original, modified);
        clock.Stop();

        Apply(original, modified, changes).Should().Equal(modified);
        clock.ElapsedMilliseconds.Should().BeLessThan(2_000, "a rewrite is marked, not searched to the end");
    }

    private static string[] RandomLines(Random random)
    {
        var lines = new string[random.Next(0, 31)];
        for (var i = 0; i < lines.Length; i++) lines[i] = ((char)('a' + random.Next(0, 3))).ToString();
        return lines;
    }

    /// <summary>The modified text rebuilt from the original and the changes alone.</summary>
    private static List<string> Apply(string[] original, string[] modified, IReadOnlyList<CodeLineChange> changes)
    {
        var result = new List<string>();
        var at = 0;
        foreach (var change in changes)
        {
            while (at < change.OriginalStart) result.Add(original[at++]);
            for (var j = 0; j < change.ModifiedCount; j++) result.Add(modified[change.ModifiedStart + j]);
            at += change.OriginalCount;
        }
        while (at < original.Length) result.Add(original[at++]);
        return result;
    }

    private static int Lcs(string[] a, string[] b)
    {
        var table = new int[a.Length + 1, b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                table[i, j] = a[i - 1] == b[j - 1] ? table[i - 1, j - 1] + 1 : Math.Max(table[i - 1, j], table[i, j - 1]);
        return table[a.Length, b.Length];
    }

    /// <summary>git's output, or null where git or the object is not there. A diff between two
    /// different files exits 1, which is an answer, not a failure.</summary>
    private static string? Git(string arguments, bool allowDifferences = false)
    {
        try
        {
            var start = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 || (allowDifferences && process.ExitCode == 1) ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
