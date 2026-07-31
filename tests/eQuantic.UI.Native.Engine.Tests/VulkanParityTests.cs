using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Engine.Vulkan;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>One Vulkan device + pipelines shared across the parity cases (created lazily so unsupported hosts skip, not fail).</summary>
public sealed class VulkanFixture : IDisposable
{
    private readonly Lazy<VulkanBackend> _backend = new(() => new VulkanBackend());

    public bool IsSupported => VulkanBackend.IsSupported;
    public VulkanBackend Backend => _backend.Value;

    public void Dispose()
    {
        if (_backend.IsValueCreated) _backend.Value.Dispose();
    }
}

/// <summary>
/// The Vulkan arm of the M0 exit gate, mirroring <see cref="MetalParityTests"/> case-for-case:
/// every <see cref="GoldenScenes"/> scene rendered by the Vulkan backend must match the Reference
/// backend within the SAME fuzzy tolerance — the Reference stays the single normative pixel
/// source, and both GPU backends run the SAME <c>RhiRenderer</c> encode loop and the SAME Slang
/// source (as SPIR-V here, MSL on Metal), so a divergence is either a backend-plumbing bug or a
/// slangc cross-target inconsistency, never a semantics fork. Skips (never fails) on hosts
/// without a Vulkan loader/device — on macOS dev machines that means MoltenVK as a DEV ICD (plan
/// D1 untouched: the product ships native Vulkan on Android only).
/// </summary>
public class VulkanParityTests : IClassFixture<VulkanFixture>
{
    /// <summary>Same cap as Metal (observed there: max diff 1 on Apple Silicon) — headroom for
    /// fast-math <c>pow</c> variance across GPU families, never for geometry drift.</summary>
    private const int MaxChannelDiff = 4;

    /// <summary>Pixels allowed beyond the golden harness's ±2 (AA edges, gradient rounding), as a fraction.</summary>
    private const double MaxLoosePixelFraction = 0.01;

    private readonly VulkanFixture _vulkan;

    public VulkanParityTests(VulkanFixture vulkan) => _vulkan = vulkan;

    [SkippableTheory]
    [MemberData(nameof(Scenes))]
    public void MatchesReference(string name)
    {
        Skip.IfNot(_vulkan.IsSupported, "No Vulkan loader/device on this host — install vulkan-loader + molten-vk (dev ICD) on macOS.");

        var displayList = GoldenScenes.Build(name);
        using var referenceBackend = new ReferenceBackend();
        var referencePixels = Render(referenceBackend, displayList);
        var vulkanPixels = Render(_vulkan.Backend, displayList);

        AssertFuzzyEqual(name, referencePixels, vulkanPixels);
    }

    [SkippableFact]
    public void Backend_ReportsVulkanKind_AndSurfaceDimensions()
    {
        Skip.IfNot(_vulkan.IsSupported, "No Vulkan loader/device on this host.");

        Assert.Equal(RenderBackendKind.Vulkan, _vulkan.Backend.Kind);
        using var surface = _vulkan.Backend.CreateSurface(64, 32);
        Assert.Equal(64, surface.Width);
        Assert.Equal(32, surface.Height);
    }

    public static TheoryData<string> Scenes => GoldenScenes.Names;

    private static byte[] Render(IRenderBackend backend, DisplayList displayList)
    {
        using var surface = backend.CreateSurface(GoldenScenes.Width, GoldenScenes.Height);
        backend.Render(displayList, surface);
        var pixels = new byte[GoldenScenes.Width * GoldenScenes.Height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    private static void AssertFuzzyEqual(string name, byte[] reference, byte[] vulkan)
    {
        var maxDiff = 0;
        var loosePixels = 0;
        var worstPixel = -1;
        for (var i = 0; i < reference.Length; i += 4)
        {
            var pixelMax = 0;
            for (var c = 0; c < 4; c++)
            {
                var diff = Math.Abs(reference[i + c] - vulkan[i + c]);
                if (diff > pixelMax) pixelMax = diff;
            }
            if (pixelMax > maxDiff)
            {
                maxDiff = pixelMax;
                worstPixel = i / 4;
            }
            if (pixelMax > 2) loosePixels++;
        }

        var totalPixels = reference.Length / 4;
        var looseFraction = loosePixels / (double)totalPixels;
        if (maxDiff <= MaxChannelDiff && looseFraction <= MaxLoosePixelFraction) return;

        var artifactDir = Path.Combine(Path.GetTempPath(), "photon-vulkan-parity-failures");
        Directory.CreateDirectory(artifactDir);
        var referencePath = Path.Combine(artifactDir, name + ".reference.png");
        var vulkanPath = Path.Combine(artifactDir, name + ".vulkan.png");
        File.WriteAllBytes(referencePath, PngCodec.Encode(GoldenScenes.Width, GoldenScenes.Height, reference));
        File.WriteAllBytes(vulkanPath, PngCodec.Encode(GoldenScenes.Width, GoldenScenes.Height, vulkan));

        throw new Xunit.Sdk.XunitException(
            $"Vulkan render of '{name}' diverges from the Reference: max channel diff {maxDiff} " +
            $"(cap {MaxChannelDiff}) at pixel ({worstPixel % GoldenScenes.Width}, {worstPixel / GoldenScenes.Width}); " +
            $"{loosePixels}/{totalPixels} pixels ({looseFraction:P2}) beyond ±2 (cap {MaxLoosePixelFraction:P0}).\n" +
            $"  reference: {referencePath}\n  vulkan: {vulkanPath}");
    }
}
