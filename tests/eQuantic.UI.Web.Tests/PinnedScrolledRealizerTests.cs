using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A pinned header's <c>ScrolledStyle</c> on the web (#506). It wrote four members of seven and a
/// border alone, so an <c>Elevation</c>, the member a developer reaches for first (Flutter's
/// <c>scrolledUnderElevation</c>), a <c>Gradient</c>, a <c>Transform</c> and the <c>Shadows</c> did
/// nothing. Every member goes through the builder a box's states use now, over
/// <see cref="Pinned.ScrolledBase"/>, and the rules select on the header's own
/// <c>data-eq-scrolled</c>, which the runtime sets from the surface the header pins to.
/// </summary>
public class PinnedScrolledRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    /// <summary>A colour as the sheet writes it: through the theme's variables.</summary>
    private static string Css(string value) => ThemeVarMap.For(Theme).Rewrite(value);

    private static string ScrolledSheet(StyleDiff scrolled)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Pinned(new Box(new BoxStyle { Height = 40 })) { ScrolledStyle = scrolled }, Theme,
            styles: sink).Render();
        return sink.Css;
    }

    [Fact]
    public void EveryMemberOfTheDiff_IsWritten()
    {
        var css = ScrolledSheet(new StyleDiff
        {
            Background = Theme.Surface,
            Elevation = 2,
            Opacity = 0.9f,
            BackdropBlur = 24,
            Gradient = new LinearGradient(Theme.Surface, Theme.SurfaceSubtle),
            Transform = Transform2D.Translate(0, -2),
            Shadows = [new ShadowSpec(0, 12, 0, Theme.FocusRing)],
        });

        css.Should().Contain("[data-eq-scrolled]{background-color:");
        css.Should().Contain("[data-eq-scrolled]{opacity:0.9}");
        css.Should().Contain("[data-eq-scrolled]{-webkit-backdrop-filter:blur(24px);backdrop-filter:blur(24px)}");
        css.Should().Contain("[data-eq-scrolled]{background-image:linear-gradient(");
        css.Should().Contain("[data-eq-scrolled]{transform:");
        // The elevation's shadow and the custom one, in one list behind the ring's slot.
        var shadow = css.Split('}').Single(rule => rule.Contains("[data-eq-scrolled]{box-shadow:"));
        shadow.Should().Contain(Css(TokenCss.Shadow(Theme.Elevation(2))))
            .And.Contain(Css(TokenCss.Shadow(new ShadowSpec(0, 12, 0, Theme.FocusRing))));
    }

    [Fact]
    public void TheBorder_IsTheHairlineAlongTheBottomEdge()
    {
        var css = ScrolledSheet(new StyleDiff { BorderWidth = 1, BorderColor = Theme.Border });

        css.Should().Contain("[data-eq-scrolled]{border-width:0 0 1px 0}");
        css.Should().Contain("[data-eq-scrolled]{border-style:solid}");
        css.Should().Contain($"[data-eq-scrolled]{{border-color:{Css(TokenCss.Value(Theme.Border))}}}");
        css.Should().NotContain("{border:", "the header's border is its bottom edge, never all four");
    }

    [Fact]
    public void TheRules_SelectOnTheHeaderItself()
    {
        var css = ScrolledSheet(new StyleDiff { Background = Theme.Surface });

        css.Should().NotContain("html.eq-scrolled",
            "one class for the whole page cannot tell a header in a scrolling panel from one on the page");
    }

    /// <summary>The same builder writes a box's states, and the fix reaches them: a hover border on a
    /// box that draws one edge used to draw all four.</summary>
    [Fact]
    public void AHoverBorder_FollowsTheEdgesTheBoxDraws()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Box(new BoxStyle
        {
            Width = 40, Height = 40, BorderWidth = 1, BorderColor = Theme.Border, BorderSides = BorderSides.Bottom,
            Hover = new StyleDiff { BorderWidth = 2, BorderColor = Theme.FocusRing },
        }), Theme, styles: sink).Render();

        sink.Css.Should().Contain(":hover{border-width:0 0 2px 0}");
        sink.Css.Should().NotContain(":hover{border:");
    }

    /// <summary>A width alone draws in the base's colour, as Photon draws it: it wrote nothing.</summary>
    [Fact]
    public void AHoverBorderWidthAlone_DrawsInTheBasesColour()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Box(new BoxStyle
        {
            Width = 40, Height = 40, BorderWidth = 1, BorderColor = Theme.Border,
            Hover = new StyleDiff { BorderWidth = 2 },
        }), Theme, styles: sink).Render();

        sink.Css.Should().Contain($":hover{{border:2px solid {Css(TokenCss.Value(Theme.Border))}}}");
    }
}
