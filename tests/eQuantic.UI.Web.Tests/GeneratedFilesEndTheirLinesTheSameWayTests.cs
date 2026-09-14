using eQuantic.UI.Codegen;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every generated or pinned file this repository commits ends its lines with LF, on every host.
/// <para>
/// This exists because the defect it guards is INVISIBLE on the machine that introduces it. A
/// writer that asks the host for a line break — <c>StringBuilder.AppendLine</c>,
/// <see cref="Environment.NewLine"/>, <c>JsonWriterOptions.NewLine</c>, which defaults to it —
/// produces LF on macOS and Linux and CRLF on Windows. The author sees a passing suite; the pin
/// fails on somebody else's checkout, comparing two strings that look identical in every diff.
/// Measured: six pins failed exactly that way the first time this suite ran on a Windows runner.
/// </para>
/// <para>
/// <c>.gitattributes</c> keeps the WORKING TREE LF, which is the half git can do. This is the other
/// half — what a writer produces is never something git sees.
/// </para>
/// </summary>
public class GeneratedFilesEndTheirLinesTheSameWayTests
{
    /// <summary>The committed files a pin compares against: the runtime's generated modules, the
    /// cross-pin fixtures, and the transpiled twins.</summary>
    private static IEnumerable<string> Committed()
    {
        var shared = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared");
        return Directory.EnumerateFiles(shared, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".ts", StringComparison.Ordinal)
                || path.EndsWith(".json", StringComparison.Ordinal)
                || path.EndsWith(".txt", StringComparison.Ordinal));
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        sourcePath;

    [Fact]
    public void NoCommittedFileCarriesTheOtherHostsLineEnding()
    {
        var offenders = Committed()
            .Where(path => File.ReadAllText(path).Contains('\r'))
            .Select(path => Path.GetRelativePath(RepoRoot(), path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a generated file that changes its line endings with the machine that wrote it makes "
            + "every pin comparing it fail on the next host — regenerate these on a checkout with "
            + "LF line endings, and fix the writer that produced them");
    }

    [Fact]
    public void TheWriterEveryGeneratedFileGoesThrough_BreaksLinesWithLf()
    {
        // The A/B for the generator half, and it runs on every host: CodeWriter names LF as a
        // constant, where StringBuilder.AppendLine would ask Environment.NewLine.
        new CodeWriter().AppendLine("first").AppendLine("second").ToString()
            .Should().Be("first\nsecond\n");
        new CodeWriter().AppendLine().ToString().Should().Be("\n");
    }

    [Fact]
    public void TheOptionsEveryCommittedFixtureIsWrittenWith_SayLf()
    {
        // JsonWriterOptions.NewLine defaults to Environment.NewLine, so this is a decision that has
        // to be STATED rather than inherited — and stating it once is why FixtureJson exists.
        FixtureJson.Write(new { a = 1, b = 2 }).Should().Be("{\n  \"a\": 1,\n  \"b\": 2\n}\n");
    }

    /// <summary>
    /// Where a line break may NOT come from the host, and what is forbidden in each.
    /// <para>
    /// In the GENERATORS: no <c>Environment.NewLine</c>, and no raw <c>StringBuilder</c> — this
    /// repository already has the answer there, since every generated file is written through
    /// <see cref="CodeWriter"/>, which names LF as a constant. The type rather than
    /// <c>AppendLine</c>, because the receiver is usually a local declared several lines away. One
    /// that assembles a SINGLE LINE and never a break says <c>// LINE ONLY</c>.
    /// </para>
    /// <para>
    /// In the TESTS: no <c>WriteIndented</c> outside <see cref="FixtureJson"/>. That is where an
    /// indented document picks up <c>JsonWriterOptions.NewLine</c>, which defaults to the host's.
    /// Naming the option rather than the host is what keeps this free of false positives — a
    /// failure MESSAGE formatted with <c>Environment.NewLine</c> is nobody's artifact.
    /// </para>
    /// </summary>
    private static IEnumerable<(string Directory, string[] Forbidden)> Areas()
    {
        yield return (Path.Combine(RepoRoot(), "src", "eQuantic.UI.Web.Build"),
            ["Environment.NewLine", "StringBuilder"]);
        yield return (Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests"),
            ["WriteIndented"]);
    }

    [Fact]
    public void NothingThatWritesACommittedArtifact_AsksTheHostForALineBreak()
    {
        // THE ONE THAT DISCRIMINATES ON EVERY HOST. The two tests above cannot: on macOS and Linux
        // `Environment.NewLine` already IS "\n", so a writer that asks the host looks correct here
        // and fails only on somebody else's Windows checkout. A source guard has no such blind
        // spot — it fails the moment the construct is written, wherever it is written.
        var offenders = new List<string>();
        foreach (var (directory, forbidden) in Areas())
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;
                // This file has to SPELL the forbidden names to look for them, and FixtureJson is
                // the one place the option is allowed to be written — that is what it is FOR.
                var file = Path.GetFileName(path);
                if (file == Path.GetFileName(ThisFile()) || file == "FixtureJson.cs") continue;

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    var code = lines[i].TrimStart();
                    // Prose is allowed to NAME the thing it warns about.
                    if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith('*')) continue;
                    if (code.Contains("LINE ONLY", StringComparison.Ordinal)) continue;
                    if (forbidden.Any(name => code.Contains(name, StringComparison.Ordinal)))
                        offenders.Add($"{Path.GetRelativePath(RepoRoot(), path)}:{i + 1}  {code}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a line break taken from the host makes a committed artifact differ between a Windows "
            + "checkout and every other one — write generated files through CodeWriter, and JSON "
            + "fixtures through FixtureJson");
    }
}
