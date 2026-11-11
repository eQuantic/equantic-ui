using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of the navigation and collection slots (E6d, #1244; <c>docs/ELEMENT-TREE-PLAN.md</c>
/// §3.10): a value a theme puts in a family's slot reaches the layout of a component of that family, and
/// Material 3's specifications reach it through <see cref="MaterialTheme"/>. Measured by the boxes the
/// layout placed.
/// </summary>
public class NavigationAndCollectionSlotsTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>One component per family, a theme whose slot names one value, and the box that value
    /// draws: a width and a height, or a height alone.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, float? Width, float Height)
    {
        public override string ToString() => Family;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Navigation", () => new Tabs(["All", "Income"], 0),
            Photon.With(navigation: new NavigationTheme { TabHeight = 60 }), null, 60),
        new Case("List", () => new ListItem("Ana", onPressed: () => { }),
            Photon.With(list: new ListTheme { ItemHeight = 70 }), null, 70),
        new Case("Table", () => new DataTable([new DataColumn("Name", GridTrack.Flex())],
                [new DataRow("a", [new Text("Ana")])]),
            Photon.With(table: new TableTheme { DataRowHeight = 61 }), null, 61),
        new Case("Badge", () => new Badge(3) { Dot = true },
            Photon.With(badge: new BadgeTheme { DotSize = 14 }), 14, 14),
        new Case("Calendar", () => new Calendar(new DateOnly(2026, 11, 11)),
            Photon.With(calendar: new CalendarTheme { CellSize = 50 }), 50, 50),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesPhotonsLayout(Case c)
    {
        bool Draws(Rect r) => Near(r.Height, c.Height) && (c.Width is not { } w || Near(r.Width, w));

        Boxes(c.Build(), c.Theme).Any(Draws).Should().BeTrue($"the {c.Family} slot names that box");
        Boxes(c.Build(), Photon).Any(Draws).Should().BeFalse($"no {c.Family} box is that size under Photon");
    }

    /// <summary>The document's slot reaches Photon's layout too: a wider gap between blocks moves the
    /// second block down by the difference, and the document's own style wins over the slot.</summary>
    [Fact]
    public void TheDocumentsGap_MovesItsBlocks_AndItsOwnStyleWins()
    {
        const string source = "# One\n\n# Two";
        var theme = Photon.With(markdown: new MarkdownTheme { BlockGap = 30 });

        var plain = TopOf("Two", new Markdown(source), Photon);
        TopOf("Two", new Markdown(source), theme).Should().BeApproximately(plain + 16, 0.5f, "30 is 16 more than 14");
        TopOf("Two", new Markdown(source) { Style = new MarkdownTheme { BlockGap = 14 } }, theme)
            .Should().BeApproximately(plain, 0.5f, "the document's own gap comes ahead of the slot's");
    }

    /// <summary>Material 3's specifications, read through the boxes a component of each family lays out
    /// under <see cref="MaterialTheme"/>.</summary>
    [Fact]
    public void MaterialTheme_LaysOutNavigationAndCollections_AtMaterial3sSizes()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void Expect(string what, VisualNode node, float? width, float height)
        {
            if (!Boxes(node, material).Any(r => Near(r.Height, height) && (width is not { } w || Near(r.Width, w))))
                missing.Add($"{what} {width?.ToString() ?? "any"}x{height}");
        }

        Expect("the navigation bar", new BottomNavigation([new NavItem(Icons.Search, "Search"), new NavItem(Icons.Mail, "Inbox"), new NavItem(Icons.Person, "Profile")], 0), null, 80);
        Expect("the navigation bar's indicator", new BottomNavigation([new NavItem(Icons.Search, "Search"), new NavItem(Icons.Mail, "Inbox"), new NavItem(Icons.Person, "Profile")], 0), 64, 32);
        Expect("the top app bar", new AppBar("Wallet"), null, 64);
        Expect("the one-line list item", new ListItem("Ana", onPressed: () => { }), null, 56);
        Expect("the small badge", new Badge(3) { Dot = true }, 6, 6);
        Expect("the date picker's day", new Calendar(new DateOnly(2026, 11, 11)), 40, 40);

        missing.Should().BeEmpty("MaterialTheme fills the navigation and collection slots from Material 3's specifications");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    private static float TopOf(string content, VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 400, 800, theme, ThemeMode.Light, new DisplayListBuilder());
        return Find(result.Root, n => n.Source is Text { Content: var text } && text == content)!.Bounds.Y;
    }

    private static LayoutNode? Find(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
            if (Find(child, match) is { } found) return found;
        return null;
    }

    private static List<Rect> Boxes(VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 400, 800, theme, ThemeMode.Light, new DisplayListBuilder());
        var found = new List<Rect>();
        Walk(result.Root, found);
        return found;
    }

    private static void Walk(LayoutNode node, List<Rect> found)
    {
        found.Add(node.Bounds);
        foreach (var child in node.Children) Walk(child, found);
    }
}
