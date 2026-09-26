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
/// The wiki is a separate repository, cloned beside this one by CI (see <c>ci.yml</c>, where the
/// clone is <c>continue-on-error</c>). On a developer's machine with no wiki beside the checkout the
/// guards skip, as the two existing wiki guards do. In CI they do NOT: a missing wiki there means the
/// clone failed, and three guards passing over nothing is exactly the silence they exist to remove —
/// so under <c>GITHUB_ACTIONS</c> the absence is a failure that names the step.
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
        // Member access, the factory's named argument, and the object initializer (`new Image(…) { Alt = … }`)
        // — Alt was an init property before #81. A line that names neither node is the escape hatch's
        // `HtmlElement.Alt`, which mirrors <img alt> on purpose and stays.
        new("Alt", new Regex(@"\b(Image|CameraPreview)\.Alt\b|\balt:|\b(Image|CameraPreview)\b[^\n]*\bAlt\s*="), "Label / label:",
            "#81 — alt is <img alt>'s word; Label is the agnostic name, on Image and CameraPreview alike"),
        // The three below are narrowed to the VOCABULARY's retired members on purpose: the DOM escape
        // hatch keeps `HtmlStyle.ZIndex` and `Position.Sticky` because it mirrors CSS, and a page that
        // documents the escape hatch is documenting the web in the web's words, which #81 kept.
        new("ZIndex", new Regex(@"\bPositioned\.ZIndex\b|\bPositioned\b[^\n]*\bzIndex\b"), "Layer",
            "#81 — z-index is CSS; Positioned.Layer is the vocabulary's depth (HtmlStyle.ZIndex stays: it IS CSS)"),
        new("Sticky", new Regex(@"\bnew Sticky\b|\bSticky\("), "Pinned",
            "#81 — position: sticky is CSS; the node is Pinned (Position.Sticky on the escape hatch stays)"),
        new("Href", new Regex(@"\b(Link|TextRun|Crumb)\.Href\b|\bLink\([^\n)]*\bhref:"), "Destination",
            "119dd0c8 — href is HTML's attribute; the vocabulary's link names a Destination"),
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
        ("PackageArchitecture.md", "Core", "explains the dissolution and where each part went"),
        ("PackageArchitecture-pt-BR.md", "Core", "the same explanation"),
        ("Icons.md", "Core", "history: the provider interface that lived there once"),
        ("Icons-pt-BR.md", "Core", "the same history"),
        ("CodeEditor.md", "Core", "a layering bug the page explains by the assembly it involved"),
        ("CodeEditor-pt-BR.md", "Core", "the same explanation"),
        ("Photon.md", "widget", "android.widget.Button is Android's class name"),
        ("Photon-pt-BR.md", "widget", "the same class name"),
    ];

    /// <summary>True when the guard may skip: no wiki, and nobody promised one (<see cref="WikiClone"/>).</summary>
    private static bool NoWikiHere() => WikiClone.Absent();

    private static IEnumerable<string> Pages() =>
        WikiClone.Location is not { } wiki
            ? []
            : Directory.EnumerateFiles(wiki, "*.md", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith('_'))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                .OrderBy(f => f, StringComparer.Ordinal);

    [Fact]
    public void TheWikiSpeaksTheTreesCurrentNames()
    {
        if (NoWikiHere()) return;

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
            $"a wiki page ({WikiClone.Describe()}) names something by a spelling the tree retired. Correct the page, in both "
            + $"languages, or allow it here with the reason:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offences) + Environment.NewLine);
    }

    /// <summary>An allowance that has stopped being needed is the list lying from the other end.</summary>
    [Fact]
    public void NoAllowanceOutlivesTheMentionItExcuses()
    {
        if (NoWikiHere()) return;

        // A LAMBDA, not the method group: `GetFileName` is annotated [NotNullIfNotNull(path)] and
        // a method group converts to Func<string, string?>, which drops it — so TKey infers as
        // `string?` and fails the notnull constraint. Same shape as the refs table in the design host.
        var byName = Pages().ToDictionary(path => Path.GetFileName(path), StringComparer.Ordinal);
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
        if (NoWikiHere()) return;

        var pages = Pages().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        Allowed.Where(a => !pages.Contains(a.Page)).Should().BeEmpty("an allowance for a page that is not in the wiki is dead prose");
        Allowed.Where(a => RetiredSpellings.All(r => r.Name != a.Name)).Should().BeEmpty("an allowance must name a listed spelling");
    }

}
