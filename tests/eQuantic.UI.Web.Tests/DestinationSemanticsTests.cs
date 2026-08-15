using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// WHERE YOU ARE, stated rather than painted.
/// <para>
/// A navigation announced its three destinations identically — "Activity, button", "Search,
/// button", "Profile, button" — because the active one was a pill and a tint, which is nothing at
/// all to a screen reader. It is the one piece of state a navigation exists to carry, and the three
/// spellings the vocabulary already had could not say it: a destination is not PRESSED (it does not
/// toggle), not CHECKED (nothing is being chosen), and not SELECTED (a tab switches a panel; a
/// destination changes where you are).
/// </para>
/// </summary>
public class DestinationSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<HtmlNode> Destinations(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render())
            .Where(candidate => candidate.Attributes.ContainsKey("aria-label"))
            .ToList();

    private static readonly NavItem[] Items =
    [
        new(Icons.Notifications, "Activity"),
        new(Icons.Search, "Search"),
        new(Icons.Person, "Profile"),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void OnlyTheCurrentDestinationCarriesTheAttribute(int selected)
    {
        var destinations = Destinations(new BottomNavigation(Items, selected));

        destinations.Should().HaveCount(3);
        for (var index = 0; index < destinations.Count; index++)
        {
            if (index == selected)
                destinations[index].Attributes["aria-current"].Should().Be("page");
            else
                destinations[index].Attributes.Should().NotContainKey("aria-current",
                    "aria-current=\"false\" is legal and pure noise — every destination announcing "
                    + "\"not current\" is worse than silence");
        }
    }

    /// <summary>The rail is the bar's twin in this too — a wider window must not be a quieter one.</summary>
    [Fact]
    public void TheRailSaysExactlyWhatTheBarSays()
    {
        var bar = Destinations(new BottomNavigation(Items, 1))[1];
        var rail = Destinations(new NavigationRail(Items, 1))[1];

        rail.Attributes["aria-current"].Should().Be(bar.Attributes["aria-current"]);
        rail.Attributes["aria-label"].Should().Be(bar.Attributes["aria-label"]);
    }

    /// <summary>A destination keeps its Tab stop, unlike a tab or a radio: a navigation is a list of
    /// links, not a composite with one entry point, and a keyboard user expects to walk it.</summary>
    [Fact]
    public void ADestinationStaysInTheTabOrder()
    {
        var destination = Destinations(new BottomNavigation(Items, 0))[0];

        destination.Attributes.GetValueOrDefault("tabindex", "0").Should().NotBe("-1");
        destination.Attributes.Should().NotContainKey("aria-pressed",
            "a destination does not toggle, and aria-pressed would say it does");
        destination.Attributes.Should().NotContainKey("aria-selected");
    }

    /// <summary>A sidebar of LINKS is the web's own navigation idiom, and the case
    /// <c>aria-current="page"</c> was invented for. Same claim, third shape.</summary>
    [Fact]
    public void ALinkToTheCurrentPageSaysSo()
    {
        static HtmlNode Anchor(bool current) =>
            Walk(WebRealizer.Lower(
                    new Link("/payments", new Text("Payments")) { Current = current }, Theme).Render())
                .Single(node => node.Tag == "a");

        Anchor(true).Attributes["aria-current"].Should().Be("page");
        Anchor(false).Attributes.Should().NotContainKey("aria-current",
            "the other nine links in a sidebar have nothing to announce");
    }

    /// <summary>A drawer's nav row is the same fact in a different shape — and an ORDINARY list row
    /// must stay silent about it, or every list in the app grows a claim it never made.</summary>
    [Fact]
    public void AListRowIsADestinationOnlyWhenItSaysSo()
    {
        var current = Destinations(new ListItem("Activity", onPressed: () => { }) { Selected = true });
        var plain = Destinations(new ListItem("Activity", onPressed: () => { }));

        current.Should().ContainSingle().Which.Attributes["aria-current"].Should().Be("page");
        plain.Should().ContainSingle().Which.Attributes.Should().NotContainKey("aria-current");
    }
}
