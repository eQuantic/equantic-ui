using System.Runtime.CompilerServices;
using System.Text;

namespace eQuantic.UI.Native.Engine.Tests.Golden;

/// <summary>
/// The golden-image harness (plan D8) — the conformance culture applied to pixels. A case renders a
/// surface, reads back sRGB RGBA, and compares against <c>Goldens/&lt;name&gt;.png</c> in the SOURCE
/// tree (resolved via CallerFilePath so updates land in the repo, not bin/).
///
/// Comparison: per-channel absolute difference with a small tolerance (default 2/255) absorbing
/// float/FMA drift across architectures — any pixel exceeding it fails the case. On failure the
/// actual and an amplified diff image are written next to the test output with their paths in the
/// message. Set <c>EQ_UPDATE_GOLDENS=1</c> to (re)generate goldens instead of comparing.
/// </summary>
public static class GoldenImage
{
    private const int Tolerance = 2;

    public static void Match(IRenderSurface surface, string name)
    {
        var actual = new byte[surface.Width * surface.Height * 4];
        surface.ReadPixelsSrgb(actual);
        Match(surface.Width, surface.Height, actual, name);
    }

    public static void Match(int width, int height, byte[] actualRgba, string name)
    {
        var goldenPath = Path.Combine(GoldensDirectory(), name + ".png");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_GOLDENS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllBytes(goldenPath, PngCodec.Encode(width, height, actualRgba));
            return;
        }

        if (!File.Exists(goldenPath))
            throw new Xunit.Sdk.XunitException(
                $"Golden '{name}' does not exist ({goldenPath}). Run once with EQ_UPDATE_GOLDENS=1 to create it, then review the image.");

        var (goldenWidth, goldenHeight, golden) = PngCodec.Decode(File.ReadAllBytes(goldenPath));
        if (goldenWidth != width || goldenHeight != height)
            throw new Xunit.Sdk.XunitException(
                $"Golden '{name}' is {goldenWidth}x{goldenHeight} but the render is {width}x{height}.");

        var failures = 0;
        var maxDiff = 0;
        var firstFailure = -1;
        for (var i = 0; i < actualRgba.Length; i++)
        {
            var diff = Math.Abs(actualRgba[i] - golden[i]);
            if (diff > maxDiff) maxDiff = diff;
            if (diff > Tolerance)
            {
                failures++;
                if (firstFailure < 0) firstFailure = i;
            }
        }
        if (failures == 0) return;

        var artifactDir = Path.Combine(Path.GetTempPath(), "photon-golden-failures");
        Directory.CreateDirectory(artifactDir);
        var actualPath = Path.Combine(artifactDir, name + ".actual.png");
        var diffPath = Path.Combine(artifactDir, name + ".diff.png");
        File.WriteAllBytes(actualPath, PngCodec.Encode(width, height, actualRgba));
        File.WriteAllBytes(diffPath, PngCodec.Encode(width, height, DiffImage(actualRgba, golden)));

        var pixel = firstFailure / 4;
        throw new Xunit.Sdk.XunitException(
            $"Golden '{name}' mismatch: {failures} byte(s) differ beyond ±{Tolerance} (max diff {maxDiff}); " +
            $"first at pixel ({pixel % width}, {pixel / width}).\n" +
            $"  actual: {actualPath}\n  diff (×8): {diffPath}\n" +
            "If the change is intentional, regenerate with EQ_UPDATE_GOLDENS=1 and review the new image.");
    }

    /// <summary>Absolute difference amplified ×8, alpha forced opaque — makes 1–2 value drifts visible.</summary>
    private static byte[] DiffImage(byte[] actual, byte[] golden)
    {
        var diff = new byte[actual.Length];
        for (var i = 0; i < actual.Length; i += 4)
        {
            diff[i + 0] = (byte)Math.Min(255, Math.Abs(actual[i + 0] - golden[i + 0]) * 8);
            diff[i + 1] = (byte)Math.Min(255, Math.Abs(actual[i + 1] - golden[i + 1]) * 8);
            diff[i + 2] = (byte)Math.Min(255, Math.Abs(actual[i + 2] - golden[i + 2]) * 8);
            diff[i + 3] = 255;
        }
        return diff;
    }

    private static string GoldensDirectory([CallerFilePath] string sourcePath = "")
        => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath))!, "Goldens");
}
