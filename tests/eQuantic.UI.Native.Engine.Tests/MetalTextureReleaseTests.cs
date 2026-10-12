using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The REAL Metal device, asked how much it has allocated. A counting fake shows that the renderer
/// calls <c>Dispose</c>; only the device shows that the texture went. On Metal that call was an empty
/// body, so every raster the renderer let go stayed allocated, and each one it uploaded again was one
/// more (#858).
/// </summary>
public class MetalTextureReleaseTests
{
    private const int Side = 256;                       // 256 x 256 RGBA: a quarter of a megabyte
    private const int Frames = 400;                     // 100 MB if none is released

    [SkippableFact]
    public void RastersNoFrameDrawsAnyMore_LeaveTheDevice()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        using var backend = new MetalBackend();
        using var surface = backend.CreateSurface(64, 64);

        // Warm: pipelines, the surface and the first rasters are allocated before the measure.
        for (var frame = 0; frame < 40; frame++) backend.Render(FrameWithANewRaster(), surface);
        var before = Allocated(backend);

        for (var frame = 0; frame < Frames; frame++) backend.Render(FrameWithANewRaster(), surface);

        var grown = Allocated(backend) - before;
        const long held = (long)Frames * Side * Side * 4;
        grown.Should().BeLessThan(held / 4,
            $"{Frames} rasters of {Side}x{Side} are {held / (1024 * 1024)} MB if the device keeps them all, " +
            "and the renderer keeps only the last few");
    }

    /// <summary>
    /// The path a WINDOW takes: each frame committed and not waited for, so a raster is released
    /// while frames that bound it may still be on their way to the GPU. Here nothing holds the CPU
    /// back (a window has its drawable pool for that), so the frames in flight are many more than
    /// the thirty a raster is kept for, and what keeps each texture alive until its frame is drawn
    /// is the command buffer that bound it. A release that took a texture from under one would end
    /// this process rather than fail an assertion.
    /// </summary>
    [SkippableFact]
    public void ThePathAWindowTakes_ReleasesWhileFramesAreStillInFlight()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        using var backend = new MetalBackend();
        using var surface = backend.CreateSurface(64, 64);
        var target = ((MetalRenderTarget)((RhiSurface)surface).Target).Texture;

        for (var frame = 0; frame < 40; frame++) Unwaited(backend, target);
        backend.Render(FrameWithANewRaster(), surface);   // waits, and the queue is in order: all of the above is drawn
        var before = Allocated(backend);

        for (var frame = 0; frame < Frames; frame++) Unwaited(backend, target);
        backend.Render(FrameWithANewRaster(), surface);

        var grown = Allocated(backend) - before;
        const long held = (long)Frames * Side * Side * 4;
        grown.Should().BeLessThan(held / 4, "the frames were drawn and their rasters went with them");
    }

    private static void Unwaited(MetalBackend backend, IntPtr target) =>
        backend.RenderToDrawable(FrameWithANewRaster(), target, MetalDevice.PixelFormatRgba8UnormSrgb, drawable: IntPtr.Zero);

    private static DisplayList FrameWithANewRaster()
    {
        var builder = new DisplayListBuilder();
        builder.Texture(new Rect(0, 0, 64, 64), Color.White, TextureData.Rgba(Side, Side, new byte[Side * Side * 4]));
        return builder.Build();
    }

    /// <summary><c>MTLDevice.currentAllocatedSize</c>: the bytes this process's Metal resources hold.</summary>
    private static long Allocated(MetalBackend backend) =>
        (long)SendULong(backend.DeviceHandle, Sel("currentAllocatedSize"));
}
