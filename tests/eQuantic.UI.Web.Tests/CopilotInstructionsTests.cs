using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// GitHub Copilot reviews every pull request (the ruleset requires it), and the repository gives it
/// the rules a review holds a pull request to: <c>.github/copilot-instructions.md</c> for every file,
/// and one file per area under <c>.github/instructions/</c>, each scoped by its <c>applyTo</c> globs
/// (#1418). Copilot's review reads only the first 4,000 characters of an instruction file, so a file
/// that grows past them loses its last rules without a word: the review still runs, and simply stops
/// holding a pull request to them. This fails first.
/// </summary>
public class CopilotInstructionsTests
{
    /// <summary>What Copilot's code review reads of one custom instruction file.</summary>
    private const int ReviewReadsCharacters = 4000;

    [Fact]
    public void EveryInstructionFile_FitsWhatCopilotsReviewReads()
    {
        var files = InstructionFiles();

        files.Should().Contain(path => path.EndsWith("copilot-instructions.md", StringComparison.Ordinal),
            "the repository-wide instructions are the floor every review reads");
        files.Should().HaveCountGreaterThan(1, "the areas have instructions of their own");

        foreach (var path in files)
        {
            var length = File.ReadAllText(path).Length;
            length.Should().BeLessThanOrEqualTo(ReviewReadsCharacters,
                $"Copilot's review reads the first {ReviewReadsCharacters} characters of {Relative(path)}, and "
                + "a rule past them is never applied: split the file by area, or say the same in fewer words");
        }
    }

    [Fact]
    public void EveryAreaFile_SaysWhichPathsItCovers()
    {
        foreach (var path in InstructionFiles().Where(path => !path.EndsWith("copilot-instructions.md", StringComparison.Ordinal)))
        {
            var lines = File.ReadAllLines(path);
            lines.First().Should().Be("---", $"{Relative(path)} opens with its front matter");
            lines.TakeWhile((_, index) => index == 0 || lines[index] != "---")
                .Should().Contain(line => line.StartsWith("applyTo: \"", StringComparison.Ordinal),
                    $"{Relative(path)} names the paths it covers, or Copilot applies it to nothing");
        }
    }

    private static IReadOnlyList<string> InstructionFiles()
    {
        var github = Path.Combine(Root(), ".github");
        var files = new List<string> { Path.Combine(github, "copilot-instructions.md") };
        var areas = Path.Combine(github, "instructions");
        if (Directory.Exists(areas))
            files.AddRange(Directory.GetFiles(areas, "*.instructions.md").OrderBy(path => path, StringComparer.Ordinal));
        return files.Where(File.Exists).ToList();
    }

    private static string Relative(string path) => Path.GetRelativePath(Root(), path).Replace('\\', '/');

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return here!.FullName;
    }
}
