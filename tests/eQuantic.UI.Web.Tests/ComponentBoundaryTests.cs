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
/// </summary>
public class ComponentBoundaryTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>Fails the way real components do: mid-build, on data it assumed was there.</summary>
    private sealed class BrokenCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            throw new InvalidOperationException("Sequence contains no elements");
    }

    private static string Rendered(VisualNode node) => Flatten(WebRealizer.Lower(node, Theme).Render());

    private static string Flatten(HtmlNode node)
    {
        var own = node.TextContent ?? string.Empty;
        var children = string.Join(' ', node.Children.Select(Flatten));
        return $"{own} {children}".Trim();
    }

    [Fact]
    public void The_throw_does_not_reach_the_caller()
    {
        var render = () => WebRealizer.Lower(new BrokenCard(), Theme);

        render.Should().NotThrow();
    }

    [Fact]
    public void Siblings_of_a_failed_component_still_render()
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new BrokenCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var rendered = Rendered(row);

        rendered.Should().Contain("before").And.Contain("after");
        rendered.Should().Contain("could not be displayed");
    }

    [Fact]
    public void Production_keeps_the_exception_to_itself()
    {
        ComponentBoundary.Diagnostics = false;

        var rendered = Rendered(new BrokenCard());

        rendered.Should().Contain("This section could not be displayed.");
        // An exception message is written for whoever wrote the code — it can name an id, a path or
        // a query, and none of that belongs on a stranger's screen.
        rendered.Should().NotContain("Sequence contains no elements").And.NotContain("BrokenCard");
    }

    [Fact]
    public void Development_names_the_component_and_quotes_the_throw()
    {
        ComponentBoundary.Diagnostics = true;
        try
        {
            var rendered = Rendered(new BrokenCard());

            rendered.Should().Contain("BrokenCard failed to render");
            rendered.Should().Contain("InvalidOperationException: Sequence contains no elements");
        }
        finally
        {
            ComponentBoundary.Diagnostics = false;
        }
    }

    [Fact]
    public void The_failure_is_reported_rather_than_swallowed()
    {
        var reported = new List<string>();
        ComponentBoundary.Report = (component, error) => reported.Add($"{component.GetType().Name}: {error.Message}");
        try
        {
            WebRealizer.Lower(new BrokenCard(), Theme);
        }
        finally
        {
            ComponentBoundary.Report = null;
        }

        // A boundary that only swallows trades a loud crash for a silent one: nobody is paged and
        // the bug lives forever behind a small red box.
        reported.Should().ContainSingle().Which.Should().Be("BrokenCard: Sequence contains no elements");
    }

    [Fact]
    public void A_component_that_stops_throwing_builds_again()
    {
        var flaky = new FlakyCard();

        Rendered(flaky).Should().Contain("could not be displayed");
        flaky.Healed = true;

        // No failure is remembered: a retry, new props or a hot reload simply build on the next pass.
        Rendered(flaky).Should().Contain("all good");
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

    private sealed class PingCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PongCard();
    }

    private sealed class PongCard : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PingCard();
    }

    [Fact]
    public void A_component_that_builds_itself_is_contained_like_one_that_throws()
    {
        var render = () => Rendered(new OuroborosCard());

        render.Should().NotThrow();
        Rendered(new OuroborosCard()).Should().Contain("could not be displayed");
    }

    [Fact]
    public void Two_components_that_build_each_other_are_contained_too()
    {
        // The pair matters on its own: neither instance ever repeats, so nothing that watched for a
        // component meeting ITSELF would see this one.
        var render = () => Rendered(new PingCard());

        render.Should().NotThrow();
        Rendered(new PingCard()).Should().Contain("could not be displayed");
    }

    [Fact]
    public void Siblings_of_a_cyclic_component_still_render()
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new OuroborosCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var rendered = Rendered(row);

        rendered.Should().Contain("before").And.Contain("after");
        rendered.Should().Contain("could not be displayed");
    }

    [Fact]
    public void A_cycle_is_reported_and_tallied_exactly_as_a_throw_is()
    {
        // The whole point of routing it through the boundary rather than bounding it in the
        // realizer: a host that refuses to exit zero while the tally is non-empty catches a cycle
        // the same way it catches a throw, and the logger hears about it either way.
        var reported = new List<string>();
        ComponentBoundary.ClearContained();
        ComponentBoundary.Report = (component, error) => reported.Add($"{component.GetType().Name}: {error.GetType().Name}");
        try
        {
            WebRealizer.Lower(new OuroborosCard(), Theme);
        }
        finally
        {
            ComponentBoundary.Report = null;
        }

        reported.Should().ContainSingle().Which.Should().Be("OuroborosCard: InvalidOperationException");
        ComponentBoundary.Contained.Should().ContainSingle().Which.Should().Be("OuroborosCard");
        ComponentBoundary.ClearContained();
    }

    [Fact]
    public void An_ordinary_chain_of_components_is_not_a_cycle()
    {
        // The bound exists for what never terminates; a component building a component is ordinary
        // composition and must be untouched by it.
        Rendered(new WrapperCard(depth: 12)).Should().Contain("bottom")
            .And.NotContain("could not be displayed");
    }

    private sealed class WrapperCard(int depth) : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            depth <= 0 ? new Text("bottom", TypeRole.BodyM) : new WrapperCard(depth - 1);
    }
}
