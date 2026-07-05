using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec A11 web lowering: explicitly sized img, object-fit, rrect clip, alt semantics.</summary>
public class ImageRealizerTests
{
    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    [Fact]
    public void Image_LowersToASizedImg_WithFitAndClip()
    {
        var node = Render(new Primitives.Image("/avatars/ana.jpg", 96, 64, ImageFit.Cover, "Ana")
        {
            CornerRadius = new CornerRadii(Radius.Md),
        });

        node.Tag.Should().Be("img");
        node.Attributes["src"].Should().Be("/avatars/ana.jpg");
        node.Attributes["alt"].Should().Be("Ana");
        var style = node.Attributes["style"]!;
        style.Should().Contain("width: 96px");
        style.Should().Contain("height: 64px");
        style.Should().Contain("object-fit: cover");
        style.Should().Contain("border-radius: 10px");
    }

    [Fact]
    public void ContainAndStretch_MapToTheCssKeywords()
    {
        Render(new Primitives.Image("/a.png", 10, 10, ImageFit.Contain))
            .Attributes["style"].Should().Contain("object-fit: contain");
        Render(new Primitives.Image("/a.png", 10, 10, ImageFit.Stretch))
            .Attributes["style"].Should().Contain("object-fit: fill");
    }

    [Fact]
    public void DecorativeImage_KeepsEmptyAlt()
    {
        Render(new Primitives.Image("/bg.png", 10, 10)).Attributes["alt"].Should().Be("");
    }
}
