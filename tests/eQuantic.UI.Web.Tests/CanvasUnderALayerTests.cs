using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web half of the shape a consumer actually writes: a Canvas inside the ordinary layout of a
/// screen, with chrome stacked over it — a sunburst under its hub, a bubble field under its labels,
/// a chart under its tooltip.
/// <para>
/// A layout node that PAINTS NOTHING is not a hit target here (LowerFlex: a Row or Column with no
/// background lowers to <c>pointer-events: none</c>, Flutter's rule rather than the DOM's), and
/// <c>pointer-events</c> INHERITS. So an interactive canvas inside almost any layout inherited the
/// disclaimer and went mute: every handler the app declared was never called, the hover fell
/// through to whatever was behind, and the arithmetic behind the canvas stayed perfectly correct —
/// which is why a green suite kept saying so. What never arrived was the EVENT.
/// </para>
/// </summary>
public class CanvasUnderALayerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var deeper in Walk(child)) yield return deeper;
    }

    private static string StyleOf(HtmlNode node) => node.Attributes.GetValueOrDefault("style") ?? "";

    /// <summary>A screen: an unpainted Column holding a Stack of canvas + chrome.</summary>
    private static VisualNode Screen(bool interactive)
    {
        var canvas = new Canvas(p => p.FillCircle(p.Width / 2, p.Height / 2, 80, Theme.BorderStrong),
            SizeValue.Fill, SizeValue.Fill)
        {
            Label = "Sunburst",
            OnPointerMove = interactive ? _ => { } : null,
        };

        var stack = new Stack { Width = SizeValue.Fixed(200), Height = SizeValue.Fixed(200) };
        stack.Add(canvas);
        stack.Add(new Positioned(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(60),
            Height = SizeValue.Fixed(60),
            Background = Theme.Surface,
        }, new Text("42", TypeRole.Title, Theme.TextPrimary)), top: 70, start: 70));

        var column = new Column(gap: Space.S2);
        column.Add(new Text("Storage", TypeRole.Title, Theme.TextPrimary));
        column.Add(stack);
        return column;
    }

    [Fact]
    public void AnInteractiveCanvasInsideALayout_TakesThePointerBack()
    {
        var rendered = Walk(WebRealizer.Lower(Screen(interactive: true), Theme).Render()).ToList();

        // THE PREMISE: the unpainted Column really does disclaim the pointer, and the disclaimer
        // inherits. Without this the canvas would need no declaration and the test would prove
        // nothing.
        rendered.Should().Contain(n => StyleOf(n).Contains("pointer-events: none"),
            "an unpainted layout node is not a hit target, and pointer-events inherits");

        var svg = rendered.Should().ContainSingle(n => n.Tag == "svg").Subject;
        StyleOf(svg).Should().Contain("pointer-events: auto",
            "a canvas that listens must take the pointer back from whatever it inherits");
    }

    /// <summary>
    /// SSR and the client must style one canvas ONE way. The strings below are cross-pinned with
    /// <c>canvas-parity.spec.ts</c>, which asserts the same four classes for the same canvas built
    /// by the twin: an element styled by classes on the server and by an inline style in the
    /// browser is the same element described twice, which is what the atomizer exists to prevent.
    /// </summary>
    [Fact]
    public void TheAtomizedCanvas_CarriesTheSameClassesTheTwinProduces()
    {
        var rendered = Walk(WebRealizer.Lower(Screen(interactive: true), Theme, 1f, new StyleSink()).Render()).ToList();
        var svg = rendered.Single(n => n.Tag == "svg");

        svg.Attributes.GetValueOrDefault("class").Should().Be("eq-16x7aca eq-1jjc6f6 eq-akjizx eq-g7fowl");
        StyleOf(svg).Should().BeEmpty("every declaration became a shared class");
    }

    [Fact]
    public void ADecorativeCanvasInTheSameLayout_StaysOutOfTheWay()
    {
        var rendered = Walk(WebRealizer.Lower(Screen(interactive: false), Theme).Render()).ToList();
        var svg = rendered.Should().ContainSingle(n => n.Tag == "svg").Subject;

        StyleOf(svg).Should().Contain("pointer-events: none",
            "a canvas with no handlers must not swallow the press that belongs under it");
    }
}
