using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// WebFrame web lowering: a sandboxed iframe whose <c>sandbox</c> is ALWAYS present (empty =
/// locked), inline document over source, fill-by-default sizing, and a title for assistive tech.
/// </summary>
public class WebFrameRealizerTests
{
    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    [Fact]
    public void InlineDocument_BecomesASandboxedSrcdocIframe()
    {
        var node = Render(new WebFrame
        {
            Document = "<!doctype html><p>hello</p>",
            Sandbox = WebSandbox.Scripts | WebSandbox.SameOrigin,
            Title = "Live preview",
        });

        node.Tag.Should().Be("iframe");
        node.Attributes["srcdoc"].Should().Be("<!doctype html><p>hello</p>");
        node.Attributes.Should().NotContainKey("src");
        node.Attributes["sandbox"].Should().Be("allow-scripts allow-same-origin");
        node.Attributes["title"].Should().Be("Live preview");
    }

    [Fact]
    public void ByAddress_UsesSrc_AndFillsTheParentByDefault()
    {
        var node = Render(new WebFrame { Source = "https://example.com/embed" });

        node.Attributes["src"].Should().Be("https://example.com/embed");
        node.Attributes.Should().NotContainKey("srcdoc");
        var style = node.Attributes["style"]!;
        style.Should().Contain("width: 100%");
        style.Should().Contain("height: 100%");
        style.Should().Contain("border: 0");
    }

    [Fact]
    public void LockedFrame_EmitsTheEmptySandbox()
    {
        // Empty sandbox = maximum isolation. The attribute MUST still be there: omitting it is
        // the opposite — no isolation at all.
        Render(new WebFrame { Source = "/x", Sandbox = WebSandbox.None })
            .Attributes["sandbox"].Should().Be("");
    }

    [Fact]
    public void ExplicitSize_AndRadius_LowerLikeImage()
    {
        var style = Render(new WebFrame
        {
            Source = "/x",
            Width = 640,
            Height = 360,
            CornerRadius = new CornerRadii(Radius.Md),
        }).Attributes["style"]!;

        style.Should().Contain("width: 640px");
        style.Should().Contain("height: 360px");
        style.Should().Contain("border-radius: 10px");
    }

    [Fact]
    public void DocumentWins_WhenBothAreSet()
    {
        var node = Render(new WebFrame { Document = "<p>doc</p>", Source = "/ignored" });
        node.Attributes["srcdoc"].Should().Be("<p>doc</p>");
        node.Attributes.Should().NotContainKey("src");
    }
}
