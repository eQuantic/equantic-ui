using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A subtree drawn AS IF it were hovered, pressed or focused.
/// <para>
/// A pressed button cannot be handed to a constructor: pressed is something the host observes, not
/// something a tree states. So a documentation gallery, a design review and a visual-regression
/// suite could only ever show a control's rest state — the three states a reader most wants to
/// compare were the three no page could draw.
/// </para>
/// </summary>
public class SimulatedStateTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (string Html, string Css) Render(VisualNode node)
    {
        var sink = new StyleSink();
        var html = Core.Rendering.HtmlRenderer.RenderNode(
            WebRealizer.Lower(node, Theme, 1f, sink).Render());
        return (html, sink.Css);
    }

    private static Primitives.Box Hoverable() => new(new BoxStyle
    {
        Background = Theme.Surface,
        Hover = new StyleDiff { Background = Theme.Colors(Variant.Primary).Base },
    }, new Text("x", TypeRole.BodyM));

    /// <summary>
    /// The web cannot force <c>:hover</c> from CSS — nothing can, which is the point of a
    /// pseudo-class — so the diff folds into the BASE rule. Same declaration, which is what makes
    /// the preview honest rather than an approximation of one.
    /// </summary>
    [Fact]
    public void AHoveredSubtree_DrawsTheHoverDiffAtRest()
    {
        var (_, resting) = Render(Hoverable());
        var (_, simulated) = Render(new Simulated(SimulatedState.Hovered, Hoverable()));

        resting.Should().Contain(":hover", "a real hover still rides its pseudo-class");
        simulated.Should().NotContain(":hover", "there is nothing to hover — it is already drawn");
        simulated.Should().Contain("--eq-color-primary", "the hover's colour is the resting colour now");
    }

    /// <summary>
    /// ONE declaration per property, not two. Every atomic class has equal specificity, so an
    /// element carrying both the base and the simulated colour would be decided by stylesheet
    /// insertion order — by whatever the rest of the page happened to emit first.
    /// </summary>
    [Fact]
    public void TheSimulatedDeclaration_REPLACESTheBaseOne()
    {
        var (html, _) = Render(new Simulated(SimulatedState.Hovered, Hoverable()));

        var classes = System.Text.RegularExpressions.Regex.Match(html, "class=\"([^\"]*)\"").Groups[1].Value;
        classes.Split(' ').Should().OnlyHaveUniqueItems();
        // The surface colour is gone: it was replaced, not overlaid.
        Render(new Simulated(SimulatedState.Hovered, Hoverable())).Css
            .Should().NotContain("light-dark(#ffffff, #14181e)");
    }

    [Fact]
    public void APressedSubtree_CarriesTheSameDeclarationAPressDoes()
    {
        var pressable = new Pressable(new Primitives.Box(new BoxStyle { Background = Theme.Surface },
            new Text("Go", TypeRole.Label)))
        {
            PressedBackground = Theme.Colors(Variant.Primary).Base,
        };

        var (html, _) = Render(new Simulated(SimulatedState.Pressed, pressable));

        html.Should().Contain("eq-pressed");
        html.Should().Contain("eq-pressable", "it is still the control it is picturing");
    }

    /// <summary>Nested previews COMBINE — a hovered card holding a pressed button is two nodes, and
    /// the inner one must not turn the outer's hover off.</summary>
    [Fact]
    public void NestedSimulations_Combine()
    {
        var inner = new Pressable(new Primitives.Box(new BoxStyle { Background = Theme.Surface },
            new Text("Go", TypeRole.Label)))
        {
            PressedBackground = Theme.Colors(Variant.Primary).Base,
        };
        var card = new Primitives.Box(new BoxStyle
        {
            Background = Theme.Surface,
            Hover = new StyleDiff { Elevation = 2 },
        }, new Simulated(SimulatedState.Pressed, inner));

        var (html, css) = Render(new Simulated(SimulatedState.Hovered, card));

        html.Should().Contain("eq-pressed", "the inner press survives the outer hover");
        css.Should().NotContain(":hover", "and the outer hover is drawn, not deferred to a pseudo");
    }

    /// <summary>The state does not LEAK past the node — a preview beside an ordinary control must
    /// leave that control alone.</summary>
    [Fact]
    public void TheStateEndsWithTheNode()
    {
        var row = new Row(gap: Space.S2)
        {
            new Simulated(SimulatedState.Hovered, Hoverable()),
            Hoverable(),
        };

        var (_, css) = Render(row);

        css.Should().Contain(":hover", "the sibling still hovers for real");
    }

    [Fact]
    public void WithoutTheNode_NothingChanges()
    {
        var (html, css) = Render(Hoverable());

        html.Should().NotContain("eq-pressed");
        css.Should().Contain(":hover");
    }
}
