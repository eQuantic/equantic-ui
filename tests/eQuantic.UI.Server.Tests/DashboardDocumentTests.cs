using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;

// Moq is a global using here and brings its own Match.
using Match = System.Text.RegularExpressions.Match;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// What the dashboard SERVES, asserted on the document rather than on a lowered node, which is where
/// the faults below become visible and nowhere earlier.
/// </summary>
public class DashboardDocumentTests(DashboardSite site) : IClassFixture<DashboardSite>
{
    private static readonly Regex Identifier = new("\\sid=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Anchor = new("<a[^>]*href=\"([^\"]*)\"", RegexOptions.Compiled);

    // A grid repeats hundreds of ids when it lands in two arms: measured with the page put back in
    // both, /table drew 434 twice and /sheet 279. Listed whole, they buried the three of /charts.
    private const int ShownPerRoute = 3;

    /// <summary>
    /// A name belongs to ONE node on the page.
    /// <para>
    /// <see cref="AdaptiveNode"/> puts EVERY declared arm in the document and hides the others with a
    /// media query, so a subtree placed in two arms carries its <see cref="VisualNode.Bookmark"/> into
    /// the page twice, and a browser resolves a fragment to the first in DOCUMENT order: the arm the
    /// media query hid, zero height, at the top, scrolling nowhere. The dashboard's shell did that to
    /// every one of its pages until #360, measured on /charts as six ids for three cards, the first
    /// copy 0px tall.
    /// </para>
    /// <para>
    /// Asserted over the whole document rather than over bookmarks alone, because a repeated id is
    /// invalid HTML whoever wrote it, and the shape that produces one is never local to the line that
    /// declared the name. Nothing else sees it: a realizer pin asserts what a node lowered to, and each
    /// copy lowers perfectly. Ported from the site's guard of the same name, which caught 29 of them
    /// the day a shared article went back into two arms.
    /// </para>
    /// </summary>
    [Fact]
    public async Task NoNameIsClaimedTwiceOnAPage()
    {
        DashboardSite.Routes.Should().NotBeEmpty("the sample declares [Page] routes, so the scan is wrong");

        var clashes = new List<string>();
        var counted = 0;

        foreach (var route in DashboardSite.Routes)
        {
            var html = await site.DocumentAsync(route);

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var match in Identifier.Matches(html).Cast<Match>())
            {
                var id = match.Groups[1].Value;
                counts[id] = counts.TryGetValue(id, out var seen) ? seen + 1 : 1;
            }

            counted += counts.Count;
            var repeated = counts.Where(entry => entry.Value > 1)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            foreach (var (id, count) in repeated.Take(ShownPerRoute))
                clashes.Add($"{route} draws id=\"{id}\" {count} times");
            if (repeated.Count > ShownPerRoute)
                clashes.Add($"{route} draws {repeated.Count - ShownPerRoute} more id(s) more than once");
        }

        counted.Should().BePositive("the pages name nodes, so something is wrong with the scrape");

        clashes.Should().BeEmpty(
            $"{counted} names were counted across {DashboardSite.Routes.Length} routes"
            + $" ({DashboardSite.Parameterised.Length} parameterised route(s) skipped):\n  "
            + string.Join("\n  ", clashes));
    }

    /// <summary>
    /// A fragment names a place, and the place exists.
    /// <para>
    /// The sibling above asks whether a name is claimed once. This asks whether a link pointing at it
    /// arrives anywhere. Neither implies the other: a duplicate passes this one twice over, and a
    /// renamed <see cref="VisualNode.Bookmark"/> passes that one while every link to it goes dead.
    /// </para>
    /// <para>
    /// A bare fragment belongs to the page it was FOUND on, which is why the scrape keeps the route
    /// beside the href.
    /// </para>
    /// </summary>
    [Fact]
    public async Task NoFragmentPointsAtAPlaceThatIsNotThere()
    {
        var wanted = new SortedSet<string>(StringComparer.Ordinal);
        var seenOn = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var route in DashboardSite.Routes)
        {
            var html = await site.DocumentAsync(route);
            foreach (var match in Anchor.Matches(html).Cast<Match>())
            {
                var href = match.Groups[1].Value;
                if (href.StartsWith("//", StringComparison.Ordinal)) continue;

                var hash = href.IndexOf('#');
                if (hash < 0 || hash == href.Length - 1) continue;

                // A bare "#x" belongs to the page it was found on.
                var page = hash == 0 ? route : href[..hash];
                if (page.Length == 0) page = "/";

                var pair = $"{page}\u0000{href[(hash + 1)..]}";
                if (wanted.Add(pair)) seenOn[pair] = route;
            }
        }

        wanted.Should().NotBeEmpty(
            "the sample draws in-page links (/charts has a Jump to row), so the scrape is wrong");

        var missing = new List<string>();
        foreach (var pair in wanted)
        {
            var parts = pair.Split('\u0000');
            var html = await site.DocumentAsync(parts[0]);
            if (!html.Contains($"id=\"{parts[1]}\"", StringComparison.Ordinal))
                missing.Add($"{parts[0]}#{parts[1]} names nothing on that page (linked from {seenOn[pair]})");
        }

        missing.Should().BeEmpty(
            $"{wanted.Count} in-page destinations were checked:\n  " + string.Join("\n  ", missing));
    }
}
