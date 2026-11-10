using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// No page of the wiki, in any of its languages, and no Markdown file of this repository holds a merge's
/// conflict markers (#1351). A merge of the wiki committed them into <c>Upgrading.md</c> on master, and
/// every docs guard passed: each reads what it is about (a version mark, a snippet, a diagnostic) and none
/// reads whether the page is whole. The page was fixed by hand; this is the guard that fails.
/// </summary>
public class ConflictMarkerTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Fact]
    public void EveryPageOfTheWiki_InEveryLanguage_HoldsNoConflictMarker()
    {
        if (WikiClone.Absent()) return;
        var wiki = WikiClone.Location!;
        var pages = Directory.EnumerateFiles(wiki, "*.md", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(wiki, path).StartsWith(".git", StringComparison.Ordinal))
            .ToList();

        pages.Should().Contain(path => path.Contains($"{Path.DirectorySeparatorChar}locale{Path.DirectorySeparatorChar}"),
            "the guard reads the translations too, under locale/");
        Offences(wiki, pages).Should().BeEmpty(
            "a merge left a conflict unresolved in the wiki, read from {0}: resolve it in every language the page is written in",
            WikiClone.Describe());
    }

    [Fact]
    public void EveryMarkdownFileOfTheRepository_HoldsNoConflictMarker()
    {
        var root = RepoRoot();
        // Every Markdown file git tracks, which is every one a reader can open, and none a build wrote.
        var (exitCode, output, error) = ChildProcess.Run("git", ["-C", root, "ls-files", "-z", "--", "*.md"], TimeSpan.FromMinutes(1));
        exitCode.Should().Be(0, error);
        var files = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(path => Path.Combine(root, path)).ToList();

        files.Should().HaveCountGreaterThan(100, "the repository's Markdown is read from git, and git listed it");
        Offences(root, files).Should().BeEmpty("a merge left a conflict unresolved in this repository's Markdown");
    }

    [Theory]
    [InlineData("<<<<<<< HEAD")]
    [InlineData("<<<<<<<")]
    [InlineData("||||||| base")]
    [InlineData(">>>>>>> origin/master")]
    public void APlantedMarker_IsFound(string marker)
    {
        var page = $"# Upgrading\n\nSome text.\n{marker}\nMore text.\n";

        ConflictMarkers.In(page).Should().ContainSingle().Which.Should().Be((4, marker));
    }

    [Fact]
    public void AWholeConflict_IsFoundAtEachOfItsMarkers()
    {
        var page = "# Upgrading\r\n\r\n<<<<<<< HEAD\r\nOurs.\r\n=======\r\nTheirs.\r\n>>>>>>> branch\r\n";

        ConflictMarkers.In(page).Select(marker => marker.Line).Should().Equal(3, 5, 7);
    }

    [Theory]
    [InlineData("Title\n=======\n\nText.\n")]
    [InlineData("# Page\n\nA heading\n=======\n")]
    [InlineData("Title\n=========\n")]
    [InlineData("<<<<<<<< eight is not git's\n>>>>>>>> nor this\n")]
    [InlineData("A line that says <<<<<<< HEAD in the middle.\n")]
    public void AHeadingsUnderline_AndTextThatOnlyLooksLikeAMarker_AreNotMarkers(string page) =>
        ConflictMarkers.In(page).Should().BeEmpty();

    [Theory]
    [InlineData("=======\nText.\n")]
    [InlineData("Text.\n\n=======\nMore.\n")]
    public void ASeparatorNoTitleCouldOwn_IsFound(string page) =>
        ConflictMarkers.In(page).Should().ContainSingle();

    private static List<string> Offences(string root, IEnumerable<string> files) =>
        files.SelectMany(path => ConflictMarkers.In(File.ReadAllText(path))
                .Select(marker => $"{Path.GetRelativePath(root, path)}:{marker.Line}: {marker.Text}"))
            .ToList();
}
