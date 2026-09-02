using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S6 on Photon, at PAINT: a box that declares <c>Transition</c> glides its colours, opacity,
/// transform and shadow to each new value under its own spec — and one that declares nothing
/// snaps, exactly as the same two boxes do in a browser. Before this the native side snapped
/// everything and said so in a fence.
/// </summary>
public class S6PaintTransitionTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Red = new(new Color(200, 0, 0, 255));
    private static readonly ColorToken Blue = new(new Color(0, 0, 200, 255));

    /// <summary>One box whose style a test mutates between frames — the shape a hover diff or a
    /// re-render produces, with the paint path kept stable by the unchanged tree.</summary>
    private sealed class Swatch : StatefulComponent
    {
        public ColorToken Fill = Red;
        public float? Opacity;
        public Transform2D? Transform;
        public int Elevation;
        public TransitionSpec? Transition;

        public void Change(Action mutate) => SetState(mutate);

        public override VisualNode Build(ComponentContext context) => new Box(new BoxStyle
        {
            Width = 100, Height = 40, Background = Fill, Opacity = Opacity,
            Transform = Transform, Elevation = Elevation, Transition = Transition,
        });
    }

    private static (PhotonHost Host, Swatch Swatch) Mount(TransitionSpec? spec)
    {
        var swatch = new Swatch { Transition = spec };
        var host = new PhotonHost(swatch, Theme, ThemeMode.Light, 200, 100);
        host.RenderFrame(new DisplayListBuilder(), 0);
        return (host, swatch);
    }

    private static DrawCommand[] Frame(PhotonHost host, float timeMs)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        return builder.Build().Commands.ToArray();
    }

    private static Color FillColor(DrawCommand[] commands) =>
        commands.Last(c => c.Kind == DrawCommandKind.FillRRect).Paint.Color;

    [Fact]
    public void ColoursGlide_UnderTheBoxsOwnSpec()
    {
        var (host, swatch) = Mount(TransitionSpec.Colors(100));
        FillColor(Frame(host, 0)).Should().Be(Red.Light, "mounted at the target");

        swatch.Change(() => swatch.Fill = Blue);
        FillColor(Frame(host, 1000)).Should().Be(Red.Light, "the change starts from the shown colour");

        var mid = FillColor(Frame(host, 1050));
        mid.R.Should().BeInRange(1, 199, "red is on its way down");
        mid.B.Should().BeInRange(1, 199, "blue is on its way up");
        // Halfway in TIME on the standard curve is ~88% of the travel, like CSS.
        mid.B.Should().BeGreaterThan(150);

        FillColor(Frame(host, 1100)).Should().Be(Blue.Light, "100 ms elapsed — settled");
    }

    [Fact]
    public void ABoxWithoutATransition_Snaps()
    {
        var (host, swatch) = Mount(spec: null);
        swatch.Change(() => swatch.Fill = Blue);
        FillColor(Frame(host, 1000)).Should().Be(Blue.Light, "no spec is no glide, as on the web");
    }

    [Fact]
    public void AChannelLeftOutOfTheSpec_Snaps_WhileTheOnesInItGlide()
    {
        // Colours only: the opacity change must land immediately, the colour must not.
        var (host, swatch) = Mount(TransitionSpec.Colors(100));
        swatch.Change(() => { swatch.Fill = Blue; swatch.Opacity = 0.5f; });
        var frame = Frame(host, 1000);

        FillColor(frame).Should().Be(Red.Light, "the colour glide has not begun");
        frame.Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer && MathF.Abs(c.StrokeWidth - 0.5f) < 1e-4f,
            "opacity is not in the spec, so it snapped to 0.5 in the same frame (the layer's alpha rides StrokeWidth)");
    }

    [Fact]
    public void OpacityGlides_AndTheLayerExistsWhileFading()
    {
        var (host, swatch) = Mount(new TransitionSpec(StyleChannels.Opacity, DurationMs: 100));
        swatch.Change(() => swatch.Opacity = 0.2f);
        Frame(host, 1000);

        var layer = Frame(host, 1050).First(c => c.Kind == DrawCommandKind.BeginLayer);
        layer.StrokeWidth.Should().BeInRange(0.21f, 0.99f, "mid-fade the layer is neither opaque nor at the target");

        // Fading back to opaque: the target says "no layer", the glide still needs one until settled.
        swatch.Change(() => swatch.Opacity = null);
        Frame(host, 2000);
        Frame(host, 2050).Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer,
            "the guard reads the interpolated alpha, not the declared one");
        Frame(host, 2100).Should().NotContain(c => c.Kind == DrawCommandKind.BeginLayer, "settled at opaque");
    }

    [Fact]
    public void TransformGlides_ComponentByComponent()
    {
        var (host, swatch) = Mount(new TransitionSpec(StyleChannels.Transform, DurationMs: 100));
        swatch.Change(() => swatch.Transform = Transform2D.Translate(40));
        Frame(host, 1000);

        // The transform is not a command: the builder bakes the current matrix into every command
        // it emits, so the fill's own matrix carries the glide. M31 also carries whatever base
        // offset the node's placement contributes, so the glide is read RELATIVE to the frame in
        // which the change began (translation still 0).
        // Pick the swatch's OWN fill by its colour: with a transform in force the command order can
        // change (the layer and the root's background move around it), so "the last fill" is not it.
        float Tx(float t) => Frame(host, t).Last(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Red.Light).Transform.M31;
        var start = Tx(1000);
        (Tx(1050) - start).Should().BeInRange(1f, 39f, "the translation is mid-way");
        (Tx(1100) - start).Should().BeApproximately(40f, 0.01f, "settled at the declared translation");
    }

    [Fact]
    public void ShadowGrows_WhenElevationRises_AndFadesWhenItDrops()
    {
        var (host, swatch) = Mount(new TransitionSpec(StyleChannels.Shadow, DurationMs: 100));
        Frame(host, 0).Should().NotContain(c => c.Kind == DrawCommandKind.ShadowRRect, "level 0 has no shadow");

        swatch.Change(() => swatch.Elevation = 3);
        Frame(host, 1000);
        var target = Theme.Elevation(3);
        var mid = Frame(host, 1050).First(c => c.Kind == DrawCommandKind.ShadowRRect);
        mid.StrokeWidth.Should().BeInRange(0.01f, target.Blur - 0.01f, "the shadow is growing, not swapped in (blur rides StrokeWidth)");

        swatch.Change(() => swatch.Elevation = 0);
        Frame(host, 2000);
        Frame(host, 2050).Should().Contain(c => c.Kind == DrawCommandKind.ShadowRRect,
            "dropping to 0 FADES the shadow rather than removing it in one frame");
        Frame(host, 2100).Should().NotContain(c => c.Kind == DrawCommandKind.ShadowRRect, "gone once settled");
    }

    [Fact]
    public void ReducedMotion_SnapsEveryChannel()
    {
        var swatch = new Swatch { Transition = TransitionSpec.All(200) };
        var host = new PhotonHost(swatch, Theme, ThemeMode.Light, 200, 100) { ReducedMotion = true };
        host.RenderFrame(new DisplayListBuilder(), 0);

        swatch.Change(() => swatch.Fill = Blue);
        FillColor(Frame(host, 1000)).Should().Be(Blue.Light, "Reduce Motion means the change, not the journey");
    }
}
