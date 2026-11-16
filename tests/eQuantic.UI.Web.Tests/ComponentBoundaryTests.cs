using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The BOUNDARY: a component whose <c>Build</c> throws costs its own subtree and nothing else.
///
/// Before this, the throw travelled out through the realizer into the SSR pipeline and the request
/// became a 500 — one null reference in one card, and the visitor got an error page instead of a
/// site. The panel that takes its place is built from the vocabulary, so it is the same surface a
/// window draws.
///
/// <para>
/// Every case runs through each DOOR a tree is lowered by: with no host (the email realizer's and the
/// design host's lowering, which expands as it walks), and through a <see cref="MarkupHost"/>, whose
/// owner builds the tree first and whose lowering reads what each position built, which is how the
/// server draws every write-once page. The cases ran through the first door only, and a component that
/// builds itself, contained there, took the server's process down through the second (#1544).
/// </para>
/// </summary>
public class ComponentBoundaryTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>How a tree reaches the lowering.</summary>
    public enum Door
    {
        /// <summary><see cref="WebRealizer.Lower(VisualNode, IAppTheme, float, StyleSink?, Density, ComponentBoundary?)"/>:
        /// no host, no frame.</summary>
        Hostless,

        /// <summary><see cref="MarkupHost.Structural"/>: a tree built once and lowered from its frame.</summary>
        StructuralHost,

        /// <summary><see cref="MarkupHost.ForPage"/>: the host the server draws a page through.</summary>
        PageHost,
    }

    /// <summary>Fails the way real components do: mid-build, on data it assumed was there.</summary>
    private sealed class BrokenCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            throw new InvalidOperationException("Sequence contains no elements");
    }

    private static string Rendered(Door door, VisualNode node, ComponentBoundary? boundary = null) =>
        Flatten(Lowered(door, node, boundary ?? new ComponentBoundary()).Render());

    /// <summary>A tree lowered through <paramref name="boundary"/> (#1452), by <paramref name="door"/>.</summary>
    private static HtmlElement Lowered(Door door, VisualNode node, ComponentBoundary boundary)
    {
        if (door == Door.Hostless) return WebRealizer.Lower(node, Theme, boundary: boundary);

        var host = door == Door.PageHost
            ? MarkupHost.ForPage(Theme, boundary: boundary)
            : MarkupHost.Structural(Theme, boundary: boundary);
        host.Build(node);
        var lowered = host.Lower(new RenderChannels());
        host.EndFrame();
        return lowered;
    }

    private static string Flatten(HtmlNode node)
    {
        var own = node.TextContent ?? string.Empty;
        var children = string.Join(' ', node.Children.Select(Flatten));
        return $"{own} {children}".Trim();
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void The_throw_does_not_reach_the_caller(Door door)
    {
        var render = () => Lowered(door, new BrokenCard(), new ComponentBoundary());

        render.Should().NotThrow();
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void Siblings_of_a_failed_component_still_render(Door door)
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new BrokenCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var rendered = Rendered(door, row);

        rendered.Should().Contain("before").And.Contain("after");
        rendered.Should().Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void Production_keeps_the_exception_to_itself(Door door)
    {
        var rendered = Rendered(door, new BrokenCard(), new ComponentBoundary { Diagnostics = false });

        rendered.Should().Contain("This section could not be displayed.");
        // An exception message is written for whoever wrote the code — it can name an id, a path or
        // a query, and none of that belongs on a stranger's screen.
        rendered.Should().NotContain("Sequence contains no elements").And.NotContain("BrokenCard");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void Development_names_the_component_and_quotes_the_throw(Door door)
    {
        var rendered = Rendered(door, new BrokenCard(), new ComponentBoundary { Diagnostics = true });

        rendered.Should().Contain("BrokenCard failed to render");
        rendered.Should().Contain("InvalidOperationException: Sequence contains no elements");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void The_failure_is_reported_rather_than_swallowed(Door door)
    {
        var reported = new List<string>();
        var boundary = new ComponentBoundary
        {
            Report = (component, error) => reported.Add($"{component.GetType().Name}: {error.Message}"),
        };
        Lowered(door, new BrokenCard(), boundary);

        // A boundary that only swallows trades a loud crash for a silent one: nobody is paged and
        // the bug lives forever behind a small red box.
        reported.Should().ContainSingle().Which.Should().Be("BrokenCard: Sequence contains no elements");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void A_component_that_stops_throwing_builds_again(Door door)
    {
        var flaky = new FlakyCard();

        Rendered(door, flaky).Should().Contain("could not be displayed");
        flaky.Healed = true;
        // No failure is remembered: a retry, new props or a hot reload simply build on the next pass.
        Rendered(door, flaky).Should().Contain("all good");
    }

    private sealed class FlakyCard : Primitives.StatelessComponent
    {
        public bool Healed { get; set; }

        public override VisualNode Build(ComponentContext context) =>
            Healed ? new Text("all good", TypeRole.BodyM) : throw new InvalidOperationException("not yet");
    }

    /// <summary>
    /// The OTHER way a component fails to produce a subtree, and the one that used to walk past this
    /// boundary entirely: it never throws, so nothing catches it — the realizer expands what Build
    /// returned, which is the component again, forever, until the stack dies. A
    /// <see cref="StackOverflowException"/> cannot be caught in .NET, so the request went with it.
    /// </summary>
    private sealed class OuroborosCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => this;
    }

    /// <summary>
    /// The same instance again, one node down: what it builds is a box around itself. A host's owner
    /// places it at every position of the chain until the bound, and a lowering that found the instance
    /// by itself alone met the outermost position at every step.
    /// </summary>
    private sealed class MirrorCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Box(new BoxStyle(), this);
    }

    private sealed class PingCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PongCard();
    }

    private sealed class PongCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PingCard();
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void A_component_that_builds_itself_is_contained_like_one_that_throws(Door door)
    {
        var render = () => Rendered(door, new OuroborosCard());

        render.Should().NotThrow();
        Rendered(door, new OuroborosCard()).Should().Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void A_component_that_builds_a_box_around_itself_is_contained_too(Door door)
    {
        var rendered = Rendered(door, new MirrorCard());

        rendered.Should().Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void Two_components_that_build_each_other_are_contained_too(Door door)
    {
        // The pair matters on its own: neither instance ever repeats, so nothing that watched for a
        // component meeting ITSELF would see this one.
        var render = () => Rendered(door, new PingCard());

        render.Should().NotThrow();
        Rendered(door, new PingCard()).Should().Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void Siblings_of_a_cyclic_component_still_render(Door door)
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new OuroborosCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var rendered = Rendered(door, row);

        rendered.Should().Contain("before").And.Contain("after");
        rendered.Should().Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void A_cycle_is_reported_and_tallied_exactly_as_a_throw_is(Door door)
    {
        // The whole point of routing it through the boundary rather than bounding it in the
        // realizer: a host that refuses to exit zero while the tally is non-empty catches a cycle
        // the same way it catches a throw, and the logger hears about it either way. ONCE through a
        // host too: its owner contains the chain where it builds it, and the lowering draws what the
        // owner built rather than bounding the chain a second time.
        var reported = new List<string>();
        var boundary = new ComponentBoundary
        {
            Report = (component, error) => reported.Add($"{component.GetType().Name}: {error.GetType().Name}"),
        };
        Lowered(door, new OuroborosCard(), boundary);

        reported.Should().ContainSingle().Which.Should().Be("OuroborosCard: InvalidOperationException");
        boundary.Contained.Should().ContainSingle().Which.Should().Be("OuroborosCard");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void A_cyclic_component_reaches_the_bound_through_the_positioning_door_too(Door door)
    {
        // `Visit(UiComponent)` is not the only door a component enters by: a Stack child goes
        // through `ResolveForPositioning` FIRST, because a `Positioned` returned by a component
        // still has to position. Nothing else here exercises that path.
        var stack = new Stack();
        stack.Add(new Text("behind", TypeRole.BodyM));
        stack.Add(new OuroborosCard());
        stack.Add(new MirrorCard());

        var rendered = Rendered(door, stack);

        rendered.Should().Contain("behind").And.Contain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.Hostless)]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void An_ordinary_chain_of_components_is_not_a_cycle(Door door)
    {
        // The bound exists for what never terminates; a component building a component is ordinary
        // composition and must be untouched by it.
        Rendered(door, new WrapperCard(depth: 12)).Should().Contain("bottom")
            .And.NotContain("could not be displayed");
    }

    [Theory]
    [InlineData(Door.StructuralHost)]
    [InlineData(Door.PageHost)]
    public void One_instance_placed_twice_side_by_side_draws_at_both_places(Door door)
    {
        // An instance at two positions that are not nested is ordinary: a stateless card handed to
        // two cells. Each position is found where its parent stands, and both draw what it builds.
        var card = new WrapperCard(depth: 1);
        var row = new Row(gap: 8);
        row.Add(card);
        row.Add(card);

        var rendered = Rendered(door, row);

        rendered.Split("bottom").Should().HaveCount(3, "the card draws at both of its positions");
        rendered.Should().NotContain("could not be displayed");
    }

    private sealed class WrapperCard(int depth) : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            depth <= 0 ? new Text("bottom", TypeRole.BodyM) : new WrapperCard(depth - 1);
    }
}
