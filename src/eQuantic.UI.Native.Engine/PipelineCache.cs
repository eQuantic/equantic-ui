using System.Security.Cryptography;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// Shared on-disk pipeline-cache pathing for the GPU backends (the D3 tail: pipeline caching
/// ACROSS LAUNCHES — Metal serializes an <c>MTLBinaryArchive</c>, Vulkan a <c>VkPipelineCache</c>
/// blob). The file name carries a SHA-256 fingerprint of the shader bytes it was built from, so a
/// framework upgrade (new metallib/SPIR-V) naturally targets a fresh file and stale siblings of
/// the same prefix are swept. Everything here is BEST-EFFORT: a read-only disk, a missing cache
/// directory or a corrupt file must degrade to "compile normally", never to a failed device.
/// </summary>
public static class PipelineCache
{
    /// <summary>
    /// Resolves the cache file for <paramref name="prefix"/> + <paramref name="shaderBytes"/>'s
    /// fingerprint (creating the cache directory, sweeping stale fingerprints of the same prefix).
    /// False when the directory cannot exist — callers skip caching entirely.
    /// </summary>
    public static bool TryGetFile(string prefix, ReadOnlySpan<byte> shaderBytes, string extension, out string path)
    {
        path = string.Empty;
        try
        {
            var directory = CacheDirectory();
            Directory.CreateDirectory(directory);

            var fingerprint = Convert.ToHexString(SHA256.HashData(shaderBytes))[..16].ToLowerInvariant();
            var fileName = $"{prefix}-{fingerprint}{extension}";
            path = Path.Combine(directory, fileName);

            foreach (var stale in Directory.EnumerateFiles(directory, $"{prefix}-*{extension}"))
            {
                if (Path.GetFileName(stale) == fileName) continue;
                try { File.Delete(stale); }
                catch (IOException) { /* another process may hold it — next launch sweeps */ }
                catch (UnauthorizedAccessException) { }
            }
            return true;
        }
        catch (Exception)
        {
            // Unwritable/undeterminable cache location — run without cross-launch caching.
            path = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// <c>EQ_PHOTON_CACHE_DIR</c> override first (tests, sandboxed hosts, the future Android shell
    /// passing the app's cacheDir), then the platform's user cache location.
    /// </summary>
    private static string CacheDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("EQ_PHOTON_CACHE_DIR");
        if (!string.IsNullOrEmpty(overridden)) return overridden;
        // Every Apple platform keeps regenerable caches in Library/Caches — on iOS that is the app's
        // own sandbox, which the system may purge, which is exactly what a rebuildable cache is for.
        var apple = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
            || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsTvOS();
        return apple
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches", "eQuantic.UI.Photon")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eQuantic.UI.Photon");
    }
}
