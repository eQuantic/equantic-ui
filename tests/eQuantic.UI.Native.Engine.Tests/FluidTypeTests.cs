using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A size that follows the window (#652): the handoff's <c>clamp(34px, 4.2vw, 54px)</c>, measured and
/// painted on Photon at the window the frame lays out against, between its floor and its ceiling.
/// </summary>
public class FluidTypeTests
{
    private static readonly TypeStyle Display =
        TypeStyle.OfSize(40, FontWeight.Bold).WithFluidSize(34, 4.2f, 54);

    [Theory]
    [InlineData(400, 34)]     // 16.8 is under the floor
    [InlineData(1000, 42)]    // 4.2% of the window
    [InlineData(2000, 54)]    // 84 is over the ceiling
    public void TheSize_IsTheWindowsShare_BetweenTheFloorAndTheCeiling(float window, float size)
    {
        Display.AtWindow(window).Size.Should().Be(size);
    }

    [Fact]
    public void TheLineBox_KeepsTheStylesRatio_AtEverySize()
    {
        Display.AtWindow(400).LineHeight.Should().Be(42.5f, "34 × 1.25");
        Display.AtWindow(2000).LineHeight.Should().Be(67.5f, "54 × 1.25");
    }

    [Fact]
    public void TheTracking_FollowsTheSize()
    {
        var tight = new TypeStyle(54, 53, FontWeight.ExtraBold, -1.89f, 1.3f).WithFluidSize(34, 4.2f, 54);

        tight.AtWindow(400).Tracking.Should().BeApproximately(-1.19f, 0.001f, "-0.035em of 34");
        tight.AtWindow(2000).Tracking.Should().BeApproximately(-1.89f, 0.001f);
    }

    [Fact]
    public void ASizeInDp_GivesTheFluidSizeWayAndStaysPut()
    {
        Display.WithSize(20).Fluid.Should().BeNull();
        Display.WithSize(20).AtWindow(2000).Size.Should().Be(20);
        TypeStyle.OfSize(16, FontWeight.Regular).AtWindow(2000).Size.Should().Be(16);
    }

    [Fact]
    public void WithoutAWindow_TheStyleHoldsItsCeiling()
    {
        // What a target that knows no window (an email) sets.
        Display.Size.Should().Be(54);
        Display.LineHeight.Should().Be(67.5f);
    }

    [Theory]
    [InlineData(0, 4, 54, "min")]
    [InlineData(34, 0, 54, "percentOfWindow")]
    [InlineData(34, 4, 30, "max")]
    public void ASizeThatCannotBe_IsRefused(float min, float percent, float max, string parameter)
    {
        var act = () => Display.WithFluidSize(min, percent, max);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameter);
    }

    [Theory]
    [InlineData(400, 42.5f)]
    [InlineData(2000, 67.5f)]
    public void Photon_MeasuresAtTheWindowItLaysOutAgainst(float window, float lineHeight)
    {
        var heading = new Text("Fala com Portugal", TypeRole.BodyM) { StyleOverride = Display };

        var node = LayoutEngine.Layout(heading, window, 800,
            new LayoutContext(PhotonTheme.Instance, new SizeEcho()));

        node.Bounds.Height.Should().Be(lineHeight);
    }

    [Fact]
    public void Photon_PaintsAtTheSameSizeItMeasured()
    {
        var echo = new SizeEcho();
        var host = new PhotonHost(new Heading(), PhotonTheme.Instance, ThemeMode.Light, 1000, 400, measurer: echo)
        {
            TextRasterizer = echo,
        };

        host.RenderFrame(new DisplayListBuilder());

        echo.Rasterized.Should().ContainSingle().Which.Size.Should().Be(42);
    }

    /// <summary>A field set in a fluid role is measured at the window too, as the text beside it is.</summary>
    [Fact]
    public void Photon_MeasuresAFieldInAFluidRole_AtTheWindow()
    {
        var theme = new FluidRoleTheme(PhotonTheme.Instance, Display);
        var field = new TextEntry("", _ => { }) { Role = TypeRole.BodyL };

        var node = LayoutEngine.Layout(field, 400, 800, new LayoutContext(theme, new SizeEcho()));

        node.Bounds.Height.Should().BeLessThan(67.5f, "a field at the ceiling would be the 54dp line box");
    }

    /// <summary>Photon's theme with BodyL set in a fluid style.</summary>
    private sealed class FluidRoleTheme(IAppTheme inner, TypeStyle bodyL) : IAppTheme
    {
        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => role == TypeRole.BodyL ? bodyL : inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }

    private sealed class Heading : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text("Fala com Portugal", TypeRole.BodyM) { StyleOverride = Display };
    }

    /// <summary>One line, as tall as the style's line box: the size reaches the measurer unchanged.</summary>
    private sealed class SizeEcho : ITextMeasurer, ITextRasterizer
    {
        public readonly List<TypeStyle> Rasterized = new();

        public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines) =>
            new(content.Length * style.Size / 2, style.LineHeight, style.LineHeight,
                [new MeasuredLine(content.Length * style.Size / 2, false)]);

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth,
            int maxLines, float scale, TextAlignment align)
        {
            Rasterized.Add(style);
            return new TextRaster(1, 1, [255]);
        }
    }
}
