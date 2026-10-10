using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A Text is never inline in the vocabulary (inline runs live in <c>Text.Spans</c>), so it lowers as
/// a BLOCK on the web, wherever it sits (#495). As an inline span inside a block parent (a Box, a
/// Flexible, a Link, a Pressable) it sat on the parent's line box, whose strut is the body font at
/// <c>line-height: normal</c>: a 10/15 label in a padded pill was 26.5px tall where its parts add up
/// to 21, and sat 3 to 4px low beside a Text in a Row. A flex or grid item is a block either way,
/// so nothing changes there. Measured in Chromium; the browser is the proof, these pin the markup.
/// </summary>
public class TextBlockFlowTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static readonly TypeStyle Small = new(10, 15, FontWeight.Medium, 0, 1.3f);

    private static Text Label(int maxLines = 0) =>
        new("RFC 9728", TypeRole.Caption, Theme.TextPrimary, maxLines: maxLines, styleOverride: Small);

    private static HtmlNode TextIn(VisualNode tree)
    {
        HtmlNode? found = null;
        void Walk(HtmlNode node)
        {
            if (node.Attributes.TryGetValue("class", out var cls) && cls is not null && cls.Contains("eq-type-caption"))
                found ??= node;
            foreach (var child in node.Children) Walk(child);
        }
        Walk(WebRealizer.Lower(tree, Theme).Render());
        found.Should().NotBeNull();
        return found!;
    }

    [Fact]
    public void ATextInsideABox_IsABlock()
    {
        var pill = new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(6, 2), BorderWidth = 1, BorderColor = Theme.Border }, Label());

        TextIn(pill).Attributes["style"].Should().Contain("display: block");
    }

    [Fact]
    public void ATextInsideAFlexible_IsABlock()
    {
        TextIn(new Row(gap: 8) { new Flexible(Label()) }).Attributes["style"].Should().Contain("display: block");
    }

    [Fact]
    public void ATextInARow_IsABlockToo()
    {
        // A flex item is blockified anyway: the declaration changes nothing there, and one rule for
        // every Text is the one the two producers cannot disagree about.
        TextIn(new Row(gap: 8) { Label() }).Attributes["style"].Should().Contain("display: block");
    }

    [Fact]
    public void AMultiLineClamp_KeepsItsOwnBox()
    {
        TextIn(new Box(new BoxStyle(), Label(maxLines: 2))).Attributes["style"]
            .Should().Contain("display: -webkit-box").And.NotContain("display: block");
    }
}
