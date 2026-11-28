using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A navigation component takes its family's record as a style of its own (#1610, the catalog's decision 6):
/// the instance's <see cref="NavigationTheme"/> is laid over the theme's slot with the record's <c>Merge</c>, so
/// what it sets wins over a different value the theme has, what it leaves null is the theme's, and a sibling with
/// no style keeps the theme's. The web's half; Photon's is <c>NavigationTakesItsOwnStyleTests</c>. Read from the
/// markup and the atomic stylesheet the lowering writes.
/// </summary>
public class NavigationTakesItsOwnStyleRealizerTests
{
    private static readonly IAppTheme Photon = PhotonTheme.Instance;

    private static readonly ColorToken ThemeInk = new(Color.FromRgb(0xAB, 0x00, 0x01));
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));
    private const string ThemeCss = "#ab0001";
    private const string OwnCss = "#123456";

    private static readonly NavItem[] Destinations =
        [new(Icons.Search, "Search"), new(Icons.Mail, "Inbox"), new(Icons.Person, "Profile")];

    /// <summary>A case: the theme's slot and the instance's, the component built with a style or without, and
    /// the declaration the instance's value writes (and the theme's).</summary>
    public sealed record Case(string Name, NavigationTheme Themed, NavigationTheme Own,
        Func<NavigationTheme?, VisualNode> Make, string OwnWrites, string ThemeWrites)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Tabs", new NavigationTheme { TabHeight = 60 }, new NavigationTheme { TabHeight = 72 },
            style => new Tabs(["All", "Income"], 0, style: style), "height:72px", "height:60px"),
        new Case("AppBar", new NavigationTheme { AppBarHeight = 70 }, new NavigationTheme { AppBarHeight = 82 },
            style => new AppBar("Wallet", style), "height:82px", "height:70px"),
        new Case("BottomNavigation", new NavigationTheme { BarHeight = 70 }, new NavigationTheme { BarHeight = 84 },
            style => new BottomNavigation(Destinations, 0, style: style), "height:84px", "height:70px"),
        new Case("NavigationRail", new NavigationTheme { RailWidth = 90 }, new NavigationTheme { RailWidth = 104 },
            style => new NavigationRail(Destinations, 0, style: style), "width:104px", "width:90px"),
        new Case("Breadcrumb", new NavigationTheme { CrumbColor = ThemeInk }, new NavigationTheme { CrumbColor = Ink },
            style => new Breadcrumb([new Crumb("Home", "/"), new Crumb("Payments")], style), OwnCss, ThemeCss),
        new Case("Pagination's page cells", new NavigationTheme { PageCellSize = 37 }, new NavigationTheme { PageCellSize = 41 },
            style => new Pagination(5, 2, style: style), "width:41px", "width:37px"),
        new Case("Pagination's ellipsis", new NavigationTheme { EllipsisColor = ThemeInk }, new NavigationTheme { EllipsisColor = Ink },
            style => new Pagination(70, 35, style: style), OwnCss, ThemeCss),
        new Case("PageIndicator", new NavigationTheme { CurrentDotWidth = 21 }, new NavigationTheme { CurrentDotWidth = 27 },
            style => new PageIndicator(4, 1, style: style), "width:27px", "width:21px"),
    };

    private static string Lowered(VisualNode node, IAppTheme theme)
    {
        var sink = new StyleSink();
        var html = HtmlRenderer.RenderNode(WebRealizer.Lower(node, theme, 1f, sink).Render());
        return html + sink.Css;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AStyleTheInstanceSets_WinsOverTheThemes_AndASiblingKeepsTheThemes(Case c)
    {
        var theme = Photon.With(navigation: c.Themed);
        var styled = Lowered(c.Make(c.Own), theme);
        var plain = Lowered(c.Make(null), theme);

        styled.Should().Contain(c.OwnWrites, "the instance's own style is what it draws");
        styled.Should().NotContain(c.ThemeWrites, "and not the theme's value it replaces");
        plain.Should().Contain(c.ThemeWrites, "a sibling with no style draws the theme's");
        plain.Should().NotContain(c.OwnWrites, "and not its neighbour's");
    }

    /// <summary>The page cells and the ellipsis of a long run read the merged slot, not the theme's again: a
    /// pagination of seventy pages draws five numbers and two gaps, and no cell of the theme's size is left.</summary>
    [Fact]
    public void APaginationsStyle_ReachesEveryCell_NotOnlyTheGapsBetweenThem()
    {
        var theme = Photon.With(navigation: new NavigationTheme { PageCellSize = 37, PageGap = 5 });
        var style = new NavigationTheme { PageCellSize = 41 };

        var styled = Lowered(new Pagination(70, 35, style: style), theme);

        styled.Should().Contain("width:41px", "the page numbers' cells are the style's")
            .And.NotContain("width:37px", "no number's cell and no ellipsis' is left at the theme's size");
        styled.Should().Contain("gap:5px", "a member the style leaves null is still the theme's");
    }

    /// <summary>A rail without its trailing edge keeps the theme's other members.</summary>
    [Fact]
    public void ARailWithoutItsTrailingEdge_KeepsTheThemesOtherMembers()
    {
        var theme = Photon.With(navigation: new NavigationTheme { RailBorderWidth = 1, RailWidth = 88 });

        var styled = Lowered(new NavigationRail(Destinations, 0, style: new NavigationTheme { RailBorderWidth = 0 }), theme);
        var plain = Lowered(new NavigationRail(Destinations, 0), theme);

        styled.Should().Contain("width:88px", "the rail is still the theme's width");
        plain.Should().Contain("width:88px").And.Contain("border-inline-end", "the theme's rail has its trailing edge");
        styled.Should().NotContain("border-inline-end", "this rail draws none");
    }

    /// <summary>No style, no builder: the family writes what it wrote, so the pins stay.</summary>
    [Fact]
    public void WithNoStyle_ATabStripIsTheThemesOwn()
    {
        var theme = Photon.With(navigation: new NavigationTheme { TabHeight = 60 });

        Lowered(new Tabs(["All", "Income"], 0, style: new NavigationTheme()), theme)
            .Should().Be(Lowered(new Tabs(["All", "Income"], 0), theme), "an empty record changes nothing");
    }
}
