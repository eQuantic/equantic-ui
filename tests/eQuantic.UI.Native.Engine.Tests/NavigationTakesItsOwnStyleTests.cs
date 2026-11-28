using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A navigation component takes its family's record as a style of its own (#1610, the catalog's decision 6):
/// the instance's <see cref="NavigationTheme"/> is laid over the theme's slot with the record's <c>Merge</c>, so
/// what it sets wins over a different value the theme has, what it leaves null is the theme's, and a sibling with
/// no style keeps the theme's. Photon's half; the web's is <c>NavigationTakesItsOwnStyleRealizerTests</c>. Read
/// from the boxes the layout placed and from what the frame paints.
/// </summary>
public class NavigationTakesItsOwnStyleTests
{
    private static readonly IAppTheme Photon = PhotonTheme.Instance;

    private static readonly ColorToken ThemeInk = new(Color.FromRgb(0xAB, 0x00, 0x01));
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));

    private static readonly NavItem[] Destinations =
        [new(Icons.Search, "Search"), new(Icons.Mail, "Inbox"), new(Icons.Person, "Profile")];

    public enum Probe { Width, Height, Cell, Ink }

    /// <summary>A case: the theme's slot and the instance's, the component built with a style or without, and
    /// what each value draws, a box of a size or text in an ink.</summary>
    public sealed record Case(string Name, NavigationTheme Themed, NavigationTheme Own,
        Func<NavigationTheme?, VisualNode> Make, Probe Reads, float ThemeSize = 0, float OwnSize = 0)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Tabs", new NavigationTheme { TabHeight = 60 }, new NavigationTheme { TabHeight = 72 },
            style => new Tabs(["All", "Income"], 0, style: style), Probe.Height, 60, 72),
        new Case("AppBar", new NavigationTheme { AppBarHeight = 70 }, new NavigationTheme { AppBarHeight = 82 },
            style => new AppBar("Wallet", style), Probe.Height, 70, 82),
        new Case("BottomNavigation", new NavigationTheme { BarHeight = 70 }, new NavigationTheme { BarHeight = 84 },
            style => new BottomNavigation(Destinations, 0, style: style), Probe.Height, 70, 84),
        new Case("NavigationRail", new NavigationTheme { RailWidth = 90 }, new NavigationTheme { RailWidth = 104 },
            style => new NavigationRail(Destinations, 0, style: style), Probe.Width, 90, 104),
        new Case("Breadcrumb", new NavigationTheme { CrumbColor = ThemeInk }, new NavigationTheme { CrumbColor = Ink },
            style => new Breadcrumb([new Crumb("Home", "/"), new Crumb("Payments")], style), Probe.Ink),
        new Case("Pagination's page cells", new NavigationTheme { PageCellSize = 37 }, new NavigationTheme { PageCellSize = 41 },
            style => new Pagination(5, 2, style: style), Probe.Cell, 37, 41),
        new Case("Pagination's ellipsis", new NavigationTheme { EllipsisColor = ThemeInk }, new NavigationTheme { EllipsisColor = Ink },
            style => new Pagination(70, 35, style: style), Probe.Ink),
        new Case("PageIndicator", new NavigationTheme { CurrentDotWidth = 21 }, new NavigationTheme { CurrentDotWidth = 27 },
            style => new PageIndicator(4, 1, style: style), Probe.Width, 21, 27),
    };

    private static (List<Rect> Boxes, DisplayList Frame) Draw(VisualNode node, IAppTheme theme)
    {
        var builder = new DisplayListBuilder();
        var result = PhotonRealizer.Realize(node, 400, 800, theme, ThemeMode.Light, ContrastLevel.Standard, builder);
        var boxes = new List<Rect>();
        Walk(result.Root, boxes);
        return (boxes, builder.Build());
    }

    private static void Walk(eQuantic.UI.Native.Framework.LayoutNode node, List<Rect> found)
    {
        found.Add(node.Bounds);
        foreach (var child in node.Children) Walk(child, found);
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    /// <summary>With no platform text service a text draws as bars in its ink at 30%: the colour is what
    /// this reads (AForegroundDescendsTests).</summary>
    private static bool Inks(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard).WithOpacity(0.30f);
        return list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color);
    }

    private static bool Marks(Case c, (List<Rect> Boxes, DisplayList Frame) drawn, bool own) => c.Reads switch
    {
        Probe.Width => drawn.Boxes.Any(r => Near(r.Width, own ? c.OwnSize : c.ThemeSize)),
        Probe.Height => drawn.Boxes.Any(r => Near(r.Height, own ? c.OwnSize : c.ThemeSize)),
        Probe.Cell => drawn.Boxes.Any(r => Near(r.Width, own ? c.OwnSize : c.ThemeSize)
            && Near(r.Height, own ? c.OwnSize : c.ThemeSize)),
        _ => Inks(drawn.Frame, own ? Ink : ThemeInk),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AStyleTheInstanceSets_WinsOverTheThemes_AndASiblingKeepsTheThemes(Case c)
    {
        var theme = Photon.With(navigation: c.Themed);
        var styled = Draw(c.Make(c.Own), theme);
        var plain = Draw(c.Make(null), theme);

        Marks(c, styled, own: true).Should().BeTrue("the instance's own style is what it draws");
        Marks(c, styled, own: false).Should().BeFalse("and not the theme's value it replaces");
        Marks(c, plain, own: false).Should().BeTrue("a sibling with no style draws the theme's");
        Marks(c, plain, own: true).Should().BeFalse("and not its neighbour's");
    }

    /// <summary>The page cells and the ellipsis of a long run read the merged slot, not the theme's again: a
    /// pagination of seventy pages draws five numbers and two gaps, and no cell of the theme's size is left.</summary>
    [Fact]
    public void APaginationsStyle_ReachesEveryCell_NotOnlyTheGapsBetweenThem()
    {
        var theme = Photon.With(navigation: new NavigationTheme { PageCellSize = 37, PageGap = 5 });
        var (boxes, _) = Draw(new Pagination(70, 35, style: new NavigationTheme { PageCellSize = 41 }), theme);

        bool Cell(Rect r, float size) => Near(r.Width, size) && Near(r.Height, size);
        boxes.Count(r => Cell(r, 41)).Should().BeGreaterThanOrEqualTo(7, "five numbers and two ellipses take the style's cell");
        boxes.Any(r => Cell(r, 37)).Should().BeFalse("no number's cell and no ellipsis' is left at the theme's size");
    }

    /// <summary>A rail without its trailing edge keeps the theme's other members.</summary>
    [Fact]
    public void ARailWithoutItsTrailingEdge_KeepsTheThemesOtherMembers()
    {
        var theme = Photon.With(navigation: new NavigationTheme { RailBorderWidth = 1, RailWidth = 88 });

        var styled = Draw(new NavigationRail(Destinations, 0, style: new NavigationTheme { RailBorderWidth = 0 }), theme);
        var plain = Draw(new NavigationRail(Destinations, 0), theme);

        styled.Boxes.Any(r => Near(r.Width, 88)).Should().BeTrue("the rail is still the theme's width");
        TrailingEdge(new NavigationRail(Destinations, 0), theme).Should().Be(1, "the theme's rail draws its trailing edge");
        TrailingEdge(new NavigationRail(Destinations, 0, style: new NavigationTheme { RailBorderWidth = 0 }), theme)
            .Should().Be(0, "and this one none");
        plain.Boxes.Any(r => Near(r.Width, 88)).Should().BeTrue();
    }

    /// <summary>The width of the border the rail's box draws on its trailing edge.</summary>
    private static float TrailingEdge(VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 400, 800, theme, ThemeMode.Light, ContrastLevel.Standard, new DisplayListBuilder());
        return Find(result.Root)!.Style.BorderWidth;

        static Box? Find(eQuantic.UI.Native.Framework.LayoutNode layout)
        {
            if (layout.Source is Box { Style.BorderSides: BorderSides.End } box) return box;
            foreach (var child in layout.Children)
                if (Find(child) is { } found) return found;
            return null;
        }
    }
}
