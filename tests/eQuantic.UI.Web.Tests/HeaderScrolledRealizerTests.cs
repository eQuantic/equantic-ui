using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using static eQuantic.UI.Web.Tests.CssSpecificity;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A box's <c>Scrolled</c> diff on the web (#1769): the elements INSIDE a pinned header follow its
/// scrolled state, so a header can swap what it shows as the page scrolls without a rebuild. Every
/// member goes through the builder a box's states use, into a family whose rules select on the
/// nearest header's <c>data-eq-scrolled</c>, the attribute the runtime already sets for the header's
/// own <c>ScrolledStyle</c>. The family weighs (0,1,1): over the box's resting class, under every
/// state of its own and its control's.
/// </summary>
public class HeaderScrolledRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private const string UnderAScrolledHeader = "html :where([data-eq-scrolled]) .";

    private static ColorToken Solid(byte r) => new(Color.FromRgb(r, 0x40, 0x50));

    /// <summary>A header holding <paramref name="child"/>, lowered; the header's element and the sheet.</summary>
    private static (HtmlElement Header, string Css) InAHeader(VisualNode child, StyleDiff? headerOwn = null)
    {
        var sink = new StyleSink();
        var header = WebRealizer.Lower(new Pinned(child) { ScrolledStyle = headerOwn }, Theme, 1f, sink);
        return (header, sink.Css);
    }

    [Fact]
    public void EveryMemberOfTheDiff_IsWrittenUnderTheHeadersScroll()
    {
        var (header, css) = InAHeader(new Box(new BoxStyle
        {
            Width = 96, Height = 96,
            Scrolled = new StyleDiff
            {
                Background = Solid(0x01),
                Opacity = 0,
                Transform = Transform2D.Scale(0.6f),
                BackdropBlur = 8,
                Elevation = 2,
            },
        }));
        var badge = (HtmlElement)header.Children[0];

        foreach (var declaration in new[]
                 {
                     @"opacity:0", @"transform:scale\(0\.6\)", @"background-color:[^{}]*",
                     @"-webkit-backdrop-filter:blur\(8px\);backdrop-filter:blur\(8px\)", @"box-shadow:[^{}]*",
                 })
        {
            RuleWriting(css, badge, declaration).Selector.Should().StartWith(UnderAScrolledHeader,
                $"`{declaration}` applies while the header is scrolled, and never at rest");
        }
        css.Should().NotMatchRegex(@"(?:^|\})\.eq-[a-z0-9]+\{opacity:0\}",
            "the diff is not the box's resting style");
    }

    /// <summary>
    /// The order the design asks for, by the selector alone: atomic rules of equal weight are decided
    /// by the order they reach the sheet, which the server sorts by class name and the browser fills
    /// in the order it lowers. A transparent header's light links turn dark once it is scrolled, and
    /// still light up under the pointer.
    /// </summary>
    [Fact]
    public void TheScrolledFill_BeatsTheRestingFill_AndLosesToTheHover()
    {
        var (header, css) = InAHeader(new Box(new BoxStyle
        {
            Width = 80, Height = 32,
            Background = Solid(0x01),
            Scrolled = new StyleDiff { Background = Solid(0x02) },
            Hover = new StyleDiff { Background = Solid(0x03) },
        }));
        var link = (HtmlElement)header.Children[0];

        (string, (int, int, int)) Fill(byte r) =>
            RuleWriting(css, link, $"background-color:{Regex.Escape(TokenCss.Value(Solid(r)))}");
        var (_, own) = Fill(0x01);
        var (scrolledSelector, scrolled) = Fill(0x02);
        var (_, hover) = Fill(0x03);

        scrolled.Should().Be((0, 1, 1));
        Compare(scrolled, own).Should().BePositive($"`{scrolledSelector}` beats the box's resting fill");
        Compare(hover, scrolled).Should().BePositive($"the hover beats `{scrolledSelector}`");
    }

    /// <summary>The same order inside a control, whose hover, focus and press its boxes take (#614).</summary>
    [Fact]
    public void InsideAControl_EveryStateOfTheControl_BeatsTheScrolledFill()
    {
        var (header, css) = InAHeader(new Pressable(new Box(new BoxStyle
        {
            Width = 80, Height = 32,
            Background = Solid(0x01),
            Scrolled = new StyleDiff { Background = Solid(0x02) },
            Hover = new StyleDiff { Background = Solid(0x03) },
            Focus = new StyleDiff { Background = Solid(0x04) },
            Pressed = new StyleDiff { Background = Solid(0x05) },
        }), () => { }));
        var surface = (HtmlElement)((HtmlElement)header.Children[0]).Children[0];

        (string, (int, int, int)) Fill(byte r) =>
            RuleWriting(css, surface, $"background-color:{Regex.Escape(TokenCss.Value(Solid(r)))}");
        var (_, own) = Fill(0x01);
        var (scrolledSelector, scrolled) = Fill(0x02);

        Compare(scrolled, own).Should().BePositive($"`{scrolledSelector}` beats the box's resting fill");
        foreach (var (state, r) in new[] { ("hover", (byte)0x03), ("focus", (byte)0x04), ("press", (byte)0x05) })
            Compare(Fill(r).Item2, scrolled).Should().BePositive($"the control's {state} beats `{scrolledSelector}`");
    }

    /// <summary>
    /// The nearest header decides: a box under a header that is not scrolled, inside one that is (a
    /// pinned rail whose own scroller pins its group headers), rests. The browser proves the match;
    /// this pins that the rule stops there, in the shape the runtime twin writes.
    /// </summary>
    [Fact]
    public void TheRule_StopsAtANestedHeaderThatIsNotScrolled()
    {
        var (header, css) = InAHeader(new Box(new BoxStyle
        {
            Width = 10, Height = 10,
            Scrolled = new StyleDiff { Opacity = 0.25f },
        }));
        var box = (HtmlElement)header.Children[0];

        var (selector, _) = RuleWriting(css, box, "opacity:0.25");
        var cls = selector[UnderAScrolledHeader.Length..].Split(':')[0];
        selector.Should().Be(
            $"{UnderAScrolledHeader}{cls}:not(:where([data-eq-scrolled] [data-eq-pinned]:not([data-eq-scrolled]) .{cls}))");
    }

    /// <summary>
    /// The header's own rule and a box's are two rules for one declaration: the header's selects on
    /// the class AND the attribute on one element, the box's on the attribute of an ancestor. Hashed in
    /// one family they would be one class, and whichever lowered first would decide both.
    /// </summary>
    [Fact]
    public void TheHeadersOwnRule_AndABoxsRule_AreTwoClasses()
    {
        var (header, css) = InAHeader(new Box(new BoxStyle
        {
            Width = 10, Height = 10,
            Scrolled = new StyleDiff { Opacity = 0.5f },
        }), headerOwn: new StyleDiff { Opacity = 0.5f });

        var own = (header.ClassName ?? "").Split(' ');
        var box = (((HtmlElement)header.Children[0]).ClassName ?? "").Split(' ');
        own.Intersect(box).Should().BeEmpty("one class names one rule");
        css.Should().Contain("[data-eq-scrolled]{opacity:0.5}", "the header's own");
        Regex.IsMatch(css, @"html :where\(\[data-eq-scrolled\]\) \.eq-[a-z0-9]+:not\([^{}]*\)\{opacity:0\.5\}")
            .Should().BeTrue("the box's");
    }

    /// <summary>
    /// A picture of a hovered box in a scrolled header shows the hover, as a real hover does and as
    /// Photon draws it. The simulated hover is written into the resting declarations, which the
    /// scrolled rule outweighs, so the scrolled rule carries the hover's fill itself.
    /// </summary>
    [Fact]
    public void ASimulatedHover_StaysOverTheScrolledFill()
    {
        var (header, css) = InAHeader(new Simulated(SimulatedState.Hovered, new Box(new BoxStyle
        {
            Width = 80, Height = 32,
            Background = Solid(0x01),
            Scrolled = new StyleDiff { Background = Solid(0x02), Opacity = 0.5f },
            Hover = new StyleDiff { Background = Solid(0x03) },
        })));
        var box = Walk(header).Single(element => (element.ClassName ?? "").Split(' ')
            .Any(cls => css.Contains($"{UnderAScrolledHeader}{cls}:")));

        Regex.IsMatch(css, $@"html :where\(\[data-eq-scrolled\]\) \.eq-[a-z0-9]+:not\([^{{}}]*\)\{{background-color:{Regex.Escape(TokenCss.Value(Solid(0x03)))}\}}")
            .Should().BeTrue("scrolled, the pictured hover's fill");
        css.Should().NotContain(TokenCss.Value(Solid(0x02)), "the scrolled fill is under the hover");
        RuleWriting(css, box, "opacity:0.5").Selector.Should().StartWith(UnderAScrolledHeader,
            "what the hover does not set is the scroll's");
    }

    private static IEnumerable<HtmlElement> Walk(HtmlElement element) =>
        element.Children.OfType<HtmlElement>().SelectMany(Walk).Prepend(element);

    /// <summary>A box that declares no diff takes no rule, and an empty diff is none.</summary>
    [Fact]
    public void AnEmptyDiff_WritesNoRule()
    {
        var (_, css) = InAHeader(new Box(new BoxStyle { Width = 10, Height = 10, Scrolled = new StyleDiff() }));

        css.Should().NotContain("html :where(");
    }
}
