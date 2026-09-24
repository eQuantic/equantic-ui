using System.Diagnostics;
using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a diff view shows: two whole texts, or the lines one file of a patch quotes, with the changes
/// the patch marks, the number each line has in its file, and what the patch left out between hunks.
/// </summary>
public class CodeDiffSourceTests
{
    private const string Patch = """
        diff --git a/src/Ledger.cs b/src/Ledger.cs
        --- a/src/Ledger.cs
        +++ b/src/Ledger.cs
        @@ -1,4 +1,4 @@
         using System;
        -var total = 1;
        +var total = 2;
         
         // end
        @@ -10,2 +10,3 @@
         var a = 1;
        -var b = 2;
        +var b = 20;
        +var c = 3;
        diff --git a/docs/new.md b/docs/new.md
        --- /dev/null
        +++ b/docs/new.md
        @@ -0,0 +1,2 @@
        +# New
        +text
        """;

    [Fact]
    public void TwoTextsAreWholeFiles()
    {
        var source = CodeDiffSource.FromTexts("a\nb\nc", "a\nB\nc");

        source.OriginalLineCount.Should().Be(3);
        source.ModifiedNumber(2).Should().Be(3, "a whole file's line is numbered by its index");
        source.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { OriginalStart = 1, OriginalCount = 1 });
        source.Gaps.Should().BeEmpty();
    }

    [Fact]
    public void APatchIsTheLinesItQuotes_NumberedAsItNumbersThem()
    {
        var source = CodeDiffSource.FromPatch(CodePatch.Parse(Patch)[0]);

        source.Original.Lines.Should().Equal("using System;", "var total = 1;", "", "// end", "var a = 1;", "var b = 2;");
        source.Modified.Lines.Should().Equal("using System;", "var total = 2;", "", "// end", "var a = 1;", "var b = 20;", "var c = 3;");
        source.OriginalNumber(4).Should().Be(10, "the second hunk starts at line 10 of the file");
        source.ModifiedNumber(6).Should().Be(12);
        source.Gaps.Should().ContainSingle().Which.Should().Be(new CodeDiffGap(4, 4, 5, 5, "@@ -10,2 +10,3 @@"),
            "the gap says what the patch says in its place, the header of the hunk after it");
    }

    [Fact]
    public void APatchsChangesAreTheOnesItMarks_WithTheirWords()
    {
        var source = CodeDiffSource.FromPatch(CodePatch.Parse(Patch)[0]);

        source.Changes.Select(c => (c.OriginalStart, c.OriginalCount, c.ModifiedStart, c.ModifiedCount))
            .Should().Equal((1, 1, 1, 1), (5, 1, 5, 2));
        source.Changes[0].Inner.Should().ContainSingle().Which.Modified.Should().Be(
            new CodeRange(new CodePosition(1, 12), new CodePosition(1, 13)), "only the 1 became a 2");
    }

    [Fact]
    public void ANewFilesOriginalSideHasNoLines()
    {
        var source = CodeDiffSource.FromPatch(CodePatch.Parse(Patch)[1]);

        source.OriginalLineCount.Should().Be(0, "the view counts none, though a document holds one empty line");
        source.ModifiedLineCount.Should().Be(2);
        source.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { OriginalStart = 0, OriginalCount = 0, ModifiedStart = 0, ModifiedCount = 2 });
    }

    /// <summary>
    /// Against git on real files: every line a patch quotes, on either side, is the line its number
    /// names in that side's file.
    /// </summary>
    [SkippableTheory]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Code/Editing/CodeEditorController.cs")]
    [InlineData("1b255b81", "src/eQuantic.UI.Compiler/CodeGen/Strategies/Primitives/PrimitiveStaticStrategy.cs")]
    public void EveryQuotedLineIsTheLineItsNumberNames_OnRealFiles(string commit, string path)
    {
        var before = Git($"show {commit}^:{path}");
        var after = Git($"show {commit}:{path}");
        Skip.If(before is null || after is null, "git or the commit is not available here.");
        var directory = Directory.CreateTempSubdirectory("eq-source-");
        try
        {
            var a = Path.Combine(directory.FullName, "a");
            var b = Path.Combine(directory.FullName, "b");
            File.WriteAllText(a, before);
            File.WriteAllText(b, after);
            var patch = Git($"diff --no-index -U3 -- \"{a}\" \"{b}\"", allowDifferences: true)!;

            var source = CodeDiffSource.FromPatch(CodePatch.Parse(patch).Single());

            var original = before!.Replace("\r\n", "\n").Split('\n');
            var modified = after!.Replace("\r\n", "\n").Split('\n');
            for (var line = 0; line < source.OriginalLineCount; line++)
                source.Original.Line(line).Should().Be(original[source.OriginalNumber(line) - 1], $"original line {line}");
            for (var line = 0; line < source.ModifiedLineCount; line++)
                source.Modified.Line(line).Should().Be(modified[source.ModifiedNumber(line) - 1], $"modified line {line}");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

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
