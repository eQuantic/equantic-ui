using System.Text.RegularExpressions;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A tab strip, a bottom bar, a rail and a breadcrumb draw their parts with the developer's builder (#1610, the
/// catalog's decision 5): a <c>Func&lt;int, VisualNode&gt;</c> over the part's index draws its CONTENT inside the
/// component's own chrome, hands it the colour and the type the part's own text had, the part's text stays its
/// accessible name, and with no builder the output is what it was. The web's half; Photon's is
/// <c>NavigationBuildersTests</c>.
/// </summary>
public class NavigationBuildersRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static readonly string[] Labels = ["All", "Income", "Refunds"];

    private static readonly NavItem[] Destinations =
        [new(Icons.Search, "Search") { BadgeCount = 3 }, new(Icons.Mail, "Inbox"), new(Icons.Person, "Profile")];

    private static readonly Crumb[] Path = [new("Home", "/"), new("Payments", "/payments"), new("Refund 42")];

    /// <summary>Content that names no colour and no type of its own, so what it draws in is what the part hands it.</summary>
    private static VisualNode Drawn(int index) => new Text("drawn-" + index);

    private static ComponentTester Pumped(VisualNode node)
    {
        var tester = new ComponentTester(Theme);
        tester.Pump(node);
        return tester;
    }

    /// <summary>The value of <paramref name="property"/> in the atomic rule nearest above the element that writes
    /// <paramref name="text"/>, itself included: what a text that names none is drawn in.</summary>
    private static string? RuleOf(ComponentTester tester, string text, string property)
    {
        var rules = Regex.Matches(tester.Css, @"\.(eq-[a-z0-9]+)\{" + property + @":([^}]*)\}")
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var path = new List<HtmlNode>();
        if (!Find(tester.Root, text, path)) throw new InvalidOperationException($"Nothing writes '{text}'.");
        for (var at = path.Count - 1; at >= 0; at--)
            foreach (var name in (path[at].Attributes.GetValueOrDefault("class") ?? "").Split(' '))
                if (rules.TryGetValue(name, out var value)) return value;
        return null;

        static bool Find(HtmlNode node, string text, List<HtmlNode> path)
        {
            path.Add(node);
            if (node.TextContent == text || node.Children.Any(child => child.Tag == "#text" && child.TextContent == text))
                return true;
            foreach (var child in node.Children)
                if (Find(child, text, path)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    /// <summary>What the text is drawn in: the colour it takes (its own or the nearest above) and every other
    /// declaration its own classes write, the type role's and the line limit's among them.</summary>
    private static string[] LookOf(ComponentTester tester, string text)
    {
        var declarations = Regex.Matches(tester.Css, @"\.(eq-[a-z0-9]+)\{([^}]*)\}")
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var own = new List<string>();
        var owner = Owner(tester.Root, text)!;
        foreach (var name in (owner.Attributes.GetValueOrDefault("class") ?? "").Split(' '))
            if (declarations.TryGetValue(name, out var declaration) && !declaration.StartsWith("color:", StringComparison.Ordinal))
                own.Add(declaration);
        own.Sort(StringComparer.Ordinal);
        return [RuleOf(tester, text, "color") ?? "(none)", .. own];

        static HtmlNode? Owner(HtmlNode node, string text) =>
            node.Children.Any(child => child.Tag == "#text" && child.TextContent == text)
                ? node
                : node.Children.Select(child => Owner(child, text)).FirstOrDefault(found => found is not null);
    }

    private static int Count(string html, string what) => Regex.Matches(html, Regex.Escape(what)).Count;

    // -- Tabs ----------------------------------------------------------------------------------------------

    [Fact]
    public void ATabsContent_IsDrawnByTheBuilder_KeepsItsIndicator_AndItsNames()
    {
        var chosen = new List<int>();
        using var plain = Pumped(new Tabs(Labels, 1, chosen.Add));
        using var built = Pumped(new Tabs(Labels, 1, chosen.Add, tabBuilder: Drawn));

        built.Html.Should().Contain("drawn-0").And.Contain("drawn-1").And.Contain("drawn-2");
        built.Html.Should().NotContain(">Income<", "the builder draws the content in the label's place");
        built.Semantics.Where(stop => stop.Role == SemanticRole.Tab).Select(stop => stop.Name)
            .Should().Equal(Labels, "the tab's text stays its accessible name");
        plain.Css.Should().Contain("height:3px", "the strip has an indicator");
        built.Css.Should().Contain("height:3px", "and a builder leaves it");

        built.Tap(SemanticRole.Tab, "Refunds");
        chosen.Should().Equal(2);
    }

    [Fact]
    public void ABuildersText_TakesTheTabsColourAndType_SelectedOrNot()
    {
        using var plain = Pumped(new Tabs(Labels, 1));
        using var built = Pumped(new Tabs(Labels, 1, tabBuilder: Drawn));

        LookOf(plain, "Income").Length.Should().BeGreaterThan(1, "the tab's text writes its type");
        LookOf(built, "drawn-1").Should().Equal(LookOf(plain, "Income"), "the selected tab's colour and weight");
        LookOf(built, "drawn-0").Should().Equal(LookOf(plain, "All"), "and an inactive tab's");
        LookOf(built, "drawn-1").Should().NotEqual(LookOf(built, "drawn-0"), "the two are not the same look");
        RuleOf(built, "drawn-0", "color").Should().NotBeNull();
    }

    [Fact]
    public void WithNoBuilder_ATabStripDrawsItsLabelsAsText()
    {
        using var tester = Pumped(new Tabs(Labels, 0));

        tester.Html.Should().Contain("Income").And.NotContain("drawn-");
    }

    // -- BottomNavigation and NavigationRail ---------------------------------------------------------------

    public static TheoryData<string> Bars => new() { "BottomNavigation", "NavigationRail" };

    private static VisualNode Bar(string kind, Action<int>? onSelect = null, bool build = true, int selected = 0) => kind == "BottomNavigation"
        ? new BottomNavigation(Destinations, selected, onSelect, destinationBuilder: build ? Drawn : null)
        : new NavigationRail(Destinations, selected, onSelect, destinationBuilder: build ? Drawn : null);

    [Theory]
    [MemberData(nameof(Bars))]
    public void ADestinationsLabel_IsDrawnByTheBuilder_KeepsItsPill_AndItsName(string kind)
    {
        var chosen = new List<int>();
        using var plain = Pumped(Bar(kind, build: false));
        using var built = Pumped(Bar(kind, chosen.Add));

        built.Html.Should().Contain("drawn-0").And.Contain("drawn-1").And.Contain("drawn-2");
        built.Html.Should().NotContain(">Inbox<", "the builder draws the label's place");
        // A destination is a button on the web that says where you are with aria-current.
        built.Semantics.Where(stop => stop.Role == SemanticRole.Button).Select(stop => stop.Name)
            .Should().Equal(new[] { "Search", "Inbox", "Profile" }, "the item's text stays the destination's name");
        Count(built.Html, "<svg").Should().Be(Count(plain.Html, "<svg"), "the pill keeps each glyph, and the first its badge's");
        built.Html.Should().Contain("aria-current=\"page\"", "the selected destination still says where you are");
        built.Html.Should().Contain(">3<", "the badge stays on the first destination");

        built.Tap(SemanticRole.Button, "Inbox");
        chosen.Should().Equal(1);
    }

    [Theory]
    [MemberData(nameof(Bars))]
    public void ADestinationsBuiltLabel_TakesTheLabelsColourAndType(string kind)
    {
        using var plain = Pumped(Bar(kind, build: false, selected: 1));
        using var built = Pumped(Bar(kind, selected: 1));

        LookOf(built, "drawn-1").Should().Equal(LookOf(plain, "Inbox"), "the selected destination's tint and type");
        LookOf(built, "drawn-2").Should().Equal(LookOf(plain, "Profile"), "and another's");
        LookOf(built, "drawn-1").Should().NotEqual(LookOf(built, "drawn-2"));
    }

    [Theory]
    [MemberData(nameof(Bars))]
    public void WithNoBuilder_ADestinationBarDrawsItsLabelsAsText(string kind)
    {
        using var tester = Pumped(Bar(kind, build: false));

        tester.Html.Should().Contain("Inbox").And.NotContain("drawn-");
    }

    // -- Breadcrumb ----------------------------------------------------------------------------------------

    private static VisualNode Crumbs(bool build = true) => new Breadcrumb(Path,
        crumbBuilder: build ? index => index == 0 ? new Icon(Icons.Search, label: "Home") : Drawn(index) : null);

    [Fact]
    public void ACrumbsContent_IsDrawnByTheBuilder_KeepsItsSeparatorsAndLinks()
    {
        using var plain = Pumped(Crumbs(build: false));
        using var built = Pumped(Crumbs());

        built.Html.Should().Contain("drawn-1").And.Contain("drawn-2").And.NotContain("drawn-0");
        built.Html.Should().NotContain(">Payments<", "the builder draws the crumb");
        built.Semantics.Where(stop => stop.Role == SemanticRole.Link).Select(stop => stop.Name)
            .Should().Equal(new[] { "Home", "Payments" }, "an ancestor is a link named by its crumb's text, and the current crumb is none");
        built.Html.Should().Contain("href=\"/\"").And.Contain("href=\"/payments\"");
        Count(built.Html, "<svg").Should().Be(Count(plain.Html, "<svg") + 1, "the two separators stay, and the first crumb's icon is a third");
    }

    [Fact]
    public void ABuiltCrumbsText_TakesTheCrumbsColour_TheCurrentOneIncluded()
    {
        using var plain = Pumped(Crumbs(build: false));
        using var built = Pumped(Crumbs());

        LookOf(plain, "Payments").Length.Should().BeGreaterThan(1, "the crumb's text writes its type");
        LookOf(built, "drawn-1").Should().Equal(LookOf(plain, "Payments"), "an ancestor's colour and type");
        LookOf(built, "drawn-2").Should().Equal(LookOf(plain, "Refund 42"), "and the current crumb's");
        LookOf(built, "drawn-1").Should().NotEqual(LookOf(built, "drawn-2"));
    }

    [Fact]
    public void WithNoBuilder_ABreadcrumbDrawsItsCrumbsAsText()
    {
        using var tester = Pumped(Crumbs(build: false));

        tester.Html.Should().Contain("Payments").And.Contain("Refund 42").And.NotContain("drawn-");
    }
}
