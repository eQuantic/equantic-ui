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
}
