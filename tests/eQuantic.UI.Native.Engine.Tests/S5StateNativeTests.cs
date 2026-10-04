using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec S5 on Photon: the hovered Box applies its Hover diff (fill swap here) — the same
/// declarative overlay the web realizes as a :hover rule, driven by the host's pointer tracking.</summary>
public class S5StateNativeTests
{
    [Fact]
    public void HoveredBox_AppliesItsHoverDiffFill()
    {
        var baseFill = new ColorToken(Color.FromRgb(0x11, 0x22, 0x33));
        var hoverFill = new ColorToken(Color.FromRgb(0xAA, 0xBB, 0xCC));
        var box = new Box(new BoxStyle
        {
            Width = 40, Height = 40,
            Background = baseFill,
            Hover = new StyleDiff { Background = hoverFill },
        });

        var host = new PhotonHost(box, PhotonTheme.Instance, ThemeMode.Light, 40, 40);

        var rest = new DisplayListBuilder();
        host.RenderFrame(rest);
        FillColors(rest.Build()).Should().Contain(baseFill.Resolve(ThemeMode.Light))
            .And.NotContain(hoverFill.Resolve(ThemeMode.Light));

        host.SetHovered(box);
        var hovered = new DisplayListBuilder();
        host.RenderFrame(hovered);
        FillColors(hovered.Build()).Should().Contain(hoverFill.Resolve(ThemeMode.Light),
            "the hovered Box swaps to its Hover diff fill");
    }

    // ---- #504: every member of the Hover diff applies, not the colours alone -------------------

    private static readonly ColorToken Fill = new(Color.FromRgb(0x11, 0x22, 0x33));
    private static readonly ColorToken Glow = new(Color.FromRgb(0x44, 0x88, 0xFF));

    /// <summary>One frame at rest, then one with the pointer on the box: the host's own tracking.</summary>
    private static (DisplayList AtRest, DisplayList Hovered) RestAndHovered(Box box)
    {
        var host = new PhotonHost(box, PhotonTheme.Instance, ThemeMode.Light, 40, 40);
        var rest = new DisplayListBuilder();
        host.RenderFrame(rest);
        host.SetHovered(box);
        var hovered = new DisplayListBuilder();
        host.RenderFrame(hovered);
        return (rest.Build(), hovered.Build());
    }

    private static DrawCommand FillOf(DisplayList list)
    {
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect) return command;
        throw new InvalidOperationException("the box painted no fill");
    }

    /// <summary>The blur of every shadow drawn, in order: the theme's levels and the custom specs
    /// differ in it, so the list says which shadows the frame drew.</summary>
    private static List<float> ShadowBlurs(DisplayList list)
    {
        var blurs = new List<float>();
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.ShadowRRect) blurs.Add(command.StrokeWidth);
        return blurs;
    }

    private static int Count(DisplayList list, DrawCommandKind kind)
    {
        var count = 0;
        foreach (var command in list.Commands)
            if (command.Kind == kind) count++;
        return count;
    }

    [Fact]
    public void AHoveredBox_MovesByItsHoverTransform()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) },
        }));

        var lifted = FillOf(hovered).Transform;
        var resting = FillOf(rest).Transform;
        (lifted.M32 - resting.M32).Should().Be(-2, "the card lifts 2dp under the pointer");
        lifted.M31.Should().Be(resting.M31);
    }

    /// <summary>The glide reads the effective style too: a lift declared with a Transition moves
    /// the box over the transition's duration, as the browser glides the :hover rule, rather than
    /// snapping to it.</summary>
    [Fact]
    public void AHoverLift_GlidesUnderTheBoxsTransition()
    {
        var box = new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Transition = new TransitionSpec(StyleChannels.Transform, 100),
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) },
        });
        var host = new PhotonHost(box, PhotonTheme.Instance, ThemeMode.Light, 40, 40);
        float LiftAt(float timeMs)
        {
            var builder = new DisplayListBuilder();
            host.RenderFrame(builder, timeMs);
            return FillOf(builder.Build()).Transform.M32;
        }

        var rest = LiftAt(0);
        host.SetHovered(box);
        (LiftAt(1000) - rest).Should().Be(0, "the glide starts from where the box is drawn");
        (LiftAt(1050) - rest).Should().BeInRange(-1.99f, -0.01f, "halfway in time, the box is on its way up");
        (LiftAt(1100) - rest).Should().Be(-2, "settled once the transition's 100 ms are over");
    }

    [Fact]
    public void AHoverTransform_ReplacesTheRestingOne()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Transform = Transform2D.Rotate(90),
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) },
        }));

        FillOf(rest).Transform.M11.Should().BeApproximately(0, 1e-5f, "a quarter turn at rest");
        FillOf(hovered).Transform.M11.Should().BeApproximately(1, 1e-5f,
            "the hover's transform replaces the resting one, as a CSS rule replaces transform");
    }

    [Fact]
    public void AHoveredBox_RaisesItsElevation_AndKeepsItsGlow()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Elevation = 1,
            Shadow = new ShadowSpec(0, 24, 0, Glow),
            Hover = new StyleDiff { Elevation = 3 },
        }));

        ShadowBlurs(rest).Should().Equal(PhotonTheme.Instance.Elevation(1).Blur, 24);
        ShadowBlurs(hovered).Should().Equal(PhotonTheme.Instance.Elevation(3).Blur, 24);
    }

    [Fact]
    public void AHoversShadows_ReplaceTheBasesCustomShadows()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Elevation = 1,
            Shadow = new ShadowSpec(0, 24, 0, Glow),
            Shadows = [new ShadowSpec(2, 4, 0, Glow)],
            Hover = new StyleDiff { Shadows = [new ShadowSpec(4, 32, 2, Glow)] },
        }));

        ShadowBlurs(rest).Should().Equal(PhotonTheme.Instance.Elevation(1).Blur, 24, 4);
        ShadowBlurs(hovered).Should().Equal(PhotonTheme.Instance.Elevation(1).Blur, 32);
    }

    [Fact]
    public void AHoveredBox_FadesByItsHoverOpacity()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Hover = new StyleDiff { Opacity = 0.5f },
        }));

        Count(rest, DrawCommandKind.BeginLayer).Should().Be(0);
        var layer = hovered.Commands.ToArray().Single(c => c.Kind == DrawCommandKind.BeginLayer);
        layer.StrokeWidth.Should().Be(0.5f, "the layer's alpha is the hover's opacity");
    }

    [Fact]
    public void AHoveredBox_BlursItsBackdrop()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Hover = new StyleDiff { BackdropBlur = 8 },
        }));

        Count(rest, DrawCommandKind.BackdropBlur).Should().Be(0);
        Count(hovered, DrawCommandKind.BackdropBlur).Should().Be(1);
    }

    [Fact]
    public void AHoveredBox_PaintsItsHoverGradient()
    {
        var (rest, hovered) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Hover = new StyleDiff { Gradient = new LinearGradient(Fill, Glow) },
        }));

        rest.Commands.ToArray().Should().NotContain(c => c.Paint.Kind == PaintKind.LinearGradient);
        hovered.Commands.ToArray().Should().Contain(c => c.Paint.Kind == PaintKind.LinearGradient);
    }

    /// <summary>A shadow with no offset, blur or spread draws nothing; the web leaves it out of its
    /// list, where it was once a <c>none</c> that made CSS drop the whole declaration.</summary>
    [Fact]
    public void AShadowWithNoGeometry_IsNotDrawn()
    {
        var (rest, _) = RestAndHovered(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Shadow = new ShadowSpec(0, 0, 0, Glow),
            Shadows = [new ShadowSpec(0, 0, 0, Glow), new ShadowSpec(2, 4, 0, Glow)],
        }));

        ShadowBlurs(rest).Should().Equal(4);
    }

    private static List<Color> FillColors(DisplayList list)
    {
        var colors = new List<Color>();
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect)
                colors.Add(command.Paint.Color);
        return colors;
    }
}
