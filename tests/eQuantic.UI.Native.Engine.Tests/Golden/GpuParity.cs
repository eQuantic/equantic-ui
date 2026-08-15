namespace eQuantic.UI.Native.Engine.Tests.Golden;

/// <summary>
/// One tolerance policy for every GPU-versus-Reference comparison.
/// <para>
/// The Reference backend is the normative pixel source; a GPU backend is allowed to differ only by
/// representation noise — the 8-bit sRGB intermediate quantization on blended overlaps, fast-math
/// <c>pow</c> in the shader's sRGB decode, the hardware's sRGB encode-on-store. Never geometry: a
/// transliteration bug (wrong corner select, wrong stroke band, wrong blend factors) moves whole edge
/// runs by dozens of values and fails both gates at once.
/// </para>
/// <para>
/// It lives here because it was written three times otherwise — Metal, Vulkan, and the tree-level
/// gate — and three copies of a tolerance is three tolerances the day someone widens one.
/// </para>
/// </summary>
public static class GpuParity
{
    /// <summary>
    /// Hard cap on any single channel difference. Observed on Apple Silicon (2026-07): max diff 1
    /// across every scene, zero pixels beyond ±2 — the cap carries 4× headroom for other GPU families
    /// (fast-math <c>pow</c> variance), not for geometry drift.
    /// </summary>
    public const int MaxChannelDiff = 4;

    /// <summary>Pixels allowed beyond the golden harness's ±2 (AA edges, gradient rounding).</summary>
    public const double MaxLoosePixelFraction = 0.01;

    /// <summary>
    /// Compares a GPU render against the Reference one, and on failure writes BOTH as PNGs — a
    /// parity failure is a picture, and a message with two paths in it is worth more than any number
    /// of channel statistics.
    /// </summary>
    public static void AssertFuzzyEqual(
        string backend, string name, int width, int height, byte[] reference, byte[] gpu)
    {
        var maxDiff = 0;
        var loosePixels = 0;
        var worstPixel = -1;

        for (var i = 0; i < reference.Length; i += 4)
        {
            var pixelMax = 0;
            for (var c = 0; c < 4; c++)
            {
                var diff = Math.Abs(reference[i + c] - gpu[i + c]);
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

        var artifactDir = Path.Combine(Path.GetTempPath(), $"photon-{backend.ToLowerInvariant()}-parity-failures");
        Directory.CreateDirectory(artifactDir);
        var referencePath = Path.Combine(artifactDir, name + ".reference.png");
        var gpuPath = Path.Combine(artifactDir, $"{name}.{backend.ToLowerInvariant()}.png");
        File.WriteAllBytes(referencePath, PngCodec.Encode(width, height, reference));
        File.WriteAllBytes(gpuPath, PngCodec.Encode(width, height, gpu));

        throw new Xunit.Sdk.XunitException(
            $"{backend} render of '{name}' diverges from the Reference: max channel diff {maxDiff} "
            + $"(cap {MaxChannelDiff}) at pixel ({worstPixel % width}, {worstPixel / width}); "
            + $"{loosePixels}/{totalPixels} pixels ({looseFraction:P2}) beyond ±2 (cap {MaxLoosePixelFraction:P0}).\n"
            + $"  reference: {referencePath}\n  {backend.ToLowerInvariant()}: {gpuPath}");
    }
}
