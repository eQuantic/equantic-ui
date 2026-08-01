using FluentAssertions;
using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The dual-Kawase pyramid, CPU vs GPU (D6b/W3): the SAME scene renders on the Reference and on
/// each GPU backend, both results run their blur — <see cref="Blur.Apply"/> on the CPU, the
/// <see cref="RhiRenderer.Blur"/> chain on the GPU — and the outputs must agree within the parity
/// gate. This is the first FILTERED path in the engine, so it is also the test that would catch a
/// sampler wired to the wrong filter, a missed sRGB decode-before-filter, or tap offsets that
/// drifted from the normative math.
/// </summary>
public class BlurParityTests
{
    private const int MaxChannelDiff = 4;
    private const double MaxLoosePixelFraction = 0.01;
    private const float Radius = 8f;

    [SkippableFact]
    public void Metal_BlurMatchesTheCpuPyramid()
    {
        Skip.IfNot(MetalBackend.IsSupported, "No Metal device on this host.");
        using var device = new MetalDevice();
        AssertBlurParity(device, "metal");
    }

    [SkippableFact]
    public void Vulkan_BlurMatchesTheCpuPyramid()
    {
        Skip.IfNot(VulkanBackend.IsSupported, "No Vulkan loader/device on this host.");
        using var device = new VulkanDevice();
        AssertBlurParity(device, "vulkan");
    }

    /// <summary>The normative pyramid depth mapping, pinned so a change is a decision.</summary>
    [Fact]
    public void Levels_MapRadiiToPyramidDepth()
    {
        Blur.Levels(2f).Should().Be(1);
        Blur.Levels(4f).Should().Be(1);
        Blur.Levels(8f).Should().Be(2);
        Blur.Levels(12f).Should().Be(3);
        Blur.Levels(64f).Should().Be(4, "depth clamps at 4 — beyond that a UI blur reads as fog");
    }

    private static void AssertBlurParity(IRhiDevice device, string label)
    {
        var displayList = GoldenScenes.Build("card-composition");

        // CPU: normative render → GPU-storage readback → normative pyramid → straight alpha.
        using var referenceBackend = new ReferenceBackend();
        var referenceSurface = (ReferenceSurface)referenceBackend.CreateSurface(GoldenScenes.Width, GoldenScenes.Height);
        referenceBackend.Render(displayList, referenceSurface);
        var premultiplied = new BlurImage(GoldenScenes.Width, GoldenScenes.Height);
        referenceSurface.ReadPixelsPremultipliedSrgb(premultiplied.PremultipliedSrgb);
        var cpuBlurred = Blur.Apply(premultiplied, Radius);
        var cpu = new byte[cpuBlurred.PremultipliedSrgb.Length];
        RhiReadback.UnpremultiplySrgb(cpuBlurred.PremultipliedSrgb, cpu);

        // GPU: same render, the RHI pyramid, the standard readback.
        var renderer = new RhiRenderer(device);
        var source = device.CreateRenderTarget(GoldenScenes.Width, GoldenScenes.Height);
        renderer.Render(displayList, source);
        var blurred = renderer.Blur(source, Radius);
        var gpu = new byte[GoldenScenes.Width * GoldenScenes.Height * 4];
        blurred.ReadPixelsSrgb(gpu);
        blurred.Dispose();
        source.Dispose();
        renderer.Dispose();

        AssertFuzzyEqual(label, cpu, gpu);
    }

    private static void AssertFuzzyEqual(string label, byte[] reference, byte[] actual)
    {
        var maxDiff = 0;
        var loosePixels = 0;
        for (var i = 0; i < reference.Length; i += 4)
        {
            var pixelMax = 0;
            for (var c = 0; c < 4; c++)
                pixelMax = Math.Max(pixelMax, Math.Abs(reference[i + c] - actual[i + c]));
            maxDiff = Math.Max(maxDiff, pixelMax);
            if (pixelMax > 2) loosePixels++;
        }

        var total = reference.Length / 4;
        var fraction = loosePixels / (double)total;
        if (maxDiff <= MaxChannelDiff && fraction <= MaxLoosePixelFraction) return;

        var dir = Path.Combine(Path.GetTempPath(), "photon-blur-parity-failures");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{label}.cpu.png"), PngCodec.Encode(GoldenScenes.Width, GoldenScenes.Height, reference));
        File.WriteAllBytes(Path.Combine(dir, $"{label}.gpu.png"), PngCodec.Encode(GoldenScenes.Width, GoldenScenes.Height, actual));
        throw new Xunit.Sdk.XunitException(
            $"GPU blur ({label}) diverges from the CPU pyramid: max channel diff {maxDiff} (cap {MaxChannelDiff}); " +
            $"{loosePixels}/{total} pixels ({fraction:P2}) beyond ±2 (cap {MaxLoosePixelFraction:P0}). Artifacts: {dir}");
    }
}
