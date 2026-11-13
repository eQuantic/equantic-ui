using eQuantic.Console;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using eQuantic.UI.Web.Tests;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The dashboard's layout (<see cref="ConsoleLayout"/>, the app's <c>UseLayout</c>, #1129): it puts its
/// page in the tree ONCE, outside every AdaptiveNode arm, and it lights the destination of the page it
/// shows from where the reader is.
/// <para>
/// The web realizer emits every arm and lets CSS show one, so a page placed inside the arms is mounted
/// once per arm. The frame used to hang the SAME page in both of its two, and the code editor ran two
/// surfaces off one controller: a selection drew its bands in the hidden copy, and ⌘F opened in both.
/// The first repair anyone reaches for, building the page once per arm, was measured as well, and it is
/// worse: the chord went to the hidden arm's binding, and an edit made at one width was missing at the
/// other. What these pin is the shape that fixes both: the page sits in one place, and only the chrome
/// around it adapts.
/// </para>
/// <para>
/// The layout under test is the SAMPLE's own, from the project this suite references: every screen of
/// the dashboard is shown in it, so a copy here would pin a frame nobody renders. That it stays mounted
/// across a navigation is the browser's to prove, and <c>tests/e2e/tests/app-layout.spec.ts</c> does.
/// </para>
/// </summary>
public class ConsoleLayoutTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>
    /// The layout's tree around <paramref name="page"/>, built where the reader is at
    /// <paramref name="location"/>, with the compact drawer opened by the layout's own trigger when
    /// <paramref name="navOpen"/> asks, as a reader's press opens it.
    /// </summary>
    private static VisualNode Frame(VisualNode page, string location = "/code", bool navOpen = false)
    {
        var layout = new ConsoleLayout(page);
        var context = new ComponentContext(Theme, route: new RouteValues(null, null, location));
        if (navOpen)
        {
            var trigger = NodePlacements.Of(layout.Build(context)).Select(placement => placement.Node)
                .OfType<IconButton>().Single(button => button.Label == "Navigation");
            trigger.OnPressed!();
        }

        return layout.Build(context);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/code")]
    public void ThePageIsPlacedOnce_OutsideEveryAdaptiveArm(string location)
    {
        var page = new Box(new BoxStyle());

        var placements = NodePlacements.Of(Frame(page, location))
            .Where(placement => ReferenceEquals(placement.Node, page))
            .ToList();

        placements.Select(placement => placement.Path).Should()
            .ContainSingle("a page reached by two paths is mounted twice on the web");
        placements[0].InArm.Should().BeFalse(
            $"the page sits at {placements[0].Path}: inside an arm it is one mount per arm on the web "
            + "and a different position at each width, so what it holds would not cross the breakpoint");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoNodeOfTheFrameIsPlacedTwice(bool navOpen)
    {
        NodePlacements.Shared(Frame(new Box(new BoxStyle()), navOpen: navOpen))
            .Should().BeEmpty("a node belongs to ONE tree, so each arm builds its own");
    }

    /// <summary>
    /// The symptom itself, on the realizer that produced it: a stateful page is built, and so mounted,
    /// exactly once when the frame is lowered for the web. The frame that shared its page built this
    /// probe twice, once per arm, and the browser then kept one instance at two paths. With the drawer
    /// up as well, because an open drawer is a layer the web lowers in the same pass.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnTheWeb_AStatefulPageIsBuiltOnce(bool navOpen)
    {
        var probe = new Probe();

        var html = WebRealizer.Lower(Frame(probe, navOpen: navOpen), Theme).Render();

        probe.Builds.Should().Be(1, "the web emits every arm, so a page inside them builds once per arm");
        Walk(html).Count(node => node.TextContent == Probe.Marker).Should().Be(1);
    }

    /// <summary>
    /// The layout knows the page it shows only by where the reader is, and lights that destination in
    /// both homes of the nav (the docked rail and the drawer), and nothing else. A screen the sidebar
    /// does not list lights the destination it belongs to, or none.
    /// </summary>
    [Theory]
    [InlineData("/charts", "Charts", "/charts")]
    [InlineData("/", "Payments", "/")]
    [InlineData("/passkeys", "Passkeys", "/identity")]
    [InlineData("/map", "Map", null)]
    [InlineData("/no-such-screen", "Not found", null)]
    public void TheLocation_LightsItsDestination_AndNamesThePage(string location, string title, string? lit)
    {
        var frame = Frame(new Box(new BoxStyle()), location);
        var nodes = NodePlacements.Of(frame).Select(placement => placement.Node).ToList();

        var current = nodes.OfType<Link>().Where(link => link.Current).Select(link => link.Destination).ToList();
        if (lit is null) current.Should().BeEmpty($"no destination of the sidebar is {location}");
        else current.Should().Equal([lit, lit], "the rail and the drawer each light the page's destination");

        nodes.OfType<Breadcrumb>().Select(crumbs => crumbs.Crumbs.Last().Label).Should()
            .Equal([title, title], "both toolbars name the page the reader is on");
    }

    /// <summary>
    /// Every screen the sample declares has a name in its layout. A screen added to the app and to
    /// neither the sidebar nor the layout's list of the others would be called "Not found" in its own
    /// breadcrumb.
    /// </summary>
    [Fact]
    public void EveryDeclaredScreen_IsNamedByTheLayout()
    {
        var unnamed = DashboardSite.Routes
            .Where(route => route != "/404")
            .Where(route => NodePlacements.Of(Frame(new Box(new BoxStyle()), route))
                .Select(placement => placement.Node).OfType<Breadcrumb>()
                .Any(crumbs => crumbs.Crumbs.Last().Label == "Not found"))
            .ToList();

        DashboardSite.Routes.Should().Contain("/charts", "the routes are read off the sample's pages");
        unnamed.Should().BeEmpty("each declared screen is a destination of the sidebar or named by the layout");
    }

    private sealed class Probe : StatefulComponent
    {
        public const string Marker = "the page";

        public int Builds { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            return new Text(Marker, TypeRole.BodyM);
        }
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }
}
