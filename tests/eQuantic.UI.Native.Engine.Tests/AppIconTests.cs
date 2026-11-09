using eQuantic.UI.Codegen;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The app icon declared in C# — a tree, rendered by the same engine that draws the app, into the
/// catalog its platform installs from. The sample ships a designer's artwork instead, which is the
/// other half of the same contract; this is what keeps the drawn half honest.
/// </summary>
public class AppIconTests
{
    private sealed class BrandIcon : IAppIcon
    {
        public VisualNode Build(ComponentContext context, AppIconUse use)
        {
            var brand = context.Theme.Colors(Variant.Primary);
            var field = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = brand.Base,
            });

            var stack = new Stack();
            stack.Add(field);
            stack.Add(new Text("W", TypeRole.Display, brand.OnBase).Centered());
            return stack;
        }
    }

    [Fact]
    public void AnIconFillsItsCanvasEdgeToEdge_AndComesOutOpaque()
    {
        // Every platform masks the icon and composites it against whatever the launcher is showing,
        // so a corner that is not painted is a corner that shows the wallpaper through it.
        const int size = 128;
        const float canvas = 64f;
        var theme = PhotonTheme.Instance;
        var host = new PhotonHost(new BrandIcon().Build(new ComponentContext(theme), AppIconUse.Launcher),
            theme, ThemeMode.Light, canvas, canvas)
        {
            RenderScale = size / canvas,
        };

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(size, size);
        backend.Render(builder.Build(), surface);

        var rgba = new byte[size * size * 4];
        surface.ReadPixelsSrgb(rgba);

        var brand = theme.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        Pixel(rgba, size, 0, 0).Should().Be((brand.R, brand.G, brand.B), "the top-left corner is painted");
        Pixel(rgba, size, size - 1, size - 1).Should().Be((brand.R, brand.G, brand.B));
        Pixel(rgba, size, size / 2, size / 2).Should().NotBe((brand.R, brand.G, brand.B),
            "the mark sits in the middle of the field");
    }

    /// <summary>
    /// The tab's cut as the build draws it (#1240): the app's own artwork, drawn by the portable
    /// rasterizer where a missing one drew the placeholder box, on a transparent backdrop, so what the
    /// artwork leaves empty stays empty.
    /// </summary>
    [Fact]
    public void ASmallCut_IsDrawnFromItsArtwork_OnATransparentBackdrop()
    {
        const int size = 128;
        const float canvas = 64f;
        var blue = new Color(30, 80, 220, 255);
        var disc = new VectorDrawing(0, 0, 10, 10,
            [new VectorShape("M5 1A4 4 0 1 0 5 9A4 4 0 1 0 5 1Z", VectorPaint.Solid(blue))]);
        var theme = PhotonTheme.Instance;
        var host = new PhotonHost(new Drawing(disc, SizeValue.Fill), theme, ThemeMode.Light, canvas, canvas)
        {
            RenderScale = size / canvas,
            IconRasterizer = new PortableIconRasterizer(),
            Backdrop = Color.Transparent,
        };

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(size, size);
        backend.Render(builder.Build(), surface);
        var rgba = new byte[size * size * 4];
        surface.ReadPixelsSrgb(rgba);

        rgba[3].Should().Be(0, "the corner is outside the disc, and the backdrop is nothing");
        var centre = (size / 2 * size + size / 2) * 4;
        rgba[centre + 3].Should().Be(255);
        Pixel(rgba, size, size / 2, size / 2).Should().Be((blue.R, blue.G, blue.B), "the artwork's own colour, not the placeholder's");
    }

    private static (byte R, byte G, byte B) Pixel(byte[] rgba, int stride, int x, int y)
    {
        var index = (y * stride + x) * 4;
        return (rgba[index], rgba[index + 1], rgba[index + 2]);
    }
}
