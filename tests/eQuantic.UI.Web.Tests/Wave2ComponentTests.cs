using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Switch = eQuantic.UI.Components.Switch;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>Wave-2 write-once components — spec metrics (A13/B2/B5/B11/B12/B13/B16/B17) as CI pins.</summary>
public class Wave2ComponentTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void IconButton_SizesAndSelectedGlyphSwap()
    {
        var node = Render(new IconButton(new Icon(Icons.Heart), "Favorite", IconButtonKind.Filled, SizeVariant.Large, () => { }));
        node.Tag.Should().Be("button");
        node.Attributes["aria-label"].Should().Be("Favorite");
        node.Children[0].Attributes["style"].Should().Contain("width: 48px");

        var selected = Render(new IconButton(new Icon(Icons.Heart), "Favorite", onPressed: () => { })
        {
            Selected = true,
            SelectedGlyph = new Icon(Icons.HeartFilled),
        });
        selected.Children[0].Children[0].Children[0].Children[0].Attributes["d"]
            .Should().Be(IconRegistry.Path(Icons.HeartFilled), "selected swaps outline → filled glyph");
    }

    [Fact]
    public void Checkbox_CheckedFill_AndErrorBorder()
    {
        var isChecked = Render(new Checkbox(true, () => { }, "Agree"));
        isChecked.Tag.Should().Be("button", "the whole row is the target");
        var box = isChecked.Children[0].Children[0];
        box.Attributes["style"].Should().Contain("width: 22px");
        box.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Colors(Variant.Primary).Base)}");

        var error = Render(new Checkbox(false, () => { }) { Error = true });
        error.Children[0].Children[0].Attributes["style"]
            .Should().Contain($"border: 2px solid {TokenCss.Value(Theme.Colors(Variant.Destructive).Base)}");
    }

    [Fact]
    public void Switch_TrackAndThumbPosition()
    {
        var on = Render(new Switch(true, () => { }));
        var stack = on.Children[0];
        stack.Children[0].Children[0].Attributes["style"].Should().Contain("width: 52px");
        stack.Children[1].Attributes["style"].Should().Contain("right: 3px", "on anchors the thumb at the end");

        Render(new Switch(false, () => { })).Children[0].Children[1]
            .Attributes["style"].Should().Contain("left: 3px");
    }

    [Fact]
    public void Button_HoverIsTheDerivedMidpoint_BehindThePointerGate()
    {
        // §10 / A12 Desktop & pointer: filled = Base→Pressed midpoint · quiet = SurfaceSubtle ·
        // Link = underline only (fenced) · inert buttons hover nothing.
        var sink = new StyleSink();
        WebRealizer.Lower(new Button("Continue"), Theme, 1f, sink).Render();
        var midpoint = TokenCss.Value(Theme.Colors(Variant.Primary).Hover);
        sink.Css.Should().Contain($":hover{{background-color:{midpoint}}}");
        sink.Css.Should().Contain("@media (hover: hover){.", "hover never fires on touch");

        var outline = new StyleSink();
        WebRealizer.Lower(new Button("Continue", Variant.Outline), Theme, 1f, outline).Render();
        outline.Css.Should().Contain(":hover{background-color:var(--eq-color-surface-subtle");

        var link = new StyleSink();
        WebRealizer.Lower(new Button("Learn more", Variant.Link), Theme, 1f, link).Render();
        link.Css.Should().NotContain(":hover");

        var disabled = new StyleSink();
        WebRealizer.Lower(new Button("Continue") { Disabled = true }, Theme, 1f, disabled).Render();
        disabled.Css.Should().NotContain(":hover");
    }

    [Fact]
    public void RadioGroup_SelectedRingAndDot()
    {
        // The group now wraps in an Adjustable (role=radiogroup, ONE Tab stop) — the selected
        // row is the radio stating aria-checked=true, wherever the wrapper puts it.
        var node = Render(new RadioGroup(["Weekly", "Monthly"], selected: 1, _ => { }));
        HtmlNode? selectedRadio = null;
        void Find(HtmlNode n)
        {
            if (n.Attributes.GetValueOrDefault("aria-checked") == "true") selectedRadio = n;
            foreach (var child in n.Children) Find(child);
        }
        Find(node);
        var circle = selectedRadio!.Children[0].Children[0];
        circle.Attributes["style"].Should().Contain($"border: 2px solid {TokenCss.Value(Theme.Colors(Variant.Primary).Base)}");
        circle.Children[0].Children[0].Attributes["style"].Should().Contain("width: 10px");
    }

    [Fact]
    public void ListItem_MinHeights_And_ListOwnsDividers()
    {
        var list = Render(new List(
        [
            new ListItem("Wi-Fi", "Photon-5G"),
            new ListItem("About"),
        ]));
        list.Children.Should().HaveCount(3, "two items + one leading-inset divider");
        list.Children[0].Attributes["style"].Should().Contain("min-height: 68px", "2-line item");
        list.Children[2].Attributes["style"].Should().Contain("min-height: 52px", "1-line item");
    }

    [Fact]
    public void Tabs_ActiveIndicatorAndWeights()
    {
        // The strip wraps in an Adjustable (role=tablist, ONE Tab stop); the row sits inside it.
        var host = Render(new Tabs(["A", "B"], selected: 0, _ => { }));
        host.Attributes["role"].Should().Be("tablist");
        var node = host.Children[0];
        node.Attributes["style"].Should().Contain("height: 48px");
        var activeCell = node.Children[0];
        activeCell.Attributes["style"].Should().Contain("flex: 1 1 0%", "fixed mode = equal width");
    }

    [Fact]
    public void EmptyState_WellTitleAndAction()
    {
        var node = Render(new EmptyState(new Icon(Icons.Search), "No results", "Try a broader term.")
        {
            Action = new Button("Clear filters"),
        });
        var well = node.Children[0];
        well.Attributes["style"].Should().Contain("width: 64px");
        well.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
    }

    [Fact]
    public void Skeleton_ShapesFollowTheSpec()
    {
        Render(new Skeleton(SkeletonShape.Line, 120)).Attributes["style"].Should().Contain("height: 12px");
        Render(new Skeleton(SkeletonShape.Circle, 40)).Attributes["style"].Should().Contain("border-radius: 999px");
        Render(new Skeleton(SkeletonShape.Block, 96, 64)).Attributes["style"].Should().Contain("border-radius: 10px");
    }

    [Fact]
    public void AppBar_TitleActionsAndLimits()
    {
        var node = Render(new AppBar("Portfolio")
        {
            Actions = [new IconButton(new Icon(Icons.Search), "Search"), new IconButton(new Icon(Icons.Mail), "Mail")],
        });
        node.Attributes["style"].Should().Contain("height: 56px");
        node.Attributes["style"].Should().Contain("padding: 0 4px 0 4px");

        var overflow = () => Render(new AppBar("X")
        {
            Actions =
            [
                new IconButton(new Icon(Icons.Search), "1"), new IconButton(new Icon(Icons.Mail), "2"),
                new IconButton(new Icon(Icons.Close), "3"), new IconButton(new Icon(Icons.Info), "4"),
            ],
        });
        overflow.Should().Throw<ArgumentException>("max 3 actions per spec B3");
    }

    [Fact]
    public void BottomNavigation_ActivePill_AndDestinationLimits()
    {
        var node = Render(new BottomNavigation(
        [
            new NavItem(Icons.Person, "Home"),
            new NavItem(Icons.Mail, "Cards") { BadgeCount = 2 },
            new NavItem(Icons.Search, "Search"),
        ], selected: 0, _ => { }));

        node.Attributes["style"].Should().Contain("height: 56px");
        node.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Surface)}");
        var activeColumn = node.Children[0].Children[0].Children[0];
        activeColumn.Tag.Should().Be("button", "the whole column is the target");

        var tooFew = () => Render(new BottomNavigation([new NavItem(Icons.Person, "A"), new NavItem(Icons.Mail, "B")], 0));
        tooFew.Should().Throw<ArgumentException>("2 destinations → Tabs per spec B4");
    }

    /// <summary>
    /// The rail is the bar stood on its side, and the web has to agree with Photon about that: 80dp
    /// of leading edge, the same destination list, the same pill. The header slot sits ABOVE the
    /// destinations rather than among them, which is what keeps a FAB from counting as one.
    /// </summary>
    [Fact]
    public void NavigationRail_IsTheBarOnItsSide_AndTakesTwoMoreDestinations()
    {
        NavItem[] four =
        [
            new(Icons.Person, "Home"),
            new(Icons.Mail, "Cards") { BadgeCount = 2 },
            new(Icons.Search, "Transfer"),
            new(Icons.Menu, "Profile"),
        ];

        var node = Render(new NavigationRail(four, selected: 0, _ => { }));
        node.Attributes["style"].Should().Contain("width: 80px");
        node.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Surface)}");

        var destinations = Walk(node).Where(child => child.Tag == "button").ToList();
        destinations.Should().HaveCount(4, "the whole item is the target, exactly as in the bar");
        destinations.Select(button => button.Attributes["aria-label"])
            .Should().Equal("Home", "Cards", "Transfer", "Profile");

        // A header is not a destination: it rides above them and never joins the count.
        var withHeader = Render(new NavigationRail(four, selected: 0, _ => { })
        {
            Leading = new IconButton(new Icon(Icons.Plus), "Compose"),
        });
        Walk(withHeader).Count(child => child.Tag == "button").Should().Be(5);

        var tooFew = () => Render(new NavigationRail([new NavItem(Icons.Person, "A"), new NavItem(Icons.Mail, "B")], 0));
        tooFew.Should().Throw<ArgumentException>("2 destinations → Tabs per spec B4");
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    [Fact]
    public void ClosedFences_AvatarImage_And_BannerDismiss()
    {
        var avatar = Render(new Avatar("AB", SizeVariant.Large, name: "Ana") { ImageSource = "/ana.jpg" });
        avatar.Tag.Should().Be("img");
        avatar.Attributes["style"].Should().Contain("border-radius: 999px");

        var dismissed = false;
        var banner = Render(new Banner(Variant.Info, "Maintenance") { OnDismiss = () => dismissed = true });
        var dismiss = banner.Children[0].Children[2];
        dismiss.Tag.Should().Be("button");
        dismiss.Attributes["aria-label"].Should().Be("Dismiss");
    }
}
