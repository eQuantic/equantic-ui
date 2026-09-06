using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The solution is the pack roster's MIRROR. CI discovers packable projects by walking src/ (every
/// csproj that does not say IsPackable=false), but a developer packs the SOLUTION — and a project
/// missing from it silently vanishes from the local feed while nuget.org keeps receiving it. It
/// happened three times: Native.Shell.iOS and Runtime (an iOS consumer of the local feed could not
/// restore), then Generators (a web app could not restore, found while rehearsing the site's bump
/// against packed main). This keeps the two rosters one.
/// </summary>
public class PackRosterTests
{
    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "eQuantic.UI.sln")))
            here = here.Parent;
        return here!.FullName;
    }

    [Fact]
    public void EveryPackableProjectUnderSrc_IsInTheSolution()
    {
        var root = RepoRoot();
        var solution = File.ReadAllText(Path.Combine(root, "eQuantic.UI.sln"));
        var inSolution = Regex.Matches(solution, @"""[^""]*[\\/]([^""\\/]+)\.csproj""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => Path.GetDirectoryName(path)!.Split(Path.DirectorySeparatorChar).Last() == Path.GetFileNameWithoutExtension(path)
                        || Directory.GetParent(path)!.Parent!.FullName == Path.Combine(root, "src"))
            .Where(path => !Regex.IsMatch(File.ReadAllText(path), @"<IsPackable>\s*false\s*</IsPackable>", RegexOptions.IgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !inSolution.Contains(name!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        missing.Should().BeEmpty(
            "a packable project the solution does not list vanishes from `dotnet pack` of the solution "
            + "while CI keeps publishing it — add it with `dotnet sln add`");
    }

    /// <summary>
    /// Building the solution in Release builds every project in Release.
    /// <para>
    /// A solution maps each of ITS configurations onto one of each project's, and the two halves are
    /// not alike: the PLATFORM legitimately differs, because these projects are AnyCPU and
    /// <c>Release|x64</c> reaching <c>Release|Any CPU</c> is how that is written. The CONFIGURATION
    /// may not, because <c>$(Configuration)</c> is what every output path and every tool path in this
    /// SDK is keyed on.
    /// </para>
    /// <para>
    /// Measured: <c>DefaultUIDashboard</c> mapped all three Release configurations onto
    /// <c>Debug|Any CPU</c> from 2026-02-07, so `dotnet build eQuantic.UI.sln -c Release` built that
    /// sample in DEBUG and said nothing — Debug binaries in a Release build for seven months. It
    /// became visible only when EQ4001 arrived and the sample went looking for an icon tool in a
    /// Debug folder a Release build never writes. Nothing in a diff shows this: the rows are one
    /// word different, in a file nobody reads, for a project that still compiled.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryProjectBuildsInTheConfigurationTheSolutionAsksFor()
    {
        var (names, section) = Solution();

        // Named down to the PLATFORM row, because one platform can drift while its siblings are
        // right, and "Release is wrong" would then send the reader through three rows to find which.
        // ActiveCfg and Build.0 are one decision written twice, so the pair collapses to one line.
        var rows = Regex.Matches(section,
            @"(\{[0-9A-Fa-f-]{36}\})\.([^.|]+\|[^.]+)\.(?:ActiveCfg|Build\.0) = ([^|\r\n]+)\|");

        // A guard that reads NOTHING passes. Whatever reshapes this file — a rename, a reformat,
        // another generator — has to leave the rows readable or say so here, rather than leave a
        // green test looking at an empty match set.
        //
        // The floor is ONE ROW PER PROJECT, taken from the roster so it tracks the repository
        // instead of a constant somebody has to remember to raise. Deliberately far below the real
        // count (twelve rows per project today): asking for all of them would make THIS test fail
        // for a missing row, which is the sibling's question, reported in the sibling's words.
        rows.Count.Should().BeGreaterThanOrEqualTo(names.Count,
            "the solution's configuration rows have to be READ for this to assert anything — if the "
            + "file's shape changed, teach the pattern rather than let it match nothing");

        var drift = rows
            .Where(m => !string.Equals(m.Groups[2].Value.Split('|')[0].Trim(), m.Groups[3].Value.Trim(),
                StringComparison.Ordinal))
            .Select(m => $"{names.GetValueOrDefault(m.Groups[1].Value, m.Groups[1].Value)}: the solution's "
                       + $"{m.Groups[2].Value.Trim()} builds it as {m.Groups[3].Value.Trim()}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        drift.Should().BeEmpty(
            "a project that builds Debug inside a Release solution build produces Debug binaries "
            + "beside everyone else's Release ones, and resolves every $(Configuration) path — the "
            + "SDK's own tool paths included — to a folder that build never wrote");
    }

    /// <summary>
    /// And every project is built at all, in every configuration the solution offers.
    /// <para>
    /// The sibling above compares the mapping a row DECLARES. This one is about the row that is not
    /// there: a project keeps its <c>ActiveCfg</c> (so an IDE still shows it) and loses its
    /// <c>Build.0</c>, and the solution stops building it — the compiler never speaks, every test
    /// that names it goes on passing against yesterday's binary, and `dotnet build` reports success
    /// for a build that skipped it. The same silence as the mapping drift, one row further along.
    /// </para>
    /// <para>
    /// Raised by review on the pull request that added the sibling: nothing in a drift check can see
    /// an absent row, because there is nothing to compare. Excluding a project from a configuration
    /// on purpose is legitimate and rare — do it by changing this expectation, so the decision is
    /// read by someone instead of inferred from a gap.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryProjectIsBuiltInEveryConfigurationTheSolutionOffers()
    {
        var (names, section) = Solution();
        var configurations = SolutionConfigurations();

        var declared = Regex.Matches(section,
                @"(\{[0-9A-Fa-f-]{36}\})\.([^.|]+\|[^.]+)\.(ActiveCfg|Build\.0) = ")
            .Select(m => (Guid: m.Groups[1].Value, Configuration: m.Groups[2].Value.Trim(), Kind: m.Groups[3].Value))
            .ToHashSet();

        // Solution FOLDERS carry no configuration and are not missing one — the roster is the
        // projects, which is what having a `.csproj` means. Taken from the same declaration lines the
        // sibling reads names from, so the two tests cannot disagree about what a project is.
        names.Should().NotBeEmpty("the projects have to be READ for this to assert anything");
        configurations.Should().NotBeEmpty("so do the configurations they are mapped onto");

        var absent = (
            from project in names
            from configuration in configurations
            from kind in new[] { "ActiveCfg", "Build.0" }
            where !declared.Contains((project.Key, configuration, kind))
            select $"{project.Value}: no {kind} for {configuration}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        absent.Should().BeEmpty(
            "a project with no Build.0 is in the solution and not built by it: nothing fails, "
            + "nothing is compiled, and whatever is already in bin/ answers for it");
    }

    /// <summary>
    /// The projects by GUID — <c>.csproj</c> declarations only, so solution folders stay out — and
    /// the section that says how each is built. Both tests read the file the same way on purpose: a
    /// second opinion about what a project is would let one of them cover a hole the other reports.
    /// </summary>
    private static (Dictionary<string, string> Names, string Section) Solution()
    {
        var solution = File.ReadAllText(Path.Combine(RepoRoot(), "eQuantic.UI.sln"));
        var section = Regex.Match(solution,
            @"GlobalSection\(ProjectConfigurationPlatforms\).*?EndGlobalSection", RegexOptions.Singleline);
        section.Success.Should().BeTrue("the solution has to say what it builds");

        // The GUID names the project only in the declaration lines, so a failure is reported by NAME:
        // a bare GUID sends the reader hunting for which project it is.
        var names = Regex.Matches(solution,
                @"Project\(""\{[0-9A-F-]+\}""\) = ""([^""]+)"", ""[^""]+\.csproj"", ""(\{[0-9A-Fa-f-]{36}\})""")
            .ToDictionary(m => m.Groups[2].Value, m => m.Groups[1].Value, StringComparer.OrdinalIgnoreCase);

        return (names, section.Value);
    }

    /// <summary>What the solution offers to build — <c>Debug|Any CPU</c>, <c>Release|x64</c> and the
    /// rest, each written as its own name on both sides of the equals sign.</summary>
    private static List<string> SolutionConfigurations()
    {
        var solution = File.ReadAllText(Path.Combine(RepoRoot(), "eQuantic.UI.sln"));
        var section = Regex.Match(solution,
            @"GlobalSection\(SolutionConfigurationPlatforms\).*?EndGlobalSection", RegexOptions.Singleline);
        section.Success.Should().BeTrue("the solution has to say what it offers");

        return Regex.Matches(section.Value, @"^\s*(\S[^\r\n=]*?) = \1\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();
    }
}
