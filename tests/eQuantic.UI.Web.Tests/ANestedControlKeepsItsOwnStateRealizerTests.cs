using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A control's press and focus reach the boxes of THAT control, and stop at a control nested in it
/// (#616): the web half of <c>ANestedControlKeepsItsOwnStateTests</c>.
/// <para>
/// A press reaches the ANCESTORS of what is pressed (<c>:active</c> matches them), so the pressed
/// family leaves out a box under a control that is not active; a focus is the one element's, so the
/// focus family leaves out a box under a control inside the focused one. Both ride a
/// <c>:where()</c>, which weighs nothing, so the families keep their order (PressedStateRealizerTests).
/// </para>
/// </summary>
public class ANestedControlKeepsItsOwnStateRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static ColorToken Solid(byte r) => new(Color.FromRgb(r, 0x40, 0x60));

    private static readonly ColorToken OuterPressed = Solid(0x11), OuterFocused = Solid(0x12);
    private static readonly ColorToken InnerPressed = Solid(0x21), InnerFocused = Solid(0x22);

    private static VisualNode Row() => new Pressable(new Box(new BoxStyle
    {
        Width = 300,
        Height = 60,
        Pressed = new StyleDiff { Background = OuterPressed },
        Focus = new StyleDiff { Background = OuterFocused },
    }, new Pressable(new Box(new BoxStyle
    {
        Width = 40,
        Height = 40,
        Pressed = new StyleDiff { Background = InnerPressed },
        Focus = new StyleDiff { Background = InnerFocused },
    }), () => { })), () => { });

    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, Theme, 1f, sink);
        return sink.Css;
    }

    /// <summary>The selector of the one rule writing <paramref name="token"/> as a fill.</summary>
    private static string SelectorOf(string css, ColorToken token)
    {
        var rules = Regex.Matches(css,
            $@"(?:^|[{{}}])([^{{}}@]*)\{{background-color:{Regex.Escape(TokenCss.Value(token))}\}}");
        rules.Should().ContainSingle($"one rule writes {TokenCss.Value(token)}");
        return rules[0].Groups[1].Value;
    }

    [Fact]
    public void ThePressedFamily_LeavesOutABoxUnderAControlNobodyPressed()
    {
        var selector = SelectorOf(Css(Row()), InnerPressed);
        var cls = Regex.Match(selector, @"^\.eq-pressable\.eq-pressable:active:not\(:where\(:has\(\.eq-pressable:active\)\)\) \.(eq-[0-9a-z]+)").Groups[1].Value;

        cls.Should().NotBeEmpty("the inner box's fill is in the pressed family: {0}", selector);
        selector.Should().Be($".eq-pressable.eq-pressable:active:not(:where(:has(.eq-pressable:active))) .{cls}:not(:where(.eq-pressable:not(:active) .{cls}))");
    }

    [Fact]
    public void TheFocusFamily_LeavesOutABoxUnderAControlInsideTheFocusedOne()
    {
        var selector = SelectorOf(Css(Row()), InnerFocused);
        var cls = Regex.Match(selector, @"^\.eq-pressable:focus-visible \.(eq-[0-9a-z]+)").Groups[1].Value;

        cls.Should().NotBeEmpty("the inner box's fill is in the focus family: {0}", selector);
        selector.Should().Be(
            $".eq-pressable:focus-visible .{cls}:not(:where(.eq-pressable:focus-visible .eq-pressable .{cls}))");
    }

    [Fact]
    public void APictureOfTheRow_PicturesTheRow_AndNotTheControlInIt()
    {
        var css = Css(new Simulated(SimulatedState.Pressed | SimulatedState.Focused, Row()));

        SelectorOf(css, OuterPressed).Should().MatchRegex(@"^\.eq-[0-9a-z]+$", "the row's press is drawn");
        SelectorOf(css, InnerPressed).Should().StartWith(".eq-pressable.eq-pressable:active:not(:where(:has(.eq-pressable:active))) ",
            "the control in the row keeps its real press, which nobody pictured");
        SelectorOf(css, InnerFocused).Should().StartWith(".eq-pressable:focus-visible ",
            "and its real focus");

        var html = HtmlRenderer.RenderNode(WebRealizer.Lower(
            new Simulated(SimulatedState.Focused, Row()), Theme).Render());
        Regex.Count(html, "eq-focused").Should().Be(1, "the pictured ring is the row's alone");
    }

    [Fact]
    public void APictureOfTheControl_InsideTheRow_PicturesTheControl()
    {
        var css = Css(new Pressable(new Box(new BoxStyle
        {
            Width = 300,
            Height = 60,
            Pressed = new StyleDiff { Background = OuterPressed },
        }, new Simulated(SimulatedState.Pressed, new Pressable(new Box(new BoxStyle
        {
            Width = 40,
            Height = 40,
            Pressed = new StyleDiff { Background = InnerPressed },
        }), () => { }))), () => { }));

        SelectorOf(css, InnerPressed).Should().MatchRegex(@"^\.eq-[0-9a-z]+$", "a picture inside a control starts over");
        SelectorOf(css, OuterPressed).Should().StartWith(".eq-pressable.eq-pressable:active:not(:where(:has(.eq-pressable:active))) ",
            "and pictures nothing of the control around it");
    }
}
