using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The handoff speaks the vocabulary's CURRENT names.
///
/// <para>
/// <see cref="HandoffTokenPinTests"/> compares every NUMBER in <c>docs/design/tokens.json</c> with
/// the value the SDK returns. This is the other half of keeping the design system and the code in
/// step: the WORDS. The vocabulary renamed <c>Alt</c> to <c>Label</c>, <c>ZIndex</c> to
/// <c>Layer</c>, <c>Sticky</c> to <c>Pinned</c> and <c>Href</c> to <c>Destination</c> because a
/// target's word does not belong in an abstract vocabulary, and it never says "widget" because that
/// is Flutter's word for a component. Each of those renames moved the code and the comments; none
/// of them could move the design pages, and the A11 Image block went on showing <c>alt: "…"</c> for
/// ten days after the property stopped existing — found by this file's first run.
/// </para>
///
/// <para>
/// The handoff lives in this repo precisely so that a correction can be made here and flow back to
/// the design tool. A re-export from that tool can bring a retired word back, which is the case
/// this exists for: the pin fails naming the file, the line and the spelling, and the fix is a word.
/// </para>
/// </summary>
public class HandoffVocabularyTests
{
    private static readonly string Root = RepositoryRoot();

    /// <summary>A retired spelling, what the vocabulary says now, and the decision that retired it.</summary>
    private sealed record Retired(Regex Pattern, string Now, string Why);

    private static readonly Retired[] RetiredSpellings =
    [
        new(new Regex(@"\bwidgets?\b", RegexOptions.IgnoreCase), "component",
            "the SDK's word is component, in code, packages, docs and prose; Widget is Flutter's type and stays in Flutter's column"),
        new(new Regex(@"\balt:|\bAlt\b"), "Label / label:",
            "#81 — alt is <img alt>'s word; Label is the agnostic name twelve nodes already used"),
        new(new Regex(@"\bZIndex\b|\bzIndex\b"), "Layer",
            "#81 — z-index is CSS; Elevation and Layer are the vocabulary's depth"),
        new(new Regex(@"\bSticky\b"), "Pinned",
            "#81 — position: sticky is CSS; the node pins"),
        new(new Regex(@"\bHref\b|\bhref:"), "Destination / destination:",
            "119dd0c8 — href is HTML's attribute; Apple and Android call the same thing a link or a URL span"),
    ];

    private static readonly string[] Extensions = [".html", ".json", ".cs", ".md"];

    /// <summary>
    /// The design pages, the token export, the exported C# view and the notes — not <c>frames/</c>:
    /// those are the device and window chrome the pages are presented INSIDE, written as React
    /// components, and a <c>zIndex</c> there is a CSS property on a bezel, not a name in our
    /// vocabulary. Scanning them would make the pin fail on its own scaffolding.
    /// </summary>
    private static IEnumerable<string> HandoffFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "docs", "design"), "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(Root, f).Split(Path.DirectorySeparatorChar).Contains("frames"))
            .OrderBy(f => f, StringComparer.Ordinal);

    [Fact]
    public void TheHandoffIsThereToBeChecked()
    {
        HandoffFiles().Should().HaveCountGreaterThan(5,
            "the design system, its phases, the handoff page, the samples and the token export all live "
            + "under docs/design — a near-empty folder means this pin is reading the wrong tree");
    }

    [Fact]
    public void TheHandoffSpeaksTheVocabularysCurrentNames()
    {
        var offences = new List<string>();

        foreach (var file in HandoffFiles())
        {
            var lines = File.ReadAllLines(file);
            var relative = Path.GetRelativePath(Root, file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var retired in RetiredSpellings)
                {
                    foreach (Match match in retired.Pattern.Matches(lines[i]))
                    {
                        offences.Add($"{relative}:{i + 1}: \"{match.Value}\" — the vocabulary says {retired.Now} ({retired.Why})");
                    }
                }
            }
        }

        offences.Should().BeEmpty(
            "the handoff names a property or a node by a spelling the vocabulary retired. Correct the page "
            + $"here (docs/design is the source now) and let it flow back to the design tool:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offences) + Environment.NewLine);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads docs/design, so it has to find the tree");
        return dir!.FullName;
    }
}
