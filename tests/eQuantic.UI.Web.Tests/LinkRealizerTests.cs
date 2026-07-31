using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The write-once <see cref="Link"/> on the WEB realizer: a REAL <c>&lt;a href&gt;</c> —
/// SSR-crawlable, intercepted by the SPA router — with UA chrome neutralized by the generated
/// <c>.eq-link</c> (the child owns all visuals, the Pressable contract). The client lowering mirror
/// is pinned in vitest.
/// </summary>
public class LinkRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void Link_LowersToARealAnchor_ChildInside()
    {
        var node = WebRealizer.Lower(
            new Link("/users/2", new Text("Open", TypeRole.Label)) { Label = "Open user 2" }, Theme).Render();

        node.Tag.Should().Be("a");
        node.Attributes["href"].Should().Be("/users/2");
        node.Attributes["class"].Should().Be("eq-link");
        node.Attributes["aria-label"].Should().Be("Open user 2");
        node.Children.Should().NotBeEmpty();
    }

    [Fact]
    public void FillChild_GetsThePassThroughChain()
    {
        var fill = new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, Background = Theme.Surface });
        var node = WebRealizer.Lower(new Link("/x", fill), Theme).Render();

        node.Attributes["style"].Should().Contain("width: 100%").And.Contain("height: 100%");
    }

    [Fact]
    public void Stylesheet_NeutralizesTheAnchorChrome()
    {
        PhotonCssGenerator.Generate(Theme).Should()
            .Contain(".eq-link { display: block; text-decoration: none; color: inherit; }");
    }
}
