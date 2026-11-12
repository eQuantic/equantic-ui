using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A foreground descends to the text and icons that name no colour, on the web (#614): CSS's own
/// inheritance is Flutter's <c>DefaultTextStyle</c>, so the box writes <c>color</c> and a text with no
/// colour writes none; an icon already drew in <c>currentColor</c>. The control's button inherits the
/// colour rather than the user agent's <c>buttontext</c>. The Photon half is
/// <c>AForegroundDescendsTests</c>.
/// </summary>
public class AForegroundDescendsRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Quiet = new(Color.FromRgb(0x60, 0x60, 0x60));
    private static readonly ColorToken Bright = new(Color.FromRgb(0x10, 0x90, 0xF0));

    private static (HtmlNode Node, string Css) Lower(VisualNode node)
    {
        var sink = new StyleSink();
        var html = WebRealizer.Lower(node, Theme, 1f, sink).Render();
        return (html, sink.Css);
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>Every declaration the node's classes write at rest.</summary>
    private static List<string> Declarations(HtmlNode node, string css) =>
        (node.Attributes.GetValueOrDefault("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(cls => Regex.Match(css, $@"(?:^|\}})\.{Regex.Escape(cls)}\{{([^{{}}]*)\}}"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToList();

    [Fact]
    public void TheBoxWritesItsForeground_AndAPlainTextWritesNone()
    {
        var (node, css) = Lower(new Box(new BoxStyle { Foreground = Quiet },
            new Text("Continue", TypeRole.Label)));

        Declarations(node, css).Should().Contain($"color:{TokenCss.Value(Quiet)}");
        var text = Walk(node).Skip(1).First(candidate => candidate.Children.Count == 0 || candidate.Tag is "span" or "p");
        Declarations(text, css).Should().NotContain(declaration => declaration.StartsWith("color:"),
            "a text with no colour inherits the box's");
    }

    [Fact]
    public void ATextThatNamesItsColour_WritesIt()
    {
        var (node, css) = Lower(new Text("Continue", TypeRole.Label, Bright));

        Declarations(node, css).Should().Contain($"color:{TokenCss.Value(Bright)}");
    }

    [Fact]
    public void AControlsHoverForeground_RidesTheControlsHover()
    {
        var (_, css) = Lower(new Pressable(new Box(new BoxStyle
        {
            Foreground = Quiet,
            Hover = new StyleDiff { Foreground = Bright },
        }, new Text("Continue", TypeRole.Label)), () => { }));

        css.Should().MatchRegex(
            $@"@media \(hover: hover\)\{{:where\(\.eq-pressable:hover\) [^{{}}]*\{{color:{Regex.Escape(TokenCss.Value(Bright))}\}}\}}",
            "the label brightens while the control is hovered");
    }

    [Fact]
    public void TheControlsButton_InheritsTheColour()
    {
        var (node, css) = Lower(new Pressable(new Box(new BoxStyle()), () => { }));

        Declarations(node, css).Should().Contain("color:inherit",
            "the user agent's buttontext would stand between the content and its foreground");
    }
}
