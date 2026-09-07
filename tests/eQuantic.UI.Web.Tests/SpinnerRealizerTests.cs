using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
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

    /// <summary>
    /// SSR and the client must describe one bar ONE way. The eight class names below are
    /// cross-pinned with <c>spinner.spec.ts</c>, which asserts the same eight for the same spinner
    /// built by the twin: the client used to hand-build an inline style here, so the same element
    /// carried a shared class when the server drew it and an inline declaration when the browser
    /// did. The suites either side of this one assert the DECLARATION and so could not tell.
    /// </summary>
    [Fact]
    public void TheAtomizedBars_CarryTheSameClassesTheTwinProduces()
    {
        var rendered = WebRealizer.Lower(new Spinner(), Theme, 1f, new StyleSink()).Render();
        var bars = rendered.Children.Where(child => child.Tag == "rect").ToList();

        bars.Select(bar => bar.Attributes.GetValueOrDefault("class")).Should().Equal(
            "eq-153eepm", "eq-1x7lrpl", "eq-1uka0o0", "eq-1rys0p3",
            "eq-1lc4svq", "eq-1e3tu7p", "eq-bzqht8", "eq-fh9n7");
        bars.Should().OnlyContain(bar => !bar.Attributes.ContainsKey("style"),
            "every declaration became a shared class");
    }

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
        css.Should().Contain(".eq-spinner rect { animation-name: eq-spinner-fade; animation-duration: 800ms; animation-timing-function: linear; animation-iteration-count: infinite; }");
        css.Should().Contain(".eq-spinner { opacity: 0; animation: eq-appear 1ms linear 400ms forwards; }",
            "the 400ms anti-flash appear delay (spec B15)");
        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-spinner rect { animation-delay: 0ms !important; } }",
            "Reduce Motion drops the rotation phase — the bars pulse in place");
    }

    /// <summary>
    /// The delay a bar carries only means something if it WINS. It rides an atomic class, whose
    /// selector is one class (0,1,0) and therefore loses to every rule here that names the bar
    /// (0,1,1) — and the fade was written as the <c>animation</c> SHORTHAND, which sets each
    /// longhand it leaves out. So the stylesheet quietly wrote <c>animation-delay: 0s</c> over all
    /// eight, and the ring pulsed in place instead of turning: measured in the browser, where the
    /// bars computed to <c>0s</c> while the class beside them said <c>-100ms</c>.
    ///
    /// Neither suite could see it. Both assert what the realizer PRODUCES, and the realizer was
    /// right; the stylesheet took the value away afterwards. So the invariant is stated over the
    /// stylesheet itself: nothing may set a bar's delay except Reduce Motion, which is entitled to
    /// (it is <c>!important</c>, and zeroing the phase is what it means).
    /// </summary>
    [Fact]
    public void NoRule_OutranksTheDelayABarCarries()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        var rulesTouchingABarsDelay = css.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Contains(".eq-spinner rect"))
            // A shorthand counts: `animation: …` writes the delay whether or not it mentions one.
            .Where(line => line.Contains("animation-delay") || line.Contains("animation:"))
            .ToList();

        rulesTouchingABarsDelay.Should().ContainSingle(
            "a bar's own delay must be the only one, or the stagger is silently dropped")
            .Which.Should().StartWith("@media (prefers-reduced-motion: reduce)");
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
