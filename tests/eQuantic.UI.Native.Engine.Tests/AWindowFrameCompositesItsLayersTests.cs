using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The path a WINDOW takes, on the real devices (#1952). It opened its own pass and encoded the
/// display list straight into it, so a group's opacity blended each of the group's draws on its own,
/// and its overlaps showed through, and a backdrop drew no blur. The offscreen path the goldens take
/// was right, which is why no golden saw either.
/// <para>
/// On Metal the frame goes through <see cref="MetalBackend.RenderToDrawable"/> itself, with an
/// offscreen texture standing in for the drawable's, unwaited as a window's is. On Vulkan it goes
/// through <see cref="SwapchainFrame"/>, the frame <c>VulkanSwapchain.Present</c> draws through, over
/// an image of the device's own and with no semaphore, since a test has no window to acquire from.
/// Each is also compared with the same frame drawn offscreen: a window shows what the goldens see.
/// </para>
/// </summary>
public class AWindowFrameCompositesItsLayersTests
{
    private const int Side = 80;

    // Two opaque squares overlapping in the middle, in a group at half opacity, over white.
    private static readonly (int X, int Y) InOneSquare = (15, 15);
    private static readonly (int X, int Y) InBothSquares = (40, 40);

    // A backdrop blur in the middle of a page of sharp black and white stripes.
    private const int GlassRow = 40;
    private static readonly (int From, int To) InTheGlass = (28, 52);
    private static readonly (int From, int To) OutsideTheGlass = (2, 14);

    private static DisplayList AGroupAtHalfOpacity()
    {
        var builder = new DisplayListBuilder();
        builder.Clear(Color.White);
        builder.PushLayer(0.5f);
        builder.FillRect(new Rect(10, 10, 40, 40), Paint.Solid(new Color(200, 0, 0, 255)));
        builder.FillRect(new Rect(30, 30, 40, 40), Paint.Solid(new Color(200, 0, 0, 255)));
        builder.PopLayer();
        return builder.Build();
    }

    private static DisplayList ABackdropOverStripes()
    {
        var builder = new DisplayListBuilder();
        builder.Clear(Color.White);
        for (var x = 0; x < Side; x += 4)
            builder.FillRect(new Rect(x, 0, 2, Side), Paint.Solid(Color.Black));
        builder.BackdropBlur(new RRect(new Rect(20, 20, 40, 40), new CornerRadii(0)), 6);
        return builder.Build();
    }

    [SkippableFact]
    public void OnMetal_AGroupsOpacity_CompositesOnce()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        var (window, offscreen) = OnMetal(AGroupAtHalfOpacity());

        AssertTheGroupCompositedOnce(window);
        window.Should().Equal(offscreen, "a window shows what the goldens see");
    }

    [SkippableFact]
    public void OnMetal_ABackdrop_Blurs()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        var (window, offscreen) = OnMetal(ABackdropOverStripes());

        AssertTheGlassBlurred(window);
        window.Should().Equal(offscreen, "a window shows what the goldens see");
    }

    [SkippableFact]
    public void OnVulkan_AGroupsOpacity_CompositesOnce()
    {
        Skip.IfNot(VulkanBackend.IsSupported, "No Vulkan loader/device on this host.");
        var (window, offscreen) = OnVulkan(AGroupAtHalfOpacity());

        AssertTheGroupCompositedOnce(window);
        window.Should().Equal(offscreen, "a window shows what the goldens see");
    }

    [SkippableFact]
    public void OnVulkan_ABackdrop_Blurs()
    {
        Skip.IfNot(VulkanBackend.IsSupported, "No Vulkan loader/device on this host.");
        var (window, offscreen) = OnVulkan(ABackdropOverStripes());

        AssertTheGlassBlurred(window);
        window.Should().Equal(offscreen, "a window shows what the goldens see");
    }

    /// <summary>
    /// Composited once, the group is one surface at half opacity: where the squares overlap is the
    /// same colour as where only one is. Blended draw by draw, the overlap is the square at half
    /// opacity over itself at half opacity, three quarters of the way to red.
    /// </summary>
    private static void AssertTheGroupCompositedOnce(byte[] pixels)
    {
        var one = Pixel(pixels, InOneSquare);
        var both = Pixel(pixels, InBothSquares);
        one.G.Should().BeInRange(150, 200, "half of a red square over white keeps half of the white's green");
        foreach (var (channel, alone, overlapped) in new[] { ("R", one.R, both.R), ("G", one.G, both.G), ("B", one.B, both.B) })
            Math.Abs(alone - overlapped).Should().BeLessThanOrEqualTo(1,
                $"the overlap's {channel} is the group's, composited once, not the squares blended over each other " +
                $"(alone {one}, overlapped {both})");
    }

    /// <summary>
    /// Outside the glass the stripes are sharp, black beside white. Inside, the backdrop blurs them
    /// into a grey that hardly varies; skipped, as the windows skipped it, the glass is as sharp as
    /// the page.
    /// </summary>
    private static void AssertTheGlassBlurred(byte[] pixels)
    {
        var outside = Contrast(pixels, OutsideTheGlass);
        var inside = Contrast(pixels, InTheGlass);
        outside.Should().BeGreaterThan(200, "the page's stripes are sharp");
        inside.Should().BeLessThan(outside / 4, $"the glass blurs the stripes (contrast {inside} inside, {outside} outside)");
    }

    private static int Contrast(byte[] pixels, (int From, int To) columns)
    {
        int lowest = 255, highest = 0;
        for (var x = columns.From; x <= columns.To; x++)
        {
            var green = Pixel(pixels, (x, GlassRow)).G;
            lowest = Math.Min(lowest, green);
            highest = Math.Max(highest, green);
        }
        return highest - lowest;
    }

    private static (byte R, byte G, byte B, byte A) Pixel(byte[] pixels, (int X, int Y) at)
    {
        var i = (at.Y * Side + at.X) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static (byte[] Window, byte[] Offscreen) OnMetal(DisplayList frame)
    {
        using var backend = new MetalBackend();
        using var drawable = backend.CreateSurface(Side, Side);
        using var offscreen = backend.CreateSurface(Side, Side);
        var texture = ((MetalRenderTarget)((RhiSurface)drawable).Target).Texture;

        backend.RenderToDrawable(frame, texture, MetalDevice.PixelFormatRgba8UnormSrgb, drawable: IntPtr.Zero);
        // Waits, and the queue runs in order: the window's frame, committed unwaited, is drawn too.
        backend.Render(frame, offscreen);

        return (Read(drawable), Read(offscreen));
    }

    private static (byte[] Window, byte[] Offscreen) OnVulkan(DisplayList frame)
    {
        using var device = new VulkanDevice();
        using var renderer = new RhiRenderer(device);
        using var image = (VulkanRenderTarget)device.CreateRenderTarget(Side, Side);
        using var offscreen = device.CreateRenderTarget(Side, Side);

        renderer.Render(frame, new SwapchainFrame(device, image, acquired: 0, rendered: 0));
        renderer.Render(frame, offscreen);

        var window = new byte[Side * Side * 4];
        image.ReadPixelsSrgb(window);
        var drawn = new byte[Side * Side * 4];
        offscreen.ReadPixelsSrgb(drawn);
        return (window, drawn);
    }

    private static byte[] Read(IRenderSurface surface)
    {
        var pixels = new byte[Side * Side * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }
}
