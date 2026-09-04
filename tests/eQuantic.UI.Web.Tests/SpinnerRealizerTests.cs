using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spinner (spec B15) and the B14 value-transition on web. The spinner lowers to an SVG of 8 rrect
/// bars whose rotation phase rides per-bar NEGATIVE animation-delays over the generated 800ms fade
/// (exact parity with the native f(t) alphas: bar i at t=0 sits i·100ms into the 1→0.3 sawtooth);
/// Reduce Motion zeroes the delays — pulse in place. Value transitions: Flexible.AnimateChanges
/// lowers to a generated-token flex-grow transition; the stateful ProgressBar omits it on a
/// regression so the change snaps (forward-only, spec B14). Cross-pinned with spinner.spec.ts.
/// </summary>
public class SpinnerRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void Spinner_LowersToTheEightBarSvg()
    {
        var node = Render(new Spinner(IconSize.Md, Theme.Colors(Variant.Primary).Base));

        node.Tag.Should().Be("svg");
        node.Attributes["class"].Should().Be("eq-spinner");
        node.Attributes["viewBox"].Should().Be("0 0 16 16");
        node.Attributes["fill"].Should().Be("currentColor");
        node.Attributes["aria-hidden"].Should().Be("true");
        node.Attributes["style"].Should().Be(
            $"width: 24px; height: 24px; color: {TokenCss.Value(Theme.Colors(Variant.Primary).Base)}");

        node.Children.Should().HaveCount(8);
        for (var i = 0; i < 8; i++)
        {
            var bar = node.Children[i];
            bar.Tag.Should().Be("rect");
            bar.Attributes["x"].Should().Be("7");
            bar.Attributes["width"].Should().Be("2");
            bar.Attributes["height"].Should().Be("5");
            bar.Attributes["rx"].Should().Be("1");
            bar.Attributes["transform"].Should().Be($"rotate({i * 45} 8 8)");
            bar.Attributes["style"].Should().Be($"animation-delay: -{i * 100}ms",
                "the negative delay IS the rotation phase (parity with the native stagger)");
        }
    }

    [Fact]
    public void GeneratedStylesheet_CarriesTheSpinnerMechanics()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain("@keyframes eq-spinner-fade { from { opacity: 1; } to { opacity: 0.3; } }");
        css.Should().Contain(".eq-spinner rect { animation: eq-spinner-fade 800ms linear infinite; }");
        css.Should().Contain(".eq-spinner { opacity: 0; animation: eq-appear 1ms linear 400ms forwards; }",
            "the 400ms anti-flash appear delay (spec B15)");
        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-spinner rect { animation-delay: 0ms !important; } }",
            "Reduce Motion drops the rotation phase — the bars pulse in place");
    }

    [Fact]
    public void AnimatedFlexible_CarriesTheTokenTransition()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Flexible(new Box(new BoxStyle { Height = 4 }), 640) { AnimateChanges = true });
        row.Add(new Spacer(360));

        var node = Render(row);
        node.Children[0].Attributes["style"].Should().Contain(
            "transition: flex-grow var(--eq-motion-base) var(--eq-curve-standard)");
    }

    [Fact]
    public void ProgressBar_ForwardChangeAnimates_RegressionSnaps()
    {
        var bar = new ProgressBar(0.3f);
        string FillStyle() => Render(bar).Children[0].Attributes["style"]!;

        FillStyle().Should().Contain("transition: flex-grow",
            "a fresh bar has no regression to honor");

        // Forward adoption (0.3 → 0.6): the next build animates.
        bar.AdoptConfig(new ProgressBar(0.6f));
        FillStyle().Should().Contain("transition: flex-grow");

        // Regression (0.6 → 0.2): the next build SNAPS (spec B14: honesty over smoothness)…
        bar.AdoptConfig(new ProgressBar(0.2f));
        FillStyle().Should().NotContain("transition:");

        // …and only that one build: the following forward change animates again.
        bar.AdoptConfig(new ProgressBar(0.8f));
        FillStyle().Should().Contain("transition: flex-grow");
    }
}
