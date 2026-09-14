using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The wiki speaks the tree's CURRENT names.
///
/// <para>
/// <c>WikiClaimsCompile</c> compiles the declarative snippets and <c>WikiVersionMarkTests</c> checks
/// every "Since" mark, and neither can see a NAME that the tree retired: an assembly that was
/// dissolved, a package that was removed, an option that no longer exists, a word the project does
/// not use. <c>eQuantic.UI.Core</c> was dissolved in #83 and went on being cited as a live
/// instruction in six pages — "All types are in <c>eQuantic.UI.Core.Assets</c>" — ten days later;
/// <c>eQuantic.UI.Tailwind</c> sat in the supported-features table as a shipping package; and the
/// word the project never uses for a component appeared in four pages of its own prose.
/// </para>
///
/// <para>
/// So the retired spellings are listed here with the decision that retired each, and every wiki page
/// is read against them, in both languages. A page that needs one of these words to tell HISTORY —
/// the Upgrading page, the package page that explains the dissolution — names itself in the allowance
/// list with the reason, and the allowance is checked in the other direction too: an allowed page
/// that no longer contains the word has to leave the list, or the list becomes a record of what
/// somebody once believed. The same shape as the handoff's <c>HandoffVocabularyTests</c> and the
/// vocabulary's coverage pin, because it is the same disease on a third support.
/// </para>
///
/// <para>
/// The wiki is a separate repository, cloned beside this one by CI (see <c>ci.yml</c>). Where it is
/// absent — a checkout with no wiki beside it — the test passes having checked nothing, as the two
/// existing wiki guards do, and says so in its output.
/// </para>
/// </summary>
public class WikiVocabularyTests
{
    /// <summary>A retired spelling, what the tree says now, and the decision that retired it.</summary>
    private sealed record Retired(string Name, Regex Pattern, string Now, string Why);

    private static readonly Retired[] RetiredSpellings =
    [
        new("Core", new Regex(@"\beQuantic\.UI\.Core\b"), "eQuantic.UI.Web (the DOM mirror), eQuantic.UI.Server (assets, rendering), eQuantic.UI.Primitives (the component model)",
            "#83 dissolved eQuantic.UI.Core into the assemblies that owned its parts"),
        new("Tailwind", new Regex(@"\beQuantic\.UI\.Tailwind\b"), "the atomic style engine",
            "the Tailwind adapter was removed; typed styles lower to atomic CSS"),
        new("EnableDefaultCss", new Regex(@"\bEnableDefaultCss\b"), "nothing — the option is gone",
            "UIOptions has no such property"),
        new("widget", new Regex(@"\bwidgets?\b", RegexOptions.IgnoreCase), "component",
            "the project's word is component; Flutter's word stays in Flutter's column, and Android's class names are Android's"),
        new("Alt", new Regex(@"\bImage\.Alt\b|\balt:"), "Label / label:",
            "#81 — alt is <img alt>'s word; Label is the agnostic name"),
        new("ZIndex", new Regex(@"\bZIndex\b"), "Layer", "#81 — z-index is CSS"),
        new("Sticky", new Regex(@"\bSticky\b"), "Pinned", "#81 — position: sticky is CSS"),
        new("Href", new Regex(@"\bHref\b"), "Destination", "119dd0c8 — href is HTML's attribute"),
        new("eqx", new Regex(@"\.eqx\b"), ".cs — there is one authoring format",
            "the .eqx markup format was excised in 2026-08 with the rest of the pre-write-once authoring"),
    ];

    /// <summary>
    /// The pages allowed to carry a retired spelling, each for a stated reason — history, a quoted
    /// platform name, an HTML attribute the page documents as HTML. Keyed by the page's file name as
    /// the wiki spells it (the pt-BR twin is a separate entry, because it is a separate page).
    /// <para>
    /// Not here on purpose: the pages that say "there is no <c>UseTailwind()</c>" or "by the makers
    /// of Tailwind CSS" — the pattern is the PACKAGE name <c>eQuantic.UI.Tailwind</c>, and a plain
    /// mention of the vendor is not a retired spelling; and <c>CookieConsent(policyHref)</c>, whose
    /// lowercase parameter the <c>Href</c> pattern does not match — that name is the audit's to
    /// retire in code, and the page will follow the code.
    /// </para>
    /// </summary>
    private static readonly (string Page, string Name, string Why)[] Allowed =
    [
        ("Upgrading.md", "Core", "the migration notes tell what moved where"),
        ("Upgrading-pt-BR.md", "Core", "the same notes in Portuguese"),
        ("Upgrading.md", "ZIndex", "the rename is a migration note"),
        ("Upgrading-pt-BR.md", "ZIndex", "the same note"),
        ("Upgrading.md", "Sticky", "the rename is a migration note"),
        ("Upgrading-pt-BR.md", "Sticky", "the same note"),
        ("PackageArchitecture.md", "Core", "explains the dissolution and where each part went"),
        ("PackageArchitecture-pt-BR.md", "Core", "the same explanation"),
        ("Icons.md", "Core", "history: the provider interface that lived there once"),
        ("Icons-pt-BR.md", "Core", "the same history"),
        ("CodeEditor.md", "Core", "a layering bug the page explains by the assembly it involved"),
        ("CodeEditor-pt-BR.md", "Core", "the same explanation"),
        ("Photon.md", "widget", "android.widget.Button is Android's class name"),
        ("Photon-pt-BR.md", "widget", "the same class name"),
        ("WriteOnceComponents.md", "Href", "the rename from Href is the page's own version note"),
        ("WriteOnceComponents-pt-BR.md", "Href", "the same note"),
        ("Assets.md", "Href", "a table that documents the HTML <link href> attribute as HTML"),
        ("Assets-pt-BR.md", "Href", "the same table"),
    ];

    private static readonly string? Wiki = LocateWiki();

    private static IEnumerable<string> Pages() =>
        Wiki is null
            ? []
            : Directory.EnumerateFiles(Wiki, "*.md", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith('_'))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                .OrderBy(f => f, StringComparer.Ordinal);

    [Fact]
    public void TheWikiSpeaksTheTreesCurrentNames()
    {
        if (Wiki is null) return; // no wiki beside this checkout — see the class summary

        var offences = new List<string>();
        foreach (var page in Pages())
        {
            var file = Path.GetFileName(page);
            var lines = File.ReadAllLines(page);
            foreach (var retired in RetiredSpellings)
            {
                if (Allowed.Any(a => a.Page == file && a.Name == retired.Name)) continue;
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in retired.Pattern.Matches(lines[i]))
                        offences.Add($"{file}:{i + 1}: \"{match.Value}\" — the tree says {retired.Now} ({retired.Why})");
                }
            }
        }

        offences.Should().BeEmpty(
            "a wiki page names something by a spelling the tree retired. Correct the page, in both "
            + $"languages, or allow it here with the reason:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offences) + Environment.NewLine);
    }

    /// <summary>An allowance that has stopped being needed is the list lying from the other end.</summary>
    [Fact]
    public void NoAllowanceOutlivesTheMentionItExcuses()
    {
        if (Wiki is null) return;

        var byName = Pages().ToDictionary(Path.GetFileName, StringComparer.Ordinal);
        var stale = Allowed
            .Where(a => byName.TryGetValue(a.Page, out var path)
                && !RetiredSpellings.Single(r => r.Name == a.Name).Pattern.IsMatch(File.ReadAllText(path)))
            .Select(a => $"{a.Page} / {a.Name}")
            .ToArray();

        stale.Should().BeEmpty("these pages no longer contain the word they are allowed; remove the allowance: "
            + string.Join(", ", stale));
    }

    [Fact]
    public void EveryAllowance_NamesARealPageAndARealSpelling()
    {
        if (Wiki is null) return;

        var pages = Pages().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        Allowed.Where(a => !pages.Contains(a.Page)).Should().BeEmpty("an allowance for a page that is not in the wiki is dead prose");
        Allowed.Where(a => RetiredSpellings.All(r => r.Name != a.Name)).Should().BeEmpty("an allowance must name a listed spelling");
    }

    private static string? LocateWiki()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        if (here is null) return null;
        var wiki = Path.Combine(Directory.GetParent(here.FullName)!.FullName, "equantic-ui.wiki");
        return Directory.Exists(wiki) ? wiki : null;
    }
}
