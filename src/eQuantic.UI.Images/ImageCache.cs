using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace eQuantic.UI.Images;

/// <summary>
/// Filesystem-based cache for optimized images with TTL support.
/// Thread-safe: uses per-key semaphores to prevent duplicate optimization.
/// </summary>
public class ImageCache
{
    private readonly ImageOptimizationOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public ImageCache(ImageOptimizationOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Tries to get a cached optimized image. Returns null if not found or expired.
    /// </summary>
    public async Task<byte[]?> GetAsync(string url, int width, int quality, string format)
    {
        var path = GetCachePath(url, width, quality, format);

        if (!File.Exists(path))
            return null;

        // Check TTL
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        if (age.TotalSeconds > _options.CacheTtlSeconds)
        {
            try { File.Delete(path); } catch { /* ignore cleanup errors */ }
            return null;
        }

        return await File.ReadAllBytesAsync(path);
    }

    /// <summary>
    /// Stores an optimized image in the cache.
    /// </summary>
    public async Task SetAsync(string url, int width, int quality, string format, byte[] data)
    {
        var path = GetCachePath(url, width, quality, format);
        var dir = Path.GetDirectoryName(path)!;

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(path, data);
    }

    /// <summary>
    /// Gets or creates an optimized image, preventing duplicate optimization for the same key.
    /// </summary>
    public async Task<byte[]> GetOrCreateAsync(
        string url, int width, int quality, string format,
        Func<Task<byte[]>> factory)
    {
        var cached = await GetAsync(url, width, quality, format);
        if (cached != null)
            return cached;

        var key = ComputeKey(url, width, quality, format);
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            cached = await GetAsync(url, width, quality, format);
            if (cached != null)
                return cached;

            var data = await factory();
            await SetAsync(url, width, quality, format, data);
            return data;
        }
        finally
        {
            semaphore.Release();

            // Clean up semaphore if no one else is waiting
            if (semaphore.CurrentCount == 1)
                _locks.TryRemove(key, out _);
        }
    }

    private string GetCachePath(string url, int width, int quality, string format)
    {
        var key = ComputeKey(url, width, quality, format);
        var ext = format switch
        {
            "image/webp" => "webp",
            "image/png" => "png",
            _ => "jpg"
        };

        return Path.Combine(_options.CacheDirectory, $"{key}.{ext}");
    }

    private static string ComputeKey(string url, int width, int quality, string format)
    {
        var input = $"{url}|{width}|{quality}|{format}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
