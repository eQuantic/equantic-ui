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

    /// <summary>A round 220 frame of a portrait, held at (0.3, 0.7) and zoomed 1.5, as the issue
    /// measures it (#1785).</summary>
    private static Primitives.Image Framed(float zoom = 1.5f) => new("/portraits/francine.jpg", 220, 220, ImageFit.Cover, "Francine")
    {
        CornerRadius = new CornerRadii(110),
        FocalPoint = new Point(0.3f, 0.7f),
        Zoom = zoom,
    };

    [Fact]
    public void AFocalPoint_IsTheObjectPosition()
    {
        var node = Render(Framed(zoom: 1f));

        node.Tag.Should().Be("img", "with no zoom nothing overflows the element, and no frame is needed");
        node.Attributes["style"].Should().Contain("object-position: 30% 70%").And.Contain("border-radius: 110px")
            .And.NotContain("transform");
    }

    [Fact]
    public void AZoom_ScalesAroundTheFocalPoint_InsideAFrameThatClips()
    {
        var frame = Render(Framed());

        frame.Tag.Should().Be("span");
        frame.Attributes["style"].Should().Contain("display: inline-block").And.Contain("width: 220px")
            .And.Contain("overflow: hidden").And.Contain("border-radius: 110px");
        var picture = frame.Children.Should().ContainSingle().Which;
        picture.Tag.Should().Be("img");
        picture.Attributes["style"].Should().Contain("object-fit: cover").And.Contain("object-position: 30% 70%")
            .And.Contain("transform: scale(1.5)").And.Contain("transform-origin: 30% 70%")
            .And.NotContain("border-radius", "the frame clips, and carries the radius");
    }

    [Fact]
    public void TheDefaults_WriteWhatAnImageAlwaysWrote()
    {
        Render(new Primitives.Image("/photo.jpg", 40, 40)).Attributes["style"]
            .Should().NotContain("object-position").And.NotContain("transform");
    }

    /// <summary>SSR and the client style the framed picture ONE way: the classes are cross-pinned with
    /// <c>image-framing-parity.spec.ts</c>, which asserts the same strings for the same node.</summary>
    [Fact]
    public void TheFramedPicture_CarriesTheClassesTheTwinProduces()
    {
        var frame = WebRealizer.Lower(Framed(), PhotonTheme.Instance, 1f, new StyleSink()).Render();

        frame.Attributes.GetValueOrDefault("class").Should().Be("eq-12dgucd eq-1fusabo eq-elib30 eq-lc8w9s eq-mbtjfn");
        frame.Children[0].Attributes.GetValueOrDefault("class").Should().Be("eq-12dgucd eq-18isjz0 eq-19qisgk eq-1cnz5gh eq-1fusabo eq-racph6");
    }
}
