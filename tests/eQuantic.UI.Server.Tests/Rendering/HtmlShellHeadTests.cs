using eQuantic.UI.Server;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// The head is authored as DATA (<see cref="HtmlTag"/>) — app code never types markup, and the
/// renderer owns quoting, encoding and closing.
/// </summary>
public class HtmlShellHeadTests
{
    [Fact]
    public void VoidElement_RendersWithoutAClosingTag()
    {
        HtmlTag.Meta("description", "Ships in C#").Render()
            .Should().Be("<meta name=\"description\" content=\"Ships in C#\">");
    }

    [Fact]
    public void AttributeValues_AreEncoded_SoTheyCannotBreakOut()
    {
        HtmlTag.Meta("description", "a \"quoted\" <b>bold</b> & more").Render()
            .Should().Be("<meta name=\"description\" content=\"a &quot;quoted&quot; &lt;b&gt;bold&lt;/b&gt; &amp; more\">");
    }

    [Fact]
    public void NullAttributeValue_RendersAsABooleanAttribute()
    {
        new HtmlTag("link").Attr("rel", "preconnect").Attr("href", "https://x.dev/").Attr("crossorigin").Render()
            .Should().Be("<link rel=\"preconnect\" href=\"https://x.dev/\" crossorigin>");
    }

    [Fact]
    public void InlineScript_KeepsItsBodyVERBATIM()
    {
        // The body is JS, not text: encoding it would break every `<`, `&&` and quote in the source.
        HtmlTag.InlineScript("if (a && b < c) go('x');", defer: false).Render()
            .Should().Be("<script>if (a && b < c) go('x');</script>");
    }

    [Fact]
    public void ExternalScript_DefersByDefault()
    {
        HtmlTag.Script("https://cdn.dev/x.js").Render()
            .Should().Be("<script src=\"https://cdn.dev/x.js\" defer></script>");
    }

    [Fact]
    public void ShellHelpers_EmitTheTypedTags()
    {
        var shell = new HtmlShellOptions()
            .AddDescription("Products, libraries & consulting")
            .AddPreconnect(new Uri("https://fonts.googleapis.com"))
            .AddPreconnect(new Uri("https://fonts.gstatic.com"), crossOrigin: true)
            .AddStylesheet(new Uri("https://fonts.googleapis.com/css2?family=Geist&display=swap"));

        shell.HeadTags.Should().Equal(
            "<meta name=\"description\" content=\"Products, libraries &amp; consulting\">",
            "<link rel=\"preconnect\" href=\"https://fonts.googleapis.com/\">",
            "<link rel=\"preconnect\" href=\"https://fonts.gstatic.com/\" crossorigin>",
            "<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?family=Geist&amp;display=swap\">");
    }

    [Fact]
    public void RawOverload_StaysAvailableAsTheEscapeHatch()
    {
        var shell = new HtmlShellOptions().AddHeadTag("<!-- build marker -->");
        shell.HeadTags.Should().ContainSingle().Which.Should().Be("<!-- build marker -->");
    }
}
