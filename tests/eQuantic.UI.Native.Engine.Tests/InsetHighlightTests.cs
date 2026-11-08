using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A box's inner top highlight (<see cref="BoxStyle.InsetHighlight"/>) is drawn on Photon as its documentation
/// says, the hairline along the top of the padding box, where the web writes an inset box-shadow one point down
/// (#1036). Until then no native code read the member: the browser drew the gloss and Photon drew a flat box.
/// Read from the pixels the reference backend draws, beside what the web realizer writes for the same box.
/// </summary>
public class InsetHighlightTests
{
    private static readonly Color Black = new(0, 0, 0, 255);
    private static readonly Color Blue = new(0, 80, 160, 255);
    private static readonly Color White = new(255, 255, 255, 255);
    private static readonly Color Red = new(200, 0, 0, 255);

    /// <summary>A 60 by 30 blue box with corners of 8 and a white highlight, 10 points into a black page.</summary>
    private static VisualNode Page(float border = 0) => new Box(
        new BoxStyle { Background = new ColorToken(Black), Padding = EdgeInsets.All(10) },
        new Box(new BoxStyle
        {
            Width = 60,
            Height = 30,
            Background = new ColorToken(Blue),
            CornerRadius = new CornerRadii(8),
            BorderWidth = border,
            BorderColor = new ColorToken(Red),
            InsetHighlight = new ColorToken(White),
        }));

    private static Func<int, int, Color> Drawn(VisualNode page)
    {
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 80, 50);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(80, 50);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        var pixels = new byte[80 * 50 * 4];
        ((ReferenceSurface)surface).ReadPixelsSrgb(pixels);
        return (x, y) =>
        {
            var at = (y * 80 + x) * 4;
            return new Color(pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]);
        };
    }

    [Fact]
    public void TheHighlightIsAHairlineAlongTheTop_InsideTheCorners()
    {
        var at = Drawn(Page());

        at(40, 10).Should().Be(White, "the box's top row is the highlight");
        at(40, 11).Should().Be(Blue, "and one point tall");
        at(40, 25).Should().Be(Blue);
        at(10, 10).Should().Be(Black, "the corner's curve clips it");
    }

    [Fact]
    public void TheHighlightSitsInsideTheBorder()
    {
        var at = Drawn(Page(border: 2));

        at(40, 10).Should().Be(Red);
        at(40, 11).Should().Be(Red);
        at(40, 12).Should().Be(White, "an inset shadow is drawn in the padding box, inside the border");
        at(40, 13).Should().Be(Blue);
    }

    [Fact]
    public void TheWebWritesTheSameHighlight_AsAnInsetShadowOnePointDown()
    {
        var html = HtmlRenderer.RenderNode(WebRealizer.Lower(Page(), PhotonTheme.Instance).Render());

        html.Should().Contain("inset 0 1px 0", "the web's half of the same box, the shadow CSS draws as the hairline");
    }
}
