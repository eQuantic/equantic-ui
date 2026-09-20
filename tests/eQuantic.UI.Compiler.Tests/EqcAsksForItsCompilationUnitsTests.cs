using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// WHICH FILES eqc COMPILES IS <c>ProjectCompilationHelper</c>'S TO SAY, and the
/// CLI may not answer it a second way.
///
/// <para>
/// #244 was exactly that second answer: <c>src/eQuantic.Build/Program.cs</c> walked the project
/// with a raw recursive <c>Directory.GetFiles(…, "*.cs")</c> filtered by the SHAPE of each path —
/// skip <c>obj/</c> and <c>bin/</c>, unless the path contains <c>/generated/</c>. A path cannot say
/// where it came from, so that exemption let EVERY configuration's generated sources through: a
/// Release build transpiled <c>obj/Debug</c>'s factory surface and failed with EQ2006 against a
/// component the app had deleted. #250 replaced the walk with the one function that knows.
/// </para>
///
/// <para>
/// The pin that already exists watches the CALLEE. <see cref="GeneratedSourceVisibilityTests"/>
/// exercises <c>GetCompilationUnits</c> itself, and it passes 12 of 12 with the old walk restored
/// in the caller — measured, which is what makes this a hole rather than a worry: eqc is a separate
/// project the compiler tests do not reference, so nothing here could see what it does.
/// </para>
///
/// <para>
/// A SOURCE guard needs no reference, which is the whole reason it can watch a project the suite
/// cannot link — the same shape as
/// <c>eQuantic.UI.Web.Tests.GeneratedFilesEndTheirLinesTheSameWayTests</c>, which reads committed
/// files rather than running the writers. It names the owner of the rule in its failure, because a
/// guard that only says "no" sends the next person looking for a workaround.
/// </para>
/// </summary>
public class EqcAsksForItsCompilationUnitsTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>
    /// Any enumeration of <c>.cs</c> files, in either spelling and whether or not the pattern is a
    /// literal in the same call — <c>GetFiles</c>, <c>EnumerateFiles</c>, a <c>SearchOption</c> away
    /// from the walk that shipped. The pattern deliberately does not try to tell a "safe" walk from
    /// an unsafe one: the rule is that eqc does not enumerate its own inputs at all.
    /// </summary>
    private static readonly Regex Enumerates = new(
        @"(GetFiles|EnumerateFiles)\s*\([^)]*""\*\.cs""", RegexOptions.Compiled);

    [Fact]
    public void TheCliNeverEnumeratesSourceFilesItself()
    {
        var cli = Path.Combine(RepoRoot(), "src", "eQuantic.Build");
        Directory.Exists(cli).Should().BeTrue("the guard is worthless if it reads nothing");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(cli, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (Enumerates.IsMatch(lines[i]))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {lines[i].Trim()}");
        }

        offenders.Should().BeEmpty(
            "which files eqc compiles is ProjectCompilationHelper.GetCompilationUnits's answer, and "
            + "a second walk beside it is how #244 shipped: a path cannot say which configuration "
            + "generated it, so any filter over path SHAPE lets the other one's sources through. "
            + "Ask GetCompilationUnits — it returns the project's own sources plus the --generated "
            + "files, each tagged with where it came from");
    }

    /// <summary>
    /// And it DOES ask: the guard above would also pass on a CLI that read no sources at all, which
    /// is a green nobody earned. Measured together, the pair says the walk is gone AND the function
    /// that replaced it is the one being called.
    /// </summary>
    [Fact]
    public void AndItAsksTheHelperInstead()
    {
        var program = Path.Combine(RepoRoot(), "src", "eQuantic.Build", "Program.cs");

        File.ReadAllText(program).Should().Contain("GetCompilationUnits",
            "the CLI builds its compile list from the same function the semantic model is built "
            + "from — that agreement IS the fix #250 made");
    }
}
