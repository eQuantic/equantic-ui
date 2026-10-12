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
