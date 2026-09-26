using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The wiki's Diagnostics page names the same codes as <c>docs/DIAGNOSTICS.md</c>, in both languages.
///
/// <para>
/// <c>DiagnosticsDocumentedTests</c> holds <c>docs/DIAGNOSTICS.md</c> to the codes <c>src/</c>
/// actually raises, in both directions. Nothing held the WIKI page — the one a reader meets, and the
/// one the site publishes. So a code could be added to the compiler, documented in the repository,
/// and never reach anybody: <c>EQ4003</c> arrived with #153 and the wiki went without it in either
/// language, and no run went red. Measured again while this guard was being written, three more had
/// drifted the same way.
/// </para>
///
/// <para>
/// A ROW is what counts, not a mention. Prose may cite a code while explaining a neighbour
/// ("the same shape as EQ2006"), and a citation is not documentation — the two files agree on 49
/// and 46 rows respectively today, and the row is the unit that would still be right when they
/// do not.
/// </para>
///
/// <para>
/// Both directions, per language. A code the docs carry and the page does not is a reader who is
/// told nothing; a code the page carries and the docs do not is a reader told about something that
/// no longer exists, which is worse because it reads as current.
/// </para>
///
/// <para>
/// The wiki is a separate repository, found by <see cref="WikiClone"/>: CI clones it beside this
/// one at the pull request's own wiki branch, and a local run may name a worktree in
/// <c>EQ_WIKI_DIR</c>. On a developer's machine without it the guard skips; under
/// <c>GITHUB_ACTIONS</c> the absence is the clone having failed, and a guard passing over nothing is
/// the silence it exists to remove — the same rule its two siblings use.
/// </para>
/// </summary>
public class WikiDiagnosticsTests
{
    /// <summary>A table row, which is what documents a code. `| `EQ2011` | … | … |`</summary>
    private static readonly Regex Row = new(@"(?m)^\|\s*`(?<code>EQ\d{4})`\s*\|", RegexOptions.Compiled);

    private static readonly string? Root = LocateRoot();

    private static SortedSet<string> CodesIn(string path) =>
        new(Row.Matches(File.ReadAllText(path)).Select(m => m.Groups["code"].Value), StringComparer.Ordinal);

    [Fact]
    public void TheWikisDiagnosticsPageCarriesEveryCodeTheDocsDo_InBothLanguages()
    {
        if (WikiClone.Absent()) return;
        var wiki = WikiClone.Location!;

        var documented = CodesIn(Path.Combine(Root!, "docs", "DIAGNOSTICS.md"));
        documented.Should().NotBeEmpty("docs/DIAGNOSTICS.md is the source this page mirrors");

        // Named rather than discovered: these two pages are the contract, and a rename that lost one
        // would otherwise leave the guard reading an empty set and reporting agreement.
        (string Language, string Path)[] pages =
        [
            ("English", Path.Combine(wiki, "Diagnostics.md")),
            ("pt-BR", Path.Combine(wiki, "locale", "pt-BR", "Diagnostics-pt-BR.md")),
        ];

        var offences = new List<string>();
        foreach (var (language, path) in pages)
        {
            if (!File.Exists(path))
            {
                offences.Add($"the {language} Diagnostics page is missing from the wiki ({path})");
                continue;
            }

            var page = CodesIn(path);

            var missing = documented.Except(page, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
                offences.Add(
                    $"{language}: documented in docs/DIAGNOSTICS.md and absent from the wiki — "
                    + string.Join(", ", missing));

            var phantom = page.Except(documented, StringComparer.Ordinal).ToList();
            if (phantom.Count > 0)
                offences.Add(
                    $"{language}: named by the wiki and gone from docs/DIAGNOSTICS.md — "
                    + string.Join(", ", phantom));
        }

        // Joined rather than asserted as a collection: a collection assertion reports "at least one
        // item" and prints the first, so the English drift would be fixed and the identical pt-BR
        // drift would come back red on the next run. Both languages always drift together, because
        // the rule is that both change in one commit.
        string.Join("\n", offences).Should().BeEmpty(
            "the wiki's Diagnostics page is what a reader sees, and every wiki edit is EN + pt-BR in "
            + "the same commit — mirror the row from docs/DIAGNOSTICS.md into both pages, on the wiki branch "
            + $"named like this pull request's, which CI reads (#406). Read from {WikiClone.Describe()}");
    }

    private static string? LocateRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName;
    }

}
