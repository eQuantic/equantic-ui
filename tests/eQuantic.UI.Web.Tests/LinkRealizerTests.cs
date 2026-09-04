using eQuantic.UI.Web;
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

    /// <summary>
    /// A link may ask to KEEP THE READER'S POSITION, and the router reads it off the anchor.
    /// <para>
    /// Arriving at the top is right for a link that takes you somewhere else, and wrong for one that
    /// swaps a panel beside a list you were half-way down: the whole shell survives the navigation,
    /// a couple of hundred nodes are patched, and then everything jumps to the top — which reads,
    /// to the person looking at it, exactly like the page reloaded.
    /// </para>
    /// </summary>
    [Fact]
    public void ALinkCanAskToKeepTheReadersPosition()
    {
        var plain = WebRealizer.Lower(new Link("/docs/b", new Text("B", TypeRole.Label)), Theme).Render();
        var keeps = WebRealizer.Lower(
            new Link("/docs/b", new Text("B", TypeRole.Label)) { KeepsPosition = true }, Theme).Render();

        plain.Attributes.Should().NotContainKey("data-eq-keep-position", "a new page starts at its top");
        keeps.Attributes.Should().ContainKey("data-eq-keep-position");
    }

    /// <summary>
    /// An app-internal link INVITES the router to warm it on hover, and an external one does not.
    /// <para>
    /// The router has always had a prefetch seam and nothing ever marked a link for it, so it was
    /// dead code: every navigation paid the module import in full, which is most of what a click
    /// costs. The router asks once per link and swallows failures, so the worst case is work the
    /// click was about to do anyway, done slightly earlier.
    /// </para>
    /// </summary>
    [Fact]
    public void AnAppLinkInvitesPrefetch_AnExternalOneDoesNot()
    {
        var internalLink = WebRealizer.Lower(new Link("/docs/b", new Text("B", TypeRole.Label)), Theme).Render();
        var external = WebRealizer.Lower(
            new Link("https://example.test/x", new Text("X", TypeRole.Label)), Theme).Render();

        internalLink.Attributes.Should().ContainKey("data-prefetch");
        external.Attributes.Should().NotContainKey("data-prefetch",
            "an absolute URL belongs to somebody else's server");
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
