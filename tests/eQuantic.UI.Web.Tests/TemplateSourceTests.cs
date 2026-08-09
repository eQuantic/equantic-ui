using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The scaffolded app, checked WITHOUT scaffolding it.
///
/// <para>
/// The SDK injects the write-once aliases and the declarative factory surface into every file
/// (Sdk.props). A template that also declares one of them by hand is a duplicate — `CS1537: the
/// using alias appeared previously` — and the ONLY thing that ever noticed was the release gate, on
/// a tag, after a full build on three operating systems. That is a very slow way to learn that
/// `dotnet new equantic-app` no longer compiles.
/// </para>
/// </summary>
public class TemplateSourceTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string[] TemplateSources() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Templates"), "*.cs",
                SearchOption.AllDirectories)
            // obj/ holds the STAMPED copy the pack produces — the same files, one build behind.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToArray();

    /// <summary>What Sdk.props puts in every file of every consumer's project, without an import.</summary>
    private static readonly (string Pattern, string Name)[] ImplicitUsings =
    [
        (@"^\s*using\s+StatefulComponent\s*=", "StatefulComponent alias"),
        (@"^\s*using\s+StatelessComponent\s*=", "StatelessComponent alias"),
        (@"^\s*using\s+static\s+eQuantic\.UI\.Components\.UI\s*;", "the UI factory surface"),
    ];

    [Fact]
    public void No_template_source_redeclares_what_the_SDK_already_injects()
    {
        var offenders = new List<string>();
        foreach (var path in TemplateSources())
        {
            var text = File.ReadAllText(path);
            foreach (var (pattern, name) in ImplicitUsings)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.Multiline))
                    offenders.Add($"{Path.GetFileName(path)} declares {name}");
            }
        }

        offenders.Should().BeEmpty("the SDK injects these implicitly — declaring one again is CS1537 "
            + "in the first project a newcomer scaffolds");
    }

    [Fact]
    public void The_templates_are_actually_there_to_check()
    {
        // A test that silently checks nothing is worse than no test: if the layout moves, this fails
        // instead of passing over an empty set.
        TemplateSources().Should().HaveCountGreaterThan(3);
    }
}
