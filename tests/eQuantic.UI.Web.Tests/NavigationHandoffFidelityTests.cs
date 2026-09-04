using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// B4 BottomNavigation and C16 NavigationRail, held against the handoff — and they are NOT the same
/// numbers.
/// <para>
/// The rail was built from the bar, so it WAS the bar stood on its side: a 56×26 pill, a Md 24 glyph
/// and an 11/700 label, where C16 asks for a 52×30 pill, Dense 20 and a Caption 12 that goes to 700
/// only when selected. Same mistake as the List — reading a sibling's spec for a component that has
/// one of its own. The destination MODEL is deliberately shared (same NavItem list, same selection
/// vocabulary); the metrics are not.
/// </para>
/// </summary>
public class NavigationHandoffFidelityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<string> Styles(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render())
            .Select(candidate => candidate.Attributes.GetValueOrDefault("style", ""))
            .ToList();

    private static readonly NavItem[] Items =
    [
        new(Icons.Notifications, "Activity"),
        new(Icons.Search, "Search"),
        new(Icons.Person, "Profile"),
    ];

    /// <summary>C16: the pill is 52×30, and the cell it sits in is 80×56 with the whole cell as the
    /// hit target.</summary>
    [Fact]
    public void TheRailsPillAndCellAreItsOwn()
    {
        var styles = Styles(new NavigationRail(Items, 0));

        styles.Should().Contain(style => style.Contains("width: 52px") && style.Contains("height: 30px"),
            "C16's pill, not the bar's 56×26");
        styles.Should().Contain(style => style.Contains("height: 56px"), "the 80×56 cell");
        styles.Should().Contain(style => style.Contains("width: 80px"), "the rail itself");
    }

    /// <summary>The bar keeps ITS figures — fixing the rail must not drag the bar along.</summary>
    [Fact]
    public void TheBarKeepsTheBarsPill()
    {
        var styles = Styles(new BottomNavigation(Items, 0));

        styles.Should().Contain(style => style.Contains("width: 56px") && style.Contains("height: 26px"));
        styles.Should().Contain(style => style.Contains("height: 56px"), "B4's bar is 56 tall");
    }

    /// <summary>
    /// C16: a 1dp Border on the TRAILING edge. Without it a rail on a Surface page has no edge at
    /// all, which is why the sample and both template shells each drew their own — three apps
    /// working around one missing line.
    /// </summary>
    [Fact]
    public void TheRailPaintsItsOwnTrailingEdge()
    {
        var styles = Styles(new NavigationRail(Items, 0));

        // Per-side, so the shorthand carries the zeros: top, END, bottom, start.
        styles.Should().Contain(style => style.Contains("border-width: 0 1px 0 0")
            && style.Contains("width: 80px"));
    }

    /// <summary>C16: selection is a WEIGHT as well as a tint — the label goes to 700. A tint alone is
    /// the kind of difference that vanishes for anyone with low contrast vision.</summary>
    [Fact]
    public void TheSelectedLabelIsHeavier()
    {
        var selected = Styles(new NavigationRail(Items, 0));
        var others = Styles(new NavigationRail(Items, 2));

        selected.Count(style => style.Contains("font-weight: 700")).Should()
            .BeGreaterThan(0, "the selected destination's label is bold");
        // And exactly one of them is: the rule is per-destination, not per-rail.
        selected.Count(style => style.Contains("font-weight: 700")).Should()
            .Be(others.Count(style => style.Contains("font-weight: 700")));
    }
}
