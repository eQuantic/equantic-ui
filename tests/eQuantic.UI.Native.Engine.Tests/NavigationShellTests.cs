using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The adaptive SHELL: the same destinations across the bottom of a phone and down the leading edge
/// of a tablet. Spec S6 was pinned with coloured markers, which proves the mechanism and nothing
/// about the vocabulary — until this, no test (and no sample) had ever put real navigation inside
/// an AdaptiveNode, so "responsive" was a claim the framework made about itself.
/// </summary>
public class NavigationShellTests
{
    private static readonly NavItem[] Destinations =
    [
        new(Icons.Person, "Home"),
        new(Icons.Mail, "Cards") { BadgeCount = 2 },
        new(Icons.Search, "Transfer"),
        new(Icons.Menu, "Profile"),
    ];

    /// <summary>The shell, built fresh each time: a node belongs to ONE tree, and both variants of
    /// an adaptive node are real trees.</summary>
    private static VisualNode Shell()
    {
        var phone = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        phone.Add(new Flexible(new Text("body", TypeRole.BodyM), 1));
        phone.Add(new BottomNavigation(Destinations, selected: 0));

        var wide = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        wide.Add(new NavigationRail(Destinations, selected: 0));
        wide.Add(new Flexible(new Text("body", TypeRole.BodyM), 1));

        return new AdaptiveNode(phone, wide);
    }

    private static IReadOnlyList<SemanticNode> Semantics(float width, float height)
    {
        var host = new PhotonHost(Shell(), PhotonTheme.Instance, ThemeMode.Light, width, height);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    /// <summary>
    /// The destinations move EDGE, not identity: on a phone they sit across the bottom, on a tablet
    /// down the leading side. Same four names either way, which is what makes it one app rather than
    /// two that agree by inspection.
    /// </summary>
    [Fact]
    public void TheDestinationsMoveFromTheBottomEdgeToTheLeadingEdge()
    {
        const float height = 800;

        var phone = Semantics(400, height).Where(node => node.Label == "Transfer").ToList();
        phone.Should().ContainSingle("the compact variant is the only one laid out");
        phone[0].Bounds.Top.Should().BeGreaterThan(height - 80, "the bar sits on the bottom edge");

        var tablet = Semantics(900, height).Where(node => node.Label == "Transfer").ToList();
        tablet.Should().ContainSingle();
        tablet[0].Bounds.Left.Should().BeLessThan(80, "the rail sits on the leading edge");
        tablet[0].Bounds.Top.Should().BeLessThan(height / 2, "and its destinations start at the top");
    }

    /// <summary>Every destination survives the crossing: a rail that quietly dropped one would look
    /// fine in a screenshot and lose a section of the app.</summary>
    [Fact]
    public void EveryDestinationSurvivesTheCrossing()
    {
        string[] Names(float width) => Semantics(width, 800)
            .Select(node => node.Label)
            .Where(label => Destinations.Any(item => item.Label == label))
            .ToArray();

        Names(400).Should().Equal("Home", "Cards", "Transfer", "Profile");
        Names(900).Should().Equal("Home", "Cards", "Transfer", "Profile");
    }

    /// <summary>
    /// The two shells, painted. A geometry assertion says the destinations moved; these say what a
    /// person actually sees when they do — the pill, the label and the badge surviving the trip
    /// from the bottom of a phone to the side of a tablet.
    /// </summary>
    [Theory]
    [InlineData(400, 320, "shell-phone")]
    [InlineData(900, 320, "shell-tablet")]
    public void TheShellPaints(int width, int height, string golden)
    {
        var host = new PhotonHost(Shell(), PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            SmoothScroll = false,
        };
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 100);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }

    /// <summary>The rail takes the two destinations the bar refuses (a rail is taller than a bar is
    /// wide), and still sends the eighth to a Drawer.</summary>
    [Fact]
    public void TheRailCarriesMoreDestinationsThanTheBar_ButNotEveryNumber()
    {
        NavItem[] Many(int count) => Enumerable.Range(0, count)
            .Select(i => new NavItem(Icons.Person, $"D{i}")).ToArray();

        var barOverflow = () => new BottomNavigation(Many(6), 0);
        barOverflow.Should().Throw<ArgumentException>("6 destinations do not fit a bar (spec B4)");

        var railTakesSix = () => new NavigationRail(Many(6), 0);
        railTakesSix.Should().NotThrow();

        var railOverflow = () => new NavigationRail(Many(8), 0);
        railOverflow.Should().Throw<ArgumentException>("8+ is a Drawer on either shell");
    }
}
