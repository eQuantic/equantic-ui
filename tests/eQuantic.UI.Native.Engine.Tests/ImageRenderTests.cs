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
