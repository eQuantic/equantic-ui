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
/// The wiki is a separate repository, cloned beside this one by CI. On a developer's machine
/// without it the guard skips; under <c>GITHUB_ACTIONS</c> the absence is the clone having failed,
/// and a guard passing over nothing is the silence it exists to remove — the same rule its two
/// siblings use.
/// </para>
/// </summary>
public class WikiDiagnosticsTests
{
    /// <summary>A table row, which is what documents a code. `| `EQ2011` | … | … |`</summary>
    private static readonly Regex Row = new(@"(?m)^\|\s*`(?<code>EQ\d{4})`\s*\|", RegexOptions.Compiled);

    private static readonly string? Root = LocateRoot();
    private static readonly string? Wiki = LocateWiki();

    /// <summary>CI clones the wiki beside the repo; a checkout without it there is a failed clone, not a choice.</summary>
    private static readonly bool WikiExpected = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    private static bool NoWikiHere()
    {
        if (Wiki is not null) return false;
        WikiExpected.Should().BeFalse(
            "CI clones equantic-ui.wiki beside this repository (ci.yml, 'Check out the wiki'), and it is not "
            + "there — the clone failed, and this guard would pass having read no page");
        return true;
    }

    private static SortedSet<string> CodesIn(string path) =>
        new(Row.Matches(File.ReadAllText(path)).Select(m => m.Groups["code"].Value), StringComparer.Ordinal);

    [Fact]
    public void TheWikisDiagnosticsPageCarriesEveryCodeTheDocsDo_InBothLanguages()
    {
        if (NoWikiHere()) return;

        var documented = CodesIn(Path.Combine(Root!, "docs", "DIAGNOSTICS.md"));
        documented.Should().NotBeEmpty("docs/DIAGNOSTICS.md is the source this page mirrors");

        // Named rather than discovered: these two pages are the contract, and a rename that lost one
        // would otherwise leave the guard reading an empty set and reporting agreement.
        (string Language, string Path)[] pages =
        [
            ("English", Path.Combine(Wiki!, "Diagnostics.md")),
            ("pt-BR", Path.Combine(Wiki!, "locale", "pt-BR", "Diagnostics-pt-BR.md")),
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
            + "the same commit — mirror the row from docs/DIAGNOSTICS.md into both pages");
    }

    private static string? LocateRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName;
    }

    private static string? LocateWiki()
    {
        if (Root is null) return null;
        var wiki = Path.Combine(Directory.GetParent(Root)!.FullName, "equantic-ui.wiki");
        return Directory.Exists(wiki) ? wiki : null;
    }
}
