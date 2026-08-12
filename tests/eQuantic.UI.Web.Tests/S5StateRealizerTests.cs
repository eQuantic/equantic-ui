using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S5 on the web realizer: Hover/Focus StyleDiffs become PSEUDO-VARIANT atomic rules
/// (`.eq-x:hover{…}`) — zero JavaScript, and the rules ride theme variables like every other color.
/// Pseudo-states REQUIRE the atomic pipeline: without a sink they are (documentedly) not realizable
/// inline and are skipped.
/// </summary>
public class S5StateRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void HoverDiff_BecomesAPseudoVariantRule_OnTheSameElement()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Background = Theme.Surface,
            Hover = new StyleDiff { Background = Theme.SurfaceSubtle, Elevation = 2 },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        var subtle = TokenCss.Value(Theme.SurfaceSubtle);
        sink.Css.Should().Contain($":hover{{background-color:var(--eq-color-surface-subtle, {subtle})}}");
        sink.Css.Should().Contain(":hover{box-shadow:0 2px 8px 0");
        element.ClassName.Should().NotBeNullOrEmpty();
        // The pseudo classes ride the SAME element, after the base atomic set (TS parity order).
        var classes = element.ClassName!.Split(' ');
        classes.Should().OnlyHaveUniqueItems();

        // §10: hover never fires on touch — a tap's sticky emulated :hover must find no rule, so
        // EVERY hover rule lives behind the capability gate (focus-visible stays ungated:
        // keyboard focus exists everywhere).
        var hoverRules = System.Text.RegularExpressions.Regex.Count(sink.Css, ":hover\\{");
        var gatedRules = System.Text.RegularExpressions.Regex.Count(
            sink.Css, "@media \\(hover: hover\\)\\{\\.eq-[0-9a-z]+:hover\\{");
        hoverRules.Should().BeGreaterThan(0);
        gatedRules.Should().Be(hoverRules, "every hover rule rides its own gate");
    }

    [Fact]
    public void FocusDiff_UsesFocusVisible()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Focus = new StyleDiff { BorderWidth = 2, BorderColor = Theme.FocusRing },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        sink.Css.Should().Contain($":focus-visible{{border:2px solid var(--eq-color-focus, {TokenCss.Value(Theme.FocusRing)})}}");
    }

    [Fact]
    public void PseudoAndBaseVariants_OfTheSameDeclaration_AreDistinctClasses()
    {
        var sink = new StyleSink();
        var baseClass = sink.ClassFor("opacity", "0.5");
        var hoverClass = sink.ClassFor("opacity", "0.5", ":hover");
        baseClass.Should().NotBe(hoverClass, "the pseudo is part of the hash");
        sink.Css.Should().Contain($".{hoverClass}:hover{{opacity:0.5}}");
    }
}
