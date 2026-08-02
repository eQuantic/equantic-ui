using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S6 style transitions (parity pair of the TS lowering's transitionValue): the channel →
/// property mapping and the emitted string are the LITERAL contract — SSR writes these declarations
/// and hydration re-derives them, so the two sides must agree byte-for-byte.
/// </summary>
public class S6TransitionRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string? TransitionOf(BoxStyle style) =>
        WebRealizer.Lower(new Primitives.Box(style), Theme).Style!.Transition;

    [Fact]
    public void NoTransition_EmitsNothing_TheChangeSnaps()
    {
        TransitionOf(new BoxStyle { Background = Theme.Surface }).Should().BeNull();
    }

    [Fact]
    public void Colors_CoversEveryColorProperty_IncludingTheGradientLayer()
    {
        TransitionOf(new BoxStyle { Transition = TransitionSpec.Colors(150) })
            .Should().Be(
                "background-color 150ms cubic-bezier(0.2, 0, 0, 1), "
                + "background-image 150ms cubic-bezier(0.2, 0, 0, 1), "
                + "border-color 150ms cubic-bezier(0.2, 0, 0, 1), "
                + "color 150ms cubic-bezier(0.2, 0, 0, 1), "
                + "fill 150ms cubic-bezier(0.2, 0, 0, 1), "
                + "stroke 150ms cubic-bezier(0.2, 0, 0, 1)");
    }

    [Fact]
    public void Channels_CombineInTheDeclaredOrder()
    {
        TransitionOf(new BoxStyle
        {
            Transition = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform, 200),
        }).Should().Be(
            "opacity 200ms cubic-bezier(0.2, 0, 0, 1), transform 200ms cubic-bezier(0.2, 0, 0, 1)");
    }

    [Fact]
    public void Filters_CoverBothTheElementAndBackdropBlur()
    {
        TransitionOf(new BoxStyle { Transition = new TransitionSpec(StyleChannels.Filters, 300) })
            .Should().Be(
                "filter 300ms cubic-bezier(0.2, 0, 0, 1), backdrop-filter 300ms cubic-bezier(0.2, 0, 0, 1)");
    }

    [Fact]
    public void Delay_RidesTheThirdSlot_OnlyWhenSet()
    {
        TransitionOf(new BoxStyle
        {
            Transition = new TransitionSpec(StyleChannels.Opacity, 100, 40) { Easing = Curve.Decelerate },
        }).Should().Be("opacity 100ms cubic-bezier(0, 0, 0, 1) 40ms");
    }

    [Fact]
    public void Size_IsTheMorphChannel()
    {
        TransitionOf(new BoxStyle { Transition = new TransitionSpec(StyleChannels.Size, 320) })
            .Should().StartWith("width 320ms").And.Contain("max-width 320ms");
    }

    [Fact]
    public void Transition_RidesTheAtomicPipeline()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(
            new Primitives.Box(new BoxStyle { Transition = new TransitionSpec(StyleChannels.Opacity, 200) }),
            Theme, 1f, sink);

        sink.Css.Should().Contain("{transition:opacity 200ms cubic-bezier(0.2, 0, 0, 1)}");
    }

    [Fact]
    public void Gradient_Via_LandsAsTheThirdStopAtItsPosition()
    {
        var element = WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Gradient = new LinearGradient(Theme.Surface, Theme.Background, GradientDirection.ToBottomRight)
            {
                Via = Theme.SurfaceSubtle,
                ViaPosition = 0.4f,
            },
        }), Theme);

        element.Style!.BackgroundImage.Should().Contain("40%",
            "the midpoint carries its own position, like the design system's from/via/to triples");
        element.Style.BackgroundImage.Should().StartWith("linear-gradient(to bottom right,");
    }

    [Fact]
    public void MaxLines_TwoOrMore_ClampsInsteadOfEllipsingOneLine()
    {
        // A grid of cards stays on one baseline only if the copy it does not control is clamped —
        // a registry description, a user's bio. (One line keeps the nowrap+ellipsis form.)
        var css = WebRealizer.Lower(new Primitives.Text("long", TypeRole.BodyM) { MaxLines = 2 }, Theme)
            .Style!.ToCssString();

        css.Should().Contain("display: -webkit-box")
            .And.Contain("-webkit-box-orient: vertical")
            .And.Contain("-webkit-line-clamp: 2")
            .And.Contain("overflow: hidden");
        css.Should().NotContain("white-space: nowrap");
    }

    [Fact]
    public void Gradient_WithoutVia_StaysTwoStop()
    {
        var element = WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Gradient = new LinearGradient(Theme.Surface, Theme.Background),
        }), Theme);

        element.Style!.BackgroundImage.Should().NotContain("%");
    }
}
