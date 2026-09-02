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
}
