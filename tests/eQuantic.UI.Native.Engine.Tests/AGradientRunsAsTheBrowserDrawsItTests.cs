using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A box's gradient on Photon, against the browser's own pixels for the CSS the web realizer writes for
/// the same box (#1955, #1956). The oracle was measured in Chromium 156: each CSS gradient drawn into a
/// canvas through an SVG image and read back with <c>getImageData</c>, over white.
/// <list type="bullet">
/// <item>A corner's gradient line runs through the other two corners at its middle (CSS's "magic
/// corners"), where Photon ran it corner to corner: on a box that is not square, the top-right corner sat
/// 73.5% of the way along.</item>
/// <item>The stops mix premultiplied, where Photon mixed them straight: red into transparent read
/// (128, 0, 0, 128) at its middle, where the browser keeps red, (255, 0, 0, 127).</item>
/// </list>
/// Each case runs on the Reference backend, and on Metal and Vulkan where the host has them, so the
/// shader and the CPU are held to the same pixels.
/// </summary>
public class AGradientRunsAsTheBrowserDrawsItTests
{
    private const int Tolerance = 2;

    private static readonly ColorToken Black = new(Color.Black);
    private static readonly ColorToken White = new(Color.White);

    public enum Target { Reference, Metal, Vulkan }

    // `linear-gradient(to bottom right, black, white)`, measured in Chromium 156.
    [SkippableTheory]
    [InlineData(Target.Reference)]
    [InlineData(Target.Metal)]
    [InlineData(Target.Vulkan)]
    public void ToBottomRight_OnAWideBox_PutsItsMiddleThroughTheOtherTwoCorners(Target target)
    {
        var pixels = Draw(target, 400, 240, new LinearGradient(Black, White, GradientDirection.ToBottomRight));

        Gray(pixels, 400, 0, 0).Should().BeInRange(0, Tolerance, "the top-left corner is the first stop");
        Gray(pixels, 400, 399, 0).Should().BeInRange(127 - Tolerance, 127 + Tolerance,
            "the browser reads 127 at the top-right corner: the middle of the line runs through it");
        Gray(pixels, 400, 0, 239).Should().BeInRange(128 - Tolerance, 128 + Tolerance, "and 128 at the bottom-left one");
        Gray(pixels, 400, 200, 120).Should().BeInRange(127 - Tolerance, 127 + Tolerance, "and 127 at the centre");
        Gray(pixels, 400, 300, 0).Should().BeInRange(96 - Tolerance, 96 + Tolerance, "and 96 at (300, 0)");
        Gray(pixels, 400, 399, 239).Should().BeInRange(255 - Tolerance, 255, "the bottom-right corner is the last stop");
    }

    [SkippableTheory]
    [InlineData(Target.Reference)]
    [InlineData(Target.Metal)]
    [InlineData(Target.Vulkan)]
    public void ToBottomRight_OnATallBox_PutsItsMiddleThroughTheOtherTwoCorners(Target target)
    {
        var pixels = Draw(target, 240, 400, new LinearGradient(Black, White, GradientDirection.ToBottomRight));

        Gray(pixels, 240, 239, 0).Should().BeInRange(127 - Tolerance, 127 + Tolerance, "the browser reads 127 at the top-right corner");
        Gray(pixels, 240, 0, 399).Should().BeInRange(128 - Tolerance, 128 + Tolerance, "and 128 at the bottom-left one");
        Gray(pixels, 240, 0, 300).Should().BeInRange(96 - Tolerance, 96 + Tolerance, "and 96 at (0, 300)");
    }

    // `linear-gradient(to bottom left, black, white)`, measured in Chromium 156.
    [SkippableTheory]
    [InlineData(Target.Reference)]
    [InlineData(Target.Metal)]
    [InlineData(Target.Vulkan)]
    public void ToBottomLeft_OnAWideBox_IsTheSameLineMirrored(Target target)
    {
        var pixels = Draw(target, 400, 240, new LinearGradient(Black, White, GradientDirection.ToBottomLeft));

        Gray(pixels, 400, 399, 0).Should().BeInRange(0, Tolerance, "the top-right corner is the first stop");
        Gray(pixels, 400, 0, 0).Should().BeInRange(127 - Tolerance, 127 + Tolerance, "the browser reads 127 at the top-left corner");
        Gray(pixels, 400, 399, 239).Should().BeInRange(128 - Tolerance, 128 + Tolerance, "and 128 at the bottom-right one");
        Gray(pixels, 400, 100, 0).Should().BeInRange(95 - Tolerance, 95 + Tolerance, "and 95 at (100, 0)");
        Gray(pixels, 400, 0, 239).Should().BeInRange(255 - Tolerance, 255, "the bottom-left corner is the last stop");
    }

    // `linear-gradient(to right, red, transparent)` over nothing, measured in Chromium 156. Over nothing, so
    // the readback is the gradient's own straight colour and alpha: over white, Photon blends in linear light
    // and the browser in sRGB, which sets every translucent fill apart, a gradient or not.
    [SkippableTheory]
    [InlineData(Target.Reference)]
    [InlineData(Target.Metal)]
    [InlineData(Target.Vulkan)]
    public void AColourIntoTransparent_KeepsItsColour_AsTheBrowserMixesIt(Target target)
    {
        var pixels = DrawAlone(target, 201, 4, new LinearGradient(
            new ColorToken(Color.FromRgb(255, 0, 0)), new ColorToken(Color.Transparent)));

        Near(Straight(pixels, 201, 50, 1), (255, 0, 0, 191), "red at three quarters of its alpha");
        Near(Straight(pixels, 201, 100, 1), (255, 0, 0, 127),
            "red at half its alpha, where a straight mix towards transparent black reads (128, 0, 0, 128)");
        Near(Straight(pixels, 201, 150, 1), (255, 0, 0, 64), "red at a quarter of its alpha");
    }

    // `linear-gradient(to right, rgba(0, 0, 255, 0.5), rgb(255, 0, 0))` over nothing, measured in Chromium 156.
    [SkippableTheory]
    [InlineData(Target.Reference)]
    [InlineData(Target.Metal)]
    [InlineData(Target.Vulkan)]
    public void AHalfTransparentStopBesideAnOpaqueOne_WeighsByItsAlpha(Target target)
    {
        var pixels = DrawAlone(target, 201, 4, new LinearGradient(
            new ColorToken(new Color(0, 0, 255, 128)), new ColorToken(Color.FromRgb(255, 0, 0))));

        Near(Straight(pixels, 201, 100, 1), (170, 0, 85, 192),
            "the browser weighs blue by its half alpha, where a straight mix reads (128, 0, 128, 191)");
    }

    /// <summary>
    /// An artwork's runs are SVG paint servers on the web, and the browser mixes those straight: an SVG
    /// <c>linearGradient</c> from red into transparent black reads (129, 0, 0, 127) at its middle over nothing,
    /// in Chromium 156. So the artwork's paint keeps the straight mix.
    /// </summary>
    [Fact]
    public void AnArtworksRun_MixesStraight_AsTheBrowserMixesAnSvgOne()
    {
        var run = new VectorGradient(0, 0, 1, 0, 0, false,
            [new VectorStop(0, Color.FromRgb(255, 0, 0)), new VectorStop(1, new Color(0, 0, 0, 0))]);
        var shape = new VectorShape("M0 0 H100 V10 H0 Z", VectorPaint.Gradients(VectorPaintKind.LinearGradient, run));
        var artwork = new VectorDrawing(0, 0, 100, 10, [shape]);

        var paint = new ArtworkPaint(artwork, new Rect(0, 0, 100, 10)).For(shape, shape.Fill, Color.Black);

        paint.Interpolation.Should().Be(GradientInterpolation.Straight);
        var middle = paint.ColorAt(new Point(50, 5));
        Near((middle.R, middle.G, middle.B, middle.A), (129, 0, 0, 127), "the browser's SVG mix, straight");
    }

    private static DisplayList Frame(int width, int height, LinearGradient gradient)
    {
        var page = new Box(new BoxStyle { Background = White },
            new Box(new BoxStyle { Width = width, Height = height, Gradient = gradient }));
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, width, height) { SmoothScroll = false };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static byte[] Draw(Target target, int width, int height, LinearGradient gradient) =>
        Render(target, width, height, Frame(width, height, gradient));

    private static byte[] Render(Target target, int width, int height, DisplayList list)
    {
        switch (target)
        {
            case Target.Metal: Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host."); break;
            case Target.Vulkan: Skip.IfNot(VulkanBackend.IsSupported, "No Vulkan loader/device on this host."); break;
        }
        using IRenderBackend backend = target switch
        {
            Target.Metal => new MetalBackend(),
            Target.Vulkan => new VulkanBackend(),
            _ => new ReferenceBackend(),
        };
        using var surface = backend.CreateSurface(width, height);
        backend.Render(list, surface);
        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    /// <summary>
    /// The realizer's own gradient command for the box, drawn alone on a surface of nothing, so what reads
    /// back is the paint's colour and alpha and no blend with anything under it.
    /// </summary>
    private static byte[] DrawAlone(Target target, int width, int height, LinearGradient gradient)
    {
        var fade = Frame(width, height, gradient).Commands.ToArray()
            .Single(c => c.Paint.Kind == PaintKind.LinearGradient);
        var alone = new DisplayListBuilder();
        alone.Replay(fade, Matrix2D.Identity);
        return Render(target, width, height, alone.Build());
    }

    private static (int R, int G, int B, int A) Straight(byte[] pixels, int width, int x, int y)
    {
        var i = (y * width + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static void Near((int R, int G, int B, int A) actual, (int R, int G, int B, int A) browser, string because)
    {
        Math.Abs(actual.R - browser.R).Should().BeLessThanOrEqualTo(Tolerance, $"{because} (R, read {actual})");
        Math.Abs(actual.G - browser.G).Should().BeLessThanOrEqualTo(Tolerance, $"{because} (G, read {actual})");
        Math.Abs(actual.B - browser.B).Should().BeLessThanOrEqualTo(Tolerance, $"{because} (B, read {actual})");
        Math.Abs(actual.A - browser.A).Should().BeLessThanOrEqualTo(Tolerance, $"{because} (A, read {actual})");
    }

    private static (int R, int G, int B) Pixel(byte[] pixels, int width, int x, int y)
    {
        var i = (y * width + x) * 4;
        pixels[i + 3].Should().Be(255, "the page under the gradient is opaque white");
        return (pixels[i], pixels[i + 1], pixels[i + 2]);
    }

    private static int Gray(byte[] pixels, int width, int x, int y) => Pixel(pixels, width, x, y).G;

}
