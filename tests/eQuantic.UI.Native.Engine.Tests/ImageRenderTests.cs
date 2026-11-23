using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>W4 images: the Rgba8 texture path — platform decode via IImageLoader, fit math in the
/// realizer, texel color (not tint) on the NORMATIVE Reference. No loader → placeholder box.</summary>
public class ImageRenderTests
{
    private sealed class FakeLoader : IImageLoader
    {
        // 2×1: left red, right transparent — enough to prove color and alpha survive.
        public RgbaImage? Load(string source) => source == "ok"
            ? new RgbaImage(2, 1, new byte[] { 255, 0, 0, 255, 0, 0, 0, 0 })
            : null;
    }

    [Fact]
    public void RgbaTexture_DrawsTexelColor_OnTheReference()
    {
        var texture = TextureData.Rgba(2, 1, new byte[] { 255, 0, 0, 255, 0, 0, 0, 0 });
        var builder = new DisplayListBuilder();
        builder.Clear(new Color(0, 0, 0, 255));
        builder.Texture(new Rect(0, 0, 2, 1), new Color(255, 255, 255, 255), texture);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(2, 1);
        backend.Render(builder.Build(), surface);
        var rgba = new byte[8];
        surface.ReadPixelsSrgb(rgba);
        rgba[0].Should().BeGreaterThan(200, "the texel's RED draws, not the tint");
        rgba[1].Should().BeLessThan(30);
        rgba[4].Should().Be(0, "the transparent texel leaves the clear");
    }

    [Fact]
    public void Image_ContainFits_AndFailedDecodeFallsBack()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Image("ok", 100, 100, ImageFit.Contain));
        row.Add(new Image("missing", 40, 40));

        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 360, 200)
        {
            ImageLoader = new FakeLoader(),
        };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        var list = builder.Build();

        list.Textures.Should().HaveCount(1, "one decoded image; the failed one draws the placeholder");
        var command = default(DrawCommand);
        foreach (var c in list.Commands) if (c.Kind == DrawCommandKind.Texture) command = c;
        // Contain on a 2×1 source in a 100×100 slot: full width, height 50, centered vertically.
        command.Shape.Rect.Width.Should().Be(100);
        command.Shape.Rect.Height.Should().Be(50);
        command.Shape.Rect.Y.Should().Be(25);
    }

    /// <summary>The destination of the one texture a frame with <paramref name="image"/> draws.</summary>
    private static Rect DrawnAt(Image image)
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(image);
        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 400, 400) { ImageLoader = new FakeLoader() };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        var list = builder.Build();
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.Texture) return command.Shape.Rect;
        throw new InvalidOperationException("no texture drawn");
    }

    [Fact]
    public void AFocalPoint_HoldsThePicturesPointAtTheSameFractionOfTheBox()
    {
        // A 2x1 picture covering a round 220 frame is 440 wide: 220 of overflow. Centred, it starts at
        // -110; held at 0.3 its point at 30% stands at 30% of the box, so it starts at -66 (#1785).
        DrawnAt(new Image("ok", 220, 220) { CornerRadius = new CornerRadii(110) }).X.Should().Be(-110);
        var held = DrawnAt(new Image("ok", 220, 220) { FocalPoint = new Point(0.3f, 0.7f), CornerRadius = new CornerRadii(110) });

        held.X.Should().BeApproximately(-66f, 0.01f);
        held.Width.Should().Be(440);
        (0.3f * held.Width + held.X).Should().BeApproximately(0.3f * 220, 0.01f, "the picture's 30% stands at the box's 30%");
    }

    [Fact]
    public void AZoom_DrawsCloserAroundTheFocalPoint_AndStillCoversTheBox()
    {
        var drawn = DrawnAt(new Image("ok", 220, 220) { FocalPoint = new Point(0.3f, 0.7f), Zoom = 1.5f });

        drawn.Width.Should().BeApproximately(660f, 0.01f, "the 440 of the fit, half as large again");
        drawn.Height.Should().BeApproximately(330f, 0.01f);
        (0.3f * drawn.Width + drawn.X).Should().BeApproximately(0.3f * 220, 0.01f, "the focal point keeps its place");
        (0.7f * drawn.Height + drawn.Y).Should().BeApproximately(0.7f * 220, 0.01f);
        drawn.X.Should().BeLessThanOrEqualTo(0);
        drawn.Right.Should().BeGreaterThanOrEqualTo(220, "the frame never shows past the picture's edge");
        drawn.Y.Should().BeLessThanOrEqualTo(0);
        drawn.Bottom.Should().BeGreaterThanOrEqualTo(220);
    }

    [Theory]
    [InlineData(-0.1f, 0.5f, 1f)]
    [InlineData(0.5f, 1.1f, 1f)]
    [InlineData(0.5f, 0.5f, 0.9f)]
    [InlineData(0.5f, 0.5f, float.PositiveInfinity)]
    public void AFocalPointOffThePicture_OrAZoomBelowItsFit_IsRefused(float x, float y, float zoom)
    {
        var build = () => new Image("ok", 220, 220) { FocalPoint = new Point(x, y), Zoom = zoom };

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FixedImage_RefusesColumnStretch()
    {
        // The layout contract: stretch fills AUTO cross sizes only — an Image's size is always
        // explicit (the constructor demands it), so a stretching Column must not widen it.
        var column = new Column(gap: 0) { Width = SizeValue.Fill }; // Cross defaults to Stretch
        column.Add(new Image("x", 240, 160));

        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 800, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());
        frame.Root.Children[0].Bounds.Width.Should().Be(240, "explicit sizes hug under Stretch");
        frame.Root.Children[0].Bounds.Height.Should().Be(160);
    }
}
