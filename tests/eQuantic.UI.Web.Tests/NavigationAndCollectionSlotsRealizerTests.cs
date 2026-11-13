using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's half of the navigation and collection slots (E6d, #1244; <c>docs/ELEMENT-TREE-PLAN.md</c>
/// §3.10): a value a theme puts in a family's slot reaches the web's lowering of a component of that
/// family, Material 3's specifications reach it through <see cref="MaterialTheme"/>, and a Markdown reads
/// its own style ahead of the theme's slot, member by member. Read from the atomic stylesheet the
/// lowering writes.
/// </summary>
public class NavigationAndCollectionSlotsRealizerTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>One component per family, a theme whose slot names one value, and the declarations that
    /// value writes.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, string[] Declarations)
    {
        public override string ToString() => Family;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Navigation", () => new Tabs(["All", "Income"], 0),
            Photon.With(navigation: new NavigationTheme { TabHeight = 60 }), ["height:60px"]),
        new Case("List", () => new ListItem("Ana", onPressed: () => { }),
            Photon.With(list: new ListTheme { ItemHeight = 70 }), ["min-height:70px"]),
        new Case("Table", () => new DataTable([new DataColumn("Name", GridTrack.Flex())],
                [new DataRow("a", [new Text("Ana")])]),
            Photon.With(table: new TableTheme { DataRowHeight = 61 }), ["min-height:61px"]),
        new Case("Badge", () => new Badge(3) { Dot = true },
            Photon.With(badge: new BadgeTheme { DotSize = 14 }), ["width:14px", "height:14px"]),
        new Case("Calendar", () => new Calendar(new DateOnly(2026, 11, 11)),
            Photon.With(calendar: new CalendarTheme { CellSize = 50 }), ["width:50px", "height:50px"]),
        new Case("Markdown", () => new Markdown("| a |\n|---|\n| b |"),
            Photon.With(markdown: new MarkdownTheme { TableRadius = 17 }), ["border-radius:17px"]),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesTheWebsLowering(Case c)
    {
        var slotted = Css(c.Build(), c.Theme);
        var plain = Css(c.Build(), Photon);
        foreach (var declaration in c.Declarations)
        {
            slotted.Should().Contain(declaration, $"the {c.Family} slot writes {declaration}");
            plain.Should().NotContain(declaration, $"no {c.Family} box writes {declaration} under Photon");
        }
    }

    /// <summary>A Markdown's own <c>Style</c> is the first step of the order, member by member: what it
    /// names wins over the theme's slot, and what it leaves null is the slot's.</summary>
    [Fact]
    public void AMarkdownsOwnStyle_ComesAheadOfTheSlot_MemberByMember()
    {
        const string source = "# One\n\n| a |\n|---|\n| b |";
        var theme = Photon.With(markdown: new MarkdownTheme { BlockGap = 23, TableRadius = 17 });

        var own = Css(new Markdown(source) { Style = new MarkdownTheme { BlockGap = 31 } }, theme);
        own.Should().Contain("gap:31px", "the document's own gap wins over the slot's");
        own.Should().NotContain("gap:23px");
        own.Should().Contain("border-radius:17px", "a member the document leaves null is the slot's");

        Css(new Markdown(source), theme).Should().Contain("gap:23px", "with no style of its own, the slot answers");
    }

    /// <summary>Material 3's specifications, read from the declarations a component of each family lowers
    /// to under <see cref="MaterialTheme"/>.</summary>
    [Fact]
    public void MaterialTheme_LowersNavigationAndCollections_AtMaterial3sSizes()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void Expect(string what, VisualNode node, params string[] declarations)
        {
            var css = Css(node, material);
            foreach (var declaration in declarations)
                if (!css.Contains(declaration, StringComparison.Ordinal)) missing.Add($"{what}: {declaration}");
        }

        Expect("the navigation bar", new BottomNavigation([new NavItem(Icons.Search, "Search"), new NavItem(Icons.Mail, "Inbox"), new NavItem(Icons.Person, "Profile")], 0),
            "height:80px", "width:64px", "height:32px");
        Expect("the top app bar", new AppBar("Wallet"), "height:64px");
        Expect("the one-line list item", new ListItem("Ana", onPressed: () => { }), "min-height:56px");
        Expect("the small badge", new Badge(3) { Dot = true }, "width:6px", "height:6px");
        Expect("the date picker's day", new Calendar(new DateOnly(2026, 11, 11)), "width:40px", "height:40px");

        missing.Should().BeEmpty("MaterialTheme fills the navigation and collection slots from Material 3's specifications");
    }

    private static string Css(VisualNode node, IAppTheme theme)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, theme, 1f, sink, Density.Comfortable).Render();
        return sink.Css;
    }
}
