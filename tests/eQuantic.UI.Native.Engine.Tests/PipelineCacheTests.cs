using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Engine.Vulkan;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The D3 tail closed: pipeline caching ACROSS LAUNCHES. Each GPU backend's first device in a
/// fresh cache directory persists its cache artifact (Metal: a serialized
/// <c>MTLBinaryArchive</c>; Vulkan: the <c>VkPipelineCache</c> blob); a second device — the
/// simulated next launch — consumes it and still renders pixel-correct against the Reference.
/// The cache is BEST-EFFORT by design, but on the supported dev/CI hosts the artifact is
/// REQUIRED to appear: silently degrading to per-launch compilation everywhere would let the D3
/// promise rot unnoticed.
/// </summary>
public class PipelineCacheTests
{
    [SkippableFact]
    public void Metal_PersistsArchive_AndSecondLaunchRendersFromIt()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        AssertCacheRoundTrip(".metalarchive", () => new MetalBackend());
    }

    [SkippableFact]
    public void Vulkan_PersistsCache_AndSecondLaunchRendersFromIt()
    {
        Skip.IfNot(VulkanBackend.IsSupported, "No Vulkan loader/device on this host.");
        AssertCacheRoundTrip(".vkpipelinecache", () => new VulkanBackend());
    }

    private static void AssertCacheRoundTrip(string extension, Func<IRenderBackend> createBackend)
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "photon-pipeline-cache-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("EQ_PHOTON_CACHE_DIR", cacheDirectory);
        try
        {
            using (var firstLaunch = createBackend())
                AssertRendersLikeReference(firstLaunch);

            var files = Directory.GetFiles(cacheDirectory, "Sdf-*" + extension);
            Assert.Single(files);
            Assert.True(new FileInfo(files[0]).Length > 0, $"{Path.GetFileName(files[0])} must not be empty.");

            using (var secondLaunch = createBackend())
                AssertRendersLikeReference(secondLaunch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EQ_PHOTON_CACHE_DIR", null);
            try { Directory.Delete(cacheDirectory, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>A cached pipeline that renders wrong is worse than no cache — every launch is
    /// judged against the Reference on the busiest scene (all three pipelines exercised).</summary>
    private static void AssertRendersLikeReference(IRenderBackend backend)
    {
        var displayList = GoldenScenes.Build("card-composition");
        using var referenceBackend = new ReferenceBackend();
        var reference = Render(referenceBackend, displayList);
        var actual = Render(backend, displayList);

        var maxDiff = 0;
        for (var i = 0; i < reference.Length; i++)
            maxDiff = Math.Max(maxDiff, Math.Abs(reference[i] - actual[i]));
        Assert.True(maxDiff <= 4, $"{backend.Kind} render through the pipeline cache diverged: max channel diff {maxDiff}.");
    }

    private static byte[] Render(IRenderBackend backend, DisplayList displayList)
    {
        using var surface = backend.CreateSurface(GoldenScenes.Width, GoldenScenes.Height);
        backend.Render(displayList, surface);
        var pixels = new byte[GoldenScenes.Width * GoldenScenes.Height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }
}
