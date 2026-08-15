using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The B2 List · ListItem block of the handoff, held against the code.
/// <para>
/// The numbers here are the design's, not a preference: <b>1-line 52 · 2-line 68 · 3-line 88</b>, and
/// <b>divider inset = 16 + leading + gap</b>. Both were wrong — the three-line row did not exist and
/// every hairline started at a flat 16, which cuts across the icons of any list that has them. A
/// deviation in a number nobody wrote down is a deviation nobody finds; these are written down.
/// </para>
/// </summary>
public class ListHandoffFidelityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static string StyleOf(HtmlNode node) => node.Attributes.GetValueOrDefault("style", "");

    [Theory]
    [InlineData(null, 1, "52px")]
    [InlineData("A second line", 1, "68px")]
    [InlineData("A second sentence of context that needs the room", 2, "88px")]
    public void TheThreeRowHeightsAreTheHandoffs(string? subtitle, int lines, string minHeight)
    {
        var html = Render(new ListItem("Notifications", subtitle) { SubtitleLines = lines });

        Walk(html).Select(StyleOf).Should().Contain(style => style.Contains($"min-height: {minHeight}"));
    }

    /// <summary>The three-line row exists so a settings subtitle can take a second line. A row that
    /// grew to 88 and still truncated at one line would be 88dp of nothing.</summary>
    [Fact]
    public void TheThreeLineRowLetsItsSubtitleWrap()
    {
        var two = Render(new ListItem("Notifications", "Long copy") { SubtitleLines = 2 });
        var one = Render(new ListItem("Notifications", "Long copy"));

        Walk(two).Select(StyleOf).Should().Contain(style => style.Contains("-webkit-line-clamp: 2"));
        Walk(one).Select(StyleOf).Should().NotContain(style => style.Contains("-webkit-line-clamp: 2"));
    }

    /// <summary>
    /// The hairline continues the text edge of the row ABOVE it: 16 + leading + gap. Flat 16 put it
    /// under the icons, which is the one detail a designer spots from across the room.
    /// </summary>
    [Theory]
    [InlineData(24, "52px")]   // Icon Md — 16 + 24 + 12
    [InlineData(40, "68px")]   // Avatar  — 16 + 40 + 12
    public void TheDividerStartsWhereTheRowsTextDoes(float leadingWidth, string inset)
    {
        var html = Render(new List([
            new ListItem("Wi-Fi", "Photon-5G")
            {
                Leading = new Icon(Icons.Info, 24, Theme.TextSecondary),
                LeadingWidth = leadingWidth,
            },
            new ListItem("Notifications"),
        ]));

        Walk(html).Select(StyleOf).Should().Contain(style => style.Contains($"padding: 0 0 0 {inset}"));
    }

    /// <summary>A list with no leading slots keeps the plain 16 — there is nothing to clear.</summary>
    [Fact]
    public void WithoutALeadingSlot_TheInsetIsThePlain16()
    {
        var html = Render(new List([new ListItem("One"), new ListItem("Two")]));

        Walk(html).Select(StyleOf).Should().Contain(style => style.Contains("padding: 0 0 0 16px"));
    }
}
