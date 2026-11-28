using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A tab strip, a bottom bar, a rail and a breadcrumb draw their parts with the developer's builder (#1610, the
/// catalog's decision 5): a <c>Func&lt;int, VisualNode&gt;</c> over the part's index draws its CONTENT inside the
/// component's own chrome, hands it the colour and the type the part's own text had, the part's text stays its
/// accessible name, and with no builder the output is what it was. Photon's half; the web's is
/// <c>NavigationBuildersRealizerTests</c>. The content is a swatch of a colour of its own per index, so the frame
/// says which index drew where.
/// </summary>
public class NavigationBuildersTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static readonly string[] Labels = ["All", "Income", "Refunds"];

    private static readonly NavItem[] Destinations =
        [new(Icons.Search, "Search") { BadgeCount = 3 }, new(Icons.Mail, "Inbox"), new(Icons.Person, "Profile")];

    private static readonly Crumb[] Path = [new("Home", "/"), new("Payments", "/payments"), new("Refund 42")];

    private static ColorToken Swatch(int index) => new(Color.FromRgb((byte)(0x20 + index * 0x30), 0x77, 0xC3));

    private static VisualNode Drawn(int index) =>
        new Box(new BoxStyle { Width = 14, Height = 14, Background = Swatch(index) });

    private static (PhotonHost Host, DisplayList Frame) Render(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder());
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 16);
        return (host, builder.Build());
    }

    private static bool Paints(DisplayList list, int index)
    {
        var color = Swatch(index).Resolve(ThemeMode.Light, ContrastLevel.Standard);
        return list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color);
    }

    /// <summary>With no platform text service a text draws as bars in its ink at 30%: the colour is what
    /// this reads (AForegroundDescendsTests).</summary>
    private static bool Inks(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard).WithOpacity(0.30f);
        return list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color);
    }

    /// <summary>The bars a text of the content's own makes, as rectangles: their height says the line it was
    /// set in.</summary>
    private static float[] BarHeights(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard).WithOpacity(0.30f);
        return list.Commands.ToArray().Where(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color)
            .Select(c => c.Shape.Rect.Height).Distinct().Order().ToArray();
    }

    /// <summary>How many commands are drawn in exactly this colour: a fill, or a glyph in its tint.</summary>
    private static int Count(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard);
        return list.Commands.ToArray().Count(c => c.Paint.Color == color);
    }

    private static List<SemanticNode> Of(PhotonHost host, SemanticRole role) =>
        host.Semantics().Where(node => node.Role == role).ToList();

    // -- Tabs ----------------------------------------------------------------------------------------------

    [Fact]
    public void ATabsContent_IsDrawnByTheBuilder_KeepsItsIndicator_AndItsNames()
    {
        var chosen = new List<int>();
        var (_, plainFrame) = Render(new Tabs(Labels, 1, chosen.Add));
        var (host, frame) = Render(new Tabs(Labels, 1, chosen.Add, tabBuilder: Drawn));

        Paints(frame, 0).Should().BeTrue();
        Paints(frame, 1).Should().BeTrue();
        Paints(frame, 2).Should().BeTrue();
        var tabs = Of(host, SemanticRole.Tab);
        tabs.Select(node => node.Label).Should().Equal(Labels, "the tab's text stays its accessible name");
        var indicator = Theme.Colors(Variant.Primary).Base;
        Count(plainFrame, indicator).Should().BeGreaterThan(0);
        Count(frame, indicator).Should().Be(Count(plainFrame, indicator), "the selected tab keeps its indicator");

        host.Activate(tabs[2].Id).Should().BeTrue();
        chosen.Should().Equal(2);
    }


    /// <summary>A builder's text that names no colour is drawn in the tab's: the selected tab's and the others'.</summary>
    [Fact]
    public void ABuildersTextWithNoColour_TakesTheTabsColourAndType()
    {
        var (_, frame) = Render(new Tabs(["A", "B"], 0, tabBuilder: index => new Text(index == 0 ? "A" : "B")));
        var (_, plain) = Render(new Tabs(["A", "B"], 0));

        Inks(frame, Theme.Colors(Variant.Primary).Base).Should().BeTrue("the selected tab's content");
        Inks(frame, Theme.TextMuted).Should().BeTrue("and the other's, in the quieter colour");
        BarHeights(frame, Theme.Colors(Variant.Primary).Base).Should().Equal(BarHeights(plain, Theme.Colors(Variant.Primary).Base),
            "set in the tab's own type");
        BarHeights(frame, Theme.TextMuted).Should().Equal(BarHeights(plain, Theme.TextMuted));
    }

    [Fact]
    public void WithNoBuilder_ATabStripDrawsNoContentOfTheDevelopers()
    {
        var (host, frame) = Render(new Tabs(Labels, 0, _ => { }));

        Paints(frame, 1).Should().BeFalse();
        Of(host, SemanticRole.Tab).Select(node => node.Label).Should().Equal(Labels);
    }

    // -- BottomNavigation and NavigationRail ---------------------------------------------------------------

    public static TheoryData<string> Bars => new() { "BottomNavigation", "NavigationRail" };

    private static VisualNode Bar(string kind, Action<int>? onSelect = null, bool build = true, int selected = 0,
        Func<int, VisualNode>? builder = null) => kind == "BottomNavigation"
        ? new BottomNavigation(Destinations, selected, onSelect, destinationBuilder: builder ?? (build ? Drawn : null))
        : new NavigationRail(Destinations, selected, onSelect, destinationBuilder: builder ?? (build ? Drawn : null));

    [Theory]
    [MemberData(nameof(Bars))]
    public void ADestinationsLabel_IsDrawnByTheBuilder_KeepsItsPill_AndItsName(string kind)
    {
        var chosen = new List<int>();
        var (_, plainFrame) = Render(Bar(kind, build: false));
        var (host, frame) = Render(Bar(kind, chosen.Add));

        Paints(frame, 0).Should().BeTrue();
        Paints(frame, 1).Should().BeTrue();
        Paints(frame, 2).Should().BeTrue();
        var destinations = Of(host, SemanticRole.Destination);
        destinations.Select(node => node.Label).Should().Equal(["Search", "Inbox", "Profile"], "the item's text stays the destination's name");
        var pill = Theme.Colors(Variant.Primary).Subtle;
        Count(plainFrame, pill).Should().BeGreaterThan(0);
        Count(frame, pill).Should().Be(Count(plainFrame, pill), "the selected destination keeps its pill's fill");
        var glyph = Theme.Colors(Variant.Primary).OnSubtle;
        Count(plainFrame, glyph).Should().BeGreaterThan(0);
        Count(frame, glyph).Should().Be(Count(plainFrame, glyph), "and its glyph, in the tint");

        host.Activate(destinations[1].Id).Should().BeTrue();
        chosen.Should().Equal(1);
    }

    [Theory]
    [MemberData(nameof(Bars))]
    public void ADestinationsBuiltText_TakesTheLabelsColourAndType(string kind)
    {
        var (_, built) = Render(Bar(kind, selected: 1, builder: index => new Text(Destinations[index].Label)));
        var (_, plain) = Render(Bar(kind, build: false, selected: 1));
        var tint = Theme.Colors(Variant.Primary).OnSubtle;

        Inks(built, tint).Should().BeTrue("the selected destination's label is in its tint");
        Inks(built, Theme.TextMuted).Should().BeTrue("and the others' in the muted one");
        BarHeights(built, tint).Should().Equal(BarHeights(plain, tint), "set in the label's own type");
        BarHeights(built, Theme.TextMuted).Should().Equal(BarHeights(plain, Theme.TextMuted));
    }

    [Theory]
    [MemberData(nameof(Bars))]
    public void WithNoBuilder_ADestinationBarDrawsNoContentOfTheDevelopers(string kind)
    {
        var (host, frame) = Render(Bar(kind, build: false));

        Paints(frame, 1).Should().BeFalse();
        Of(host, SemanticRole.Destination).Select(node => node.Label).Should().Equal(["Search", "Inbox", "Profile"]);
    }

    // -- Breadcrumb ----------------------------------------------------------------------------------------

    private static VisualNode Crumbs(bool build = true, Func<int, VisualNode>? builder = null) => new Breadcrumb(Path,
        crumbBuilder: builder ?? (build ? Drawn : null));

    [Fact]
    public void ACrumbsContent_IsDrawnByTheBuilder_KeepsItsSeparatorsAndLinks()
    {
        var (_, plainFrame) = Render(Crumbs(build: false));
        var (host, frame) = Render(Crumbs());

        Paints(frame, 0).Should().BeTrue();
        Paints(frame, 1).Should().BeTrue();
        Paints(frame, 2).Should().BeTrue();
        Of(host, SemanticRole.Link).Select(node => node.Label).Should()
            .Equal(["Home", "Payments"], "an ancestor is a link named by its crumb's text, and the current crumb is none");
        Count(plainFrame, Theme.BorderStrong).Should().BeGreaterThan(0);
        Count(frame, Theme.BorderStrong).Should().Be(Count(plainFrame, Theme.BorderStrong), "the separators stay between the crumbs");
    }

    [Fact]
    public void ABuiltCrumbsText_TakesTheCrumbsColour_TheCurrentOneIncluded()
    {
        var (_, frame) = Render(Crumbs(builder: index => new Text(Path[index].Label)));

        Inks(frame, Theme.TextSecondary).Should().BeTrue("an ancestor's colour");
        Inks(frame, Theme.TextPrimary).Should().BeTrue("and the current crumb's");
    }

    [Fact]
    public void WithNoBuilder_ABreadcrumbDrawsNoContentOfTheDevelopers()
    {
        var (host, frame) = Render(Crumbs(build: false));

        Paints(frame, 1).Should().BeFalse();
        Of(host, SemanticRole.Link).Select(node => node.Label).Should().Equal(["Home", "Payments"]);
    }
}
