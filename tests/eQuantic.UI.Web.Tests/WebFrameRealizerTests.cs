using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// WebFrame web lowering: a sandboxed iframe whose <c>sandbox</c> is ALWAYS present (empty =
/// locked), the ONE content attribute its <see cref="WebContent"/> names, fill-by-default sizing,
/// and a title for assistive tech.
/// <para>
/// There used to be a <c>DocumentWins_WhenBothAreSet</c> here, pinning that an inline document
/// beat an address when a caller set both. It is gone because the state it pinned cannot be built
/// any more — <see cref="WebContent"/> is an address or a document and never both — and
/// <see cref="TheTwoFormsAreTheOnlyTwo"/> below asserts that in its place. A precedence nobody can
/// reach is not a behaviour to keep testing; it was a rule the type now enforces.
/// </para>
/// </summary>
public class WebFrameRealizerTests
{
    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    [Fact]
    public void InlineDocument_BecomesASandboxedSrcdocIframe()
    {
        var node = Render(new WebFrame(WebContent.Document("<!doctype html><p>hello</p>"), "Live preview")
        {
            Sandbox = WebSandbox.Scripts | WebSandbox.SameOrigin,
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
        var node = Render(new WebFrame(WebContent.Url("https://example.com/embed"), "Embed"));

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
        Render(new WebFrame(WebContent.Url("/x"), "Locked") { Sandbox = WebSandbox.None })
            .Attributes["sandbox"].Should().Be("");
    }

    [Fact]
    public void ExplicitSize_AndRadius_LowerLikeImage()
    {
        var style = Render(new WebFrame(WebContent.Url("/x"), "Sized")
        {
            Width = 640,
            Height = 360,
            CornerRadius = new CornerRadii(Radius.Md),
        }).Attributes["style"]!;

        style.Should().Contain("width: 640px");
        style.Should().Contain("height: 360px");
        style.Should().Contain("border-radius: 10px");
    }

    /// <summary>
    /// The exclusion, asserted where it now lives. Each form carries its own string and sets its
    /// own attribute, and NEITHER leaves the other reachable — so the realizer's one line has no
    /// second case to get wrong, and there is no "both" for a caller to build.
    /// </summary>
    [Fact]
    public void TheTwoFormsAreTheOnlyTwo()
    {
        var url = WebContent.Url("https://example.com/a");
        var document = WebContent.Document("<p>a</p>");

        url.IsInline.Should().BeFalse();
        url.Value.Should().Be("https://example.com/a");
        document.IsInline.Should().BeTrue();
        document.Value.Should().Be("<p>a</p>");

        // The only public ways to make one are those two statics: a caller cannot hand a frame a
        // value that is an address AND a document, which is what the realizer's precedence was for.
        typeof(WebContent).GetConstructors().Should().BeEmpty(
            "the private constructor is what makes Url and Document the only two forms");

        Render(new WebFrame(url, "A")).Attributes.Should().NotContainKey("srcdoc");
        Render(new WebFrame(document, "A")).Attributes.Should().NotContainKey("src");
    }

    /// <summary>
    /// A frame whose content is the struct's own default draws neither attribute, which is what
    /// the two nullable strings did when both were null. <c>default(WebContent)</c> is the one
    /// value the private constructor cannot stop, so the realizer still guards on emptiness rather
    /// than writing <c>src=""</c>.
    /// </summary>
    [Fact]
    public void TheDefaultContentDrawsNeitherAttribute()
    {
        var node = Render(new WebFrame(default, "Empty"));

        node.Attributes.Should().NotContainKey("src");
        node.Attributes.Should().NotContainKey("srcdoc");
        node.Attributes["title"].Should().Be("Empty");
    }
}
