using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A hover inside a control is the control's on the web (#614): the arrow inside a button nudges while
/// the BUTTON is hovered. Its rule rides the control's hover family, behind the pointer gate, leaving
/// out a box under a nested control nobody hovers; a box outside every control, and one inside a
/// disabled control, keep their own <c>:hover</c>. The Photon half is <c>AHoverInsideAControlTests</c>,
/// and the order against the focus and the press is in <c>PressedStateRealizerTests</c>.
/// </summary>
public class AHoverInsideAControlRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static Box Arrow() => new(new BoxStyle
    {
        Width = 40, Height = 40,
        Hover = new StyleDiff { Transform = Transform2D.Translate(2, 0) },
    });

    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, Theme, 1f, sink);
        return sink.Css;
    }

    private static string HoverRule(string css) =>
        Regex.Match(css, @"@media \(hover: hover\)\{([^{}]*)\{transform:translate\(2px, 0\)\}\}").Groups[1].Value;

    [Fact]
    public void TheArrowInsideAButton_TakesTheButtonsHover()
    {
        var row = new Row(gap: 0) { new Text("Continue", TypeRole.Label), Arrow() };
        var selector = HoverRule(Css(new Pressable(row, () => { })));

        var cls = Regex.Match(selector, @"^:where\(\.eq-pressable:hover\) \.(eq-[0-9a-z]+)\.").Groups[1].Value;
        cls.Should().NotBeEmpty("the arrow's hover is its control's, behind the pointer gate: `{0}`", selector);
        selector.Should().Be($":where(.eq-pressable:hover) .{cls}.{cls}:not(:where(.eq-pressable:not(:hover) .{cls}))");
    }

    [Fact]
    public void ABoxOutsideEveryControl_KeepsItsOwnHover() =>
        HoverRule(Css(Arrow())).Should().MatchRegex(@"^\.eq-[0-9a-z]+:hover$");

    [Fact]
    public void ABoxInsideADisabledControl_KeepsItsOwnHover() =>
        HoverRule(Css(new Pressable(Arrow(), () => { }) { Disabled = true })).Should().MatchRegex(@"^\.eq-[0-9a-z]+:hover$");

    /// <summary>A picture of the row pictures the row: a control nested in it keeps its real hover.</summary>
    [Fact]
    public void APictureOfTheControl_StopsAtANestedOne()
    {
        var inner = new Pressable(Arrow(), () => { });
        var css = Css(new Simulated(SimulatedState.Hovered,
            new Pressable(new Row(gap: 0) { new Text("Row", TypeRole.Label), inner }, () => { })));

        HoverRule(css).Should().StartWith(":where(.eq-pressable:hover) ", "the nested control's hover was not pictured");
    }

    /// <summary>An enabled control inside a disabled one: the outer boxes hover themselves, and the
    /// inner control's take the inner control's hover.</summary>
    [Fact]
    public void AnEnabledControlInsideADisabledOne_TakesItsOwnControlsHover()
    {
        var inner = new Pressable(Arrow(), () => { });
        var css = Css(new Pressable(new Row(gap: 0) { new Text("Row", TypeRole.Label), inner }, () => { }) { Disabled = true });

        HoverRule(css).Should().StartWith(":where(.eq-pressable:hover) ", "the arrow's nearest control is enabled");
    }
}
