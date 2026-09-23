using eQuantic.Console;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The dashboard's frame puts its page in the tree ONCE, outside every AdaptiveNode arm.
/// <para>
/// The web realizer emits every arm and lets CSS show one, so a page placed inside the arms is
/// mounted once per arm. The frame used to hang the SAME page in both of its two, and the code
/// editor ran two surfaces off one controller: a selection drew its bands in the hidden copy, and
/// ⌘F opened in both. The first repair anyone reaches for — build the page once per arm — was
/// measured as well, and it is worse: the chord went to the hidden arm's binding, and an edit made
/// at one width was missing at the other. What these pin is the shape that fixes both: the page
/// sits in one place, and only the chrome around it adapts.
/// </para>
/// <para>
/// The frame under test is the SAMPLE's own file, linked into this project, because every screen of
/// the dashboard reaches its page through it — a copy here would pin a frame nobody renders.
/// </para>
/// </summary>
public class ConsoleShellTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>The frame through either door: a demo route's (<c>Frame</c>) or the payments
    /// route's, which brings its own toolbar actions (<c>Wrap</c>).</summary>
    private static VisualNode Frame(VisualNode page, bool pageActions, bool navOpen = false) => pageActions
        ? ConsoleShell.Wrap(Theme, "/", [new Crumb("Workspace", "/"), new Crumb("Payments")], page,
            new ShellActions(() => { }, () => { }, _ => { }, _ => { }, _ => { }, "", false, () => { },
                NavOpen: navOpen, OnToggleNav: () => { }))
        : ConsoleShell.Frame(Theme, "/code", "Code editor", page, navOpen, () => { });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePageIsPlacedOnce_OutsideEveryAdaptiveArm(bool pageActions)
    {
        var page = new Box(new BoxStyle());

        var placements = NodePlacements.Of(Frame(page, pageActions))
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
    public void NoNodeOfTheFrameIsPlacedTwice(bool pageActions)
    {
        NodePlacements.Shared(Frame(new Box(new BoxStyle()), pageActions))
            .Should().BeEmpty("a node belongs to ONE tree, so each arm builds its own");
    }

    /// <summary>
    /// The symptom itself, on the realizer that produced it: a stateful page is built — and so
    /// mounted — exactly once when the frame is lowered for the web. The frame that shared its page
    /// built this probe twice, once per arm, and the browser then kept one instance at two paths.
    /// With the drawer up as well, because an open drawer is a layer the web lowers in the same pass.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnTheWeb_AStatefulPageIsBuiltOnce(bool navOpen)
    {
        var probe = new Probe();

        var html = WebRealizer.Lower(Frame(probe, pageActions: false, navOpen), Theme).Render();

        probe.Builds.Should().Be(1, "the web emits every arm, so a page inside them builds once per arm");
        Walk(html).Count(node => node.TextContent == Probe.Marker).Should().Be(1);
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
