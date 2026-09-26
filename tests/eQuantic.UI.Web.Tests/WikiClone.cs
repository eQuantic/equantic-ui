using System.Diagnostics;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Where every docs guard finds the wiki it reads (#406): the directory <c>EQ_WIKI_DIR</c> names, or
/// else <c>equantic-ui.wiki</c> beside the repository, where CI clones it at the pull request's own
/// wiki branch (<c>scripts/checkout-wiki.sh</c>).
/// <para>
/// A local run against a pull request's pages points <c>EQ_WIKI_DIR</c> at a worktree of its wiki
/// branch, so the clone beside the repository, which every other local run reads, stays on master.
/// Checking a branch out there made every other branch's guards read it: #354 failed on EQ1008 rows
/// that belonged to #418's wiki branch. One locator for every guard, because a guard that kept its own
/// would read the clone while the others read the override.
/// </para>
/// </summary>
internal static class WikiClone
{
    private const string Variable = "EQ_WIKI_DIR";

    private static readonly string? Named =
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } named ? named : null;

    /// <summary>CI clones the wiki beside the repository, so a run there without it is a failed clone.</summary>
    private static readonly bool OnCi = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    /// <summary>The wiki's directory, or null when there is none. A guard asks <see cref="Absent"/>
    /// before it reads this.</summary>
    public static string? Location { get; } = Locate();

    private static string? Locate()
    {
        if (Named is not null) return Directory.Exists(Named) ? Path.GetFullPath(Named) : null;
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        if (here?.Parent is not { } parent) return null;
        var beside = Path.Combine(parent.FullName, "equantic-ui.wiki");
        return Directory.Exists(beside) ? beside : null;
    }

    /// <summary>
    /// Whether there is no wiki to read, which a guard answers by returning. Where the wiki has to be
    /// there it fails instead: when <c>EQ_WIKI_DIR</c> is set, since a typo in it would turn every guard
    /// into a pass that read no page, and on CI, where the clone is expected. A directory without the
    /// wiki's <c>Home.md</c> is not a wiki checkout, and fails the same way.
    /// </summary>
    public static bool Absent()
    {
        if (Named is not null)
        {
            Location.Should().NotBeNull($"{Variable} names {Named}, and there is no directory there: every guard would read no page");
            File.Exists(Path.Combine(Location!, "Home.md")).Should().BeTrue(
                $"{Variable} names {Location}, which has no Home.md: it is not a checkout of the wiki");
            return false;
        }
        if (Location is not null) return false;
        OnCi.Should().BeFalse(
            "CI clones equantic-ui.wiki beside this repository (ci.yml, 'Check out the wiki'), and it is not "
            + "there: the clone failed, and the guard would pass having read no page");
        return true;
    }

    /// <summary>What a guard read, for its failure message: the directory, and the branch and commit
    /// checked out there. A red run then names its input, which is what would have said in one line
    /// that #354's failure came from another pull request's wiki branch.</summary>
    public static string Describe() =>
        Location is null ? "no wiki" : $"{Location} at {Git("--abbrev-ref", "HEAD")} {Git("--short", "HEAD")}";

    /// <summary>One `git rev-parse` in the wiki, each argument passed as one: the directory is a path
    /// someone typed into a variable, and is never spliced into a command line.</summary>
    private static string Git(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in (string[])["-C", Location!, "rev-parse", .. arguments])
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && output.Length > 0 ? output : "?";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "?";
        }
    }
}
