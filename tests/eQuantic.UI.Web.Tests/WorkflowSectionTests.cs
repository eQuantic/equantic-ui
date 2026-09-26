using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The working agreement is the <c>## Workflow</c> section, and it is the same text in
/// <c>CLAUDE.md</c>, which Claude Code reads, and in <c>AGENTS.md</c>, which the other agents and
/// OpenSpec's <c>agents</c> skills read.
/// <para>
/// Two hand-kept copies drift, and this repository had the proof before the section existed:
/// <c>AGENTS.md</c> was a fork of an older <c>CLAUDE.md</c> that still documented a Tailwind
/// integration removed months earlier, and nothing noticed. An edit to one copy alone now fails
/// here, naming the first line that differs, so the two are changed in the same commit or not at all.
/// </para>
/// </summary>
public class WorkflowSectionTests
{
    private const string Heading = "## Workflow";

    /// <summary>
    /// A floor against the vacuous pass: two files that kept the heading and lost everything under it
    /// would compare equal. The section is well over a hundred lines; forty only has to catch "gone".
    /// </summary>
    private const int MinimumLines = 40;

    [Fact]
    public void TheWorkflowSection_IsTheSameText_InClaudeMdAndAgentsMd()
    {
        var root = Root();
        var claude = Section(Path.Combine(root, "CLAUDE.md"));
        var agents = Section(Path.Combine(root, "AGENTS.md"));

        claude.Length.Should().BeGreaterThanOrEqualTo(MinimumLines,
            "the Workflow section of CLAUDE.md is the whole working agreement, not a heading");

        var firstDifference = Enumerable.Range(0, Math.Max(claude.Length, agents.Length))
            .FirstOrDefault(i => i >= claude.Length || i >= agents.Length || claude[i] != agents[i], -1);

        firstDifference.Should().Be(-1,
            "the Workflow section is one text kept in two files; change CLAUDE.md and AGENTS.md in the "
            + "same commit. First difference at line {0} of the section: CLAUDE.md has \"{1}\", AGENTS.md "
            + "has \"{2}\"",
            firstDifference + 1,
            LineAt(claude, firstDifference),
            LineAt(agents, firstDifference));
    }

    /// <summary>The lines from the <c>## Workflow</c> heading up to the next level-two heading.</summary>
    private static string[] Section(string path)
    {
        File.Exists(path).Should().BeTrue($"{Path.GetFileName(path)} is where the working agreement is read from");
        var lines = File.ReadAllLines(path);

        var starts = lines.Select((line, index) => (line, index))
            .Where(entry => entry.line.TrimEnd() == Heading)
            .Select(entry => entry.index)
            .ToList();
        starts.Should().ContainSingle(
            $"{Path.GetFileName(path)} has exactly one `{Heading}` section to compare");

        var start = starts[0];
        var end = Enumerable.Range(start + 1, lines.Length - start - 1)
            .FirstOrDefault(i => lines[i].StartsWith("## ", StringComparison.Ordinal), lines.Length);
        // The blank lines before the next heading are the space between sections, which differs by
        // where the section sits in its file (CLAUDE.md goes on after it, AGENTS.md ends with it).
        while (end > start + 1 && string.IsNullOrWhiteSpace(lines[end - 1])) end--;
        return lines[start..end];
    }

    private static string LineAt(string[] lines, int index) =>
        index < 0 ? "" : index < lines.Length ? lines[index] : "(the section ended)";

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return here!.FullName;
    }
}
