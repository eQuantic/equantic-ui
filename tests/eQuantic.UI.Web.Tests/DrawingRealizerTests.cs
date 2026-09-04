using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Artwork on the WEB. What is pinned here is that it stays VECTOR in the DOM: an inline
/// <c>&lt;svg&gt;</c> carrying the drawing's own viewBox and one <c>&lt;path&gt;</c> per shape.
/// <para>
/// An <c>&lt;img src="logo.svg"&gt;</c> would have been one line of realizer and the wrong answer:
/// it costs a request, it cannot be tinted, and the parts a designer left as <c>currentColor</c>
/// stop following the theme the moment they cross into an image.
/// </para>
/// </summary>
public class DrawingRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static VectorDrawing Mark() => SvgDocument.Parse("""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
          <rect width="120" height="40" fill="#4F46E5"/>
          <circle cx="20" cy="20" r="12" fill="#F59E0B" fill-opacity="0.5"/>
          <path d="M44 12h60v16H44z" fill="currentColor"/>
          <path d="M0 39h120" stroke="#111827" stroke-width="2" fill="none"/>
        </svg>
        """);

    [Fact]
    public void TheArtworkArrivesAsInlineSvgWithOnePathPerShape()
    {
        var html = Render(new Drawing(Mark(), 240));

        var svg = Walk(html).Single(node => node.Tag == "svg");
        svg.Attributes["viewBox"].Should().Be("0 0 120 40");
        Walk(svg).Count(node => node.Tag == "path").Should().Be(4);
    }

    /// <summary>
    /// The word <c>currentColor</c> survives into the markup instead of being resolved. That is
    /// what lets CSS answer it — the tint is set as `color` on the wrapper, so a mark follows the
    /// theme, a hover, or anything else the cascade decides.
    /// </summary>
    [Fact]
    public void CurrentColorReachesTheMarkupAsCurrentColor()
    {
        var html = Render(new Drawing(Mark(), 240));

        var paths = Walk(html).Where(node => node.Tag == "path").ToList();
        paths[0].Attributes["fill"].Should().Be("#4f46e5", "a literal colour the designer chose");
        paths[2].Attributes["fill"].Should().Be("currentColor");
    }

    [Fact]
    public void TheTintIsSetAsColorSoTheCascadeCanAnswerIt()
    {
        var html = Render(new Drawing(Mark(), 240) { Tint = Theme.Colors(Variant.Primary).Base });

        var svg = Walk(html).Single(node => node.Tag == "svg");
        svg.Attributes["style"].Should().Contain("color:");
    }

    /// <summary>Alpha, the stroke and the fill rule all cross — a shape that lost its
    /// half-transparency or its pen is a shape drawn wrong, quietly.</summary>
    [Fact]
    public void PaintDetailCrossesIntact()
    {
        var html = Render(new Drawing(Mark(), 240));

        var paths = Walk(html).Where(node => node.Tag == "path").ToList();
        paths[1].Attributes["fill"].Should().Be("#f59e0b80");
        paths[3].Attributes["stroke"].Should().Be("#111827");
        paths[3].Attributes["stroke-width"].Should().Be("2");
        paths[3].Attributes["fill"].Should().Be("none");
    }

    /// <summary>One number means the artwork's own aspect: the box is 240×80 for a 3:1 mark,
    /// because a squashed logo is a wrong logo.</summary>
    [Fact]
    public void OneNumberMeansTheArtworksOwnAspect()
    {
        var html = Render(new Drawing(Mark(), 240));

        var style = Walk(html).Single(node => node.Tag == "svg").Attributes["style"];
        style.Should().Contain("width: 240px").And.Contain("height: 80px");
    }

    /// <summary>Decorative unless it is told otherwise — a logo beside the wordmark it repeats is
    /// noise in a screen reader, and a label is the author saying it is not.</summary>
    [Fact]
    public void ItIsDecorativeUntilGivenALabel()
    {
        Walk(Render(new Drawing(Mark(), 240))).Single(node => node.Tag == "svg")
            .Attributes.Should().ContainKey("aria-hidden");

        Walk(Render(new Drawing(Mark(), 240) { Label = "eQuantic" })).Single(node => node.Tag == "svg")
            .Attributes["aria-label"].Should().Be("eQuantic");
    }
}
