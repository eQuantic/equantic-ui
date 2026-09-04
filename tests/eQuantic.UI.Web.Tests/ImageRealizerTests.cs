using eQuantic.UI.Web;
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

    [Fact]
    public void AThemedPair_ShipsBothArtworks_AndLetsCssPickOne()
    {
        // Colors switch by mode through a ColorToken; artwork cannot, so the pair lives on the
        // node and the generated .eq-themed-* rules choose — the OS preference by default, the
        // app's forced data-theme when it set one. No branch in app code, no mode question.
        var node = Render(new Primitives.Image("/assets/logo.svg", 97, 26, ImageFit.Contain, "eQuantic")
        {
            DarkSource = "/assets/logo-dark.svg",
        });

        node.Tag.Should().Be("span");
        node.Children.Should().HaveCount(2);
        node.Children[0].Attributes["src"].Should().Be("/assets/logo.svg");
        node.Children[0].Attributes["class"].Should().Contain("eq-themed-light");
        node.Children[1].Attributes["src"].Should().Be("/assets/logo-dark.svg");
        node.Children[1].Attributes["class"].Should().Contain("eq-themed-dark");
        // Both keep the same slot geometry — a pair is one image, drawn twice.
        node.Children[1].Attributes["style"].Should().Be(node.Children[0].Attributes["style"]);
    }

    [Fact]
    public void WithoutADarkSource_ItStaysASingleImg()
    {
        Render(new Primitives.Image("/photo.jpg", 40, 40)).Tag.Should().Be("img");
    }
}
