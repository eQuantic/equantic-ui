using System.Diagnostics;
using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Reading a unified diff: the files of a patch and their hunks, the shapes git writes around them,
/// and, against git itself, a patch that rebuilds the modified file from the original.
/// </summary>
public class CodePatchTests
{
    private const string Patch = """
        diff --git a/src/Ledger.cs b/src/Ledger.cs
        index 1111111..2222222 100644
        --- a/src/Ledger.cs
        +++ b/src/Ledger.cs
        @@ -1,4 +1,4 @@ namespace Ledger
         using System;
        -var total = 1;
        +var total = 2;

         // end
        @@ -10 +10,2 @@ public sealed class Invoice
        --- a line that starts like a header, removed
        +-- the same, kept
        +++ a line that starts like the other header
        \ No newline at end of file
        diff --git a/docs/new.md b/docs/new.md
        new file mode 100644
        --- /dev/null
        +++ b/docs/new.md
        @@ -0,0 +1,2 @@
        +# New
        +text
        diff --git a/old name.txt b/new name.txt
        similarity index 100%
        rename from old name.txt
        rename to new name.txt
        diff --git a/logo.png b/logo.png
        Binary files a/logo.png and b/logo.png differ
        """;

    [Fact]
    public void ThePatchIsReadFileByFile()
    {
        var files = CodePatch.Parse(Patch);

        files.Select(f => (f.OriginalPath, f.ModifiedPath)).Should().Equal(
            ("src/Ledger.cs", "src/Ledger.cs"),
            (null, "docs/new.md"),
            ("old name.txt", "new name.txt"),
            ("logo.png", "logo.png"));
        files[2].Hunks.Should().BeEmpty("a rename with nothing changed has no hunk");
        files[3].Binary.Should().BeTrue();
    }

    [Fact]
    public void AHunkCountsItsLinesFromZero_AndKeepsItsSection()
    {
        var hunk = CodePatch.Parse(Patch)[0].Hunks[0];

        hunk.Should().BeEquivalentTo(new { OriginalStart = 0, OriginalCount = 4, ModifiedStart = 0, ModifiedCount = 4, Section = "namespace Ledger" });
        hunk.Lines.Select(l => l.Kind).Should().Equal(
            CodePatchLineKind.Context, CodePatchLineKind.Removed, CodePatchLineKind.Added,
            CodePatchLineKind.Context, CodePatchLineKind.Context);
        hunk.Lines[3].Text.Should().Be("", "an empty context line written as nothing at all is still a line");
    }

    [Fact]
    public void ALineThatLooksLikeAHeader_StaysInItsHunk()
    {
        var hunk = CodePatch.Parse(Patch)[0].Hunks[1];

        hunk.Should().BeEquivalentTo(new { OriginalStart = 9, OriginalCount = 1, ModifiedStart = 9, ModifiedCount = 2 });
        hunk.Lines.Should().Equal(
            new CodePatchLine(CodePatchLineKind.Removed, "-- a line that starts like a header, removed"),
            new CodePatchLine(CodePatchLineKind.Added, "-- the same, kept"),
            new CodePatchLine(CodePatchLineKind.Added, "++ a line that starts like the other header"));
    }

    [Fact]
    public void ANewFilesSideOfNothingStartsWhereItsLinesWouldGo()
    {
        var hunk = CodePatch.Parse(Patch)[1].Hunks.Should().ContainSingle().Subject;

        hunk.Should().BeEquivalentTo(new { OriginalStart = 0, OriginalCount = 0, ModifiedStart = 0, ModifiedCount = 2 });
    }

    [Fact]
    public void APlainDiffU_WithTimestamps_IsReadToo()
    {
        var files = CodePatch.Parse("""
            --- before.txt	2026-09-24 10:00:00.000000000 +0100
            +++ after.txt	2026-09-24 10:05:00.000000000 +0100
            @@ -2,2 +2,2 @@
             b
            -c
            +C
            """);

        files.Should().ContainSingle().Which.Should().BeEquivalentTo(new { OriginalPath = "before.txt", ModifiedPath = "after.txt" });
        files[0].Hunks.Should().ContainSingle().Which.OriginalStart.Should().Be(1);
    }

    /// <summary>
    /// Against git on real files: the patch git writes between two versions of a file, read and
    /// applied to the first version, rebuilds the second, line for line.
    /// </summary>
    [SkippableTheory]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Code/Editing/CodeEditorController.cs")]
    [InlineData("9bc9ae8d", "src/eQuantic.UI.Components/CodeBlock.cs")]
    [InlineData("1b255b81", "src/eQuantic.UI.Compiler/CodeGen/Strategies/Primitives/PrimitiveStaticStrategy.cs")]
    public void GitsOwnPatch_RebuildsTheModifiedFile(string commit, string path)
    {
        var before = Git($"show {commit}^:{path}");
        var after = Git($"show {commit}:{path}");
        Skip.If(before is null || after is null, "git or the commit is not available here.");
        var directory = Directory.CreateTempSubdirectory("eq-patch-");
        try
        {
            var a = Path.Combine(directory.FullName, "a");
            var b = Path.Combine(directory.FullName, "b");
            File.WriteAllText(a, before);
            File.WriteAllText(b, after);
            var patch = Git($"diff --no-index -U3 -- \"{a}\" \"{b}\"", allowDifferences: true)!;

            var file = CodePatch.Parse(patch).Should().ContainSingle().Subject;

            var original = before!.Replace("\r\n", "\n").Split('\n').ToList();
            var rebuilt = new List<string>();
            var at = 0;
            foreach (var hunk in file.Hunks)
            {
                while (at < hunk.OriginalStart) rebuilt.Add(original[at++]);
                foreach (var line in hunk.Lines)
                {
                    if (line.Kind != CodePatchLineKind.Added) original[at++].Should().Be(line.Text, "the hunk quotes the original");
                    if (line.Kind != CodePatchLineKind.Removed) rebuilt.Add(line.Text);
                }
            }
            while (at < original.Count) rebuilt.Add(original[at++]);

            rebuilt.Should().Equal(after!.Replace("\r\n", "\n").Split('\n'));
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
