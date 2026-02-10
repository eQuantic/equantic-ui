using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Images;

/// <summary>
/// Filesystem-based cache for optimized images with TTL support.
/// Thread-safe: uses per-key semaphores to prevent duplicate optimization.
/// Limits concurrent optimizations to prevent resource exhaustion.
/// Periodically evicts expired entries to prevent unbounded disk growth.
/// </summary>
public class ImageCache : IDisposable
{
    private readonly ImageOptimizationOptions _options;
    private readonly ILogger<ImageCache>? _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly SemaphoreSlim _concurrencyLimiter = new(10, 10);
    private readonly Timer _evictionTimer;
    private bool _disposed;

    public ImageCache(ImageOptimizationOptions options, ILogger<ImageCache>? logger = null)
    {
        _options = options;
        _logger = logger;

        // Run eviction every CacheTtlSeconds (minimum 60s to avoid excessive I/O)
        var evictionInterval = TimeSpan.FromSeconds(Math.Max(options.CacheTtlSeconds, 60));
        _evictionTimer = new Timer(_ => EvictExpiredEntries(), null, evictionInterval, evictionInterval);
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
    /// Limits total concurrent optimizations to prevent resource exhaustion.
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

        // Limit total concurrent optimizations
        await _concurrencyLimiter.WaitAsync();
        try
        {
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

                // Clean up semaphore if no one else is waiting, and dispose it
                if (_locks.TryRemove(key, out var removed) && removed.CurrentCount == 1)
                {
                    removed.Dispose();
                }
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private void EvictExpiredEntries()
    {
        try
        {
            if (!Directory.Exists(_options.CacheDirectory))
                return;

            var now = DateTime.UtcNow;
            var evicted = 0;

            foreach (var file in Directory.EnumerateFiles(_options.CacheDirectory))
            {
                try
                {
                    var age = now - File.GetLastWriteTimeUtc(file);
                    if (age.TotalSeconds > _options.CacheTtlSeconds)
                    {
                        File.Delete(file);
                        evicted++;
                    }
                }
                catch
                {
                    // Ignore per-file errors (locked, permissions, etc.)
                }
            }

            if (evicted > 0)
            {
                _logger?.LogInformation("Image cache eviction: removed {Count} expired entries", evicted);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Image cache eviction failed");
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _evictionTimer.Dispose();
        _concurrencyLimiter.Dispose();
        foreach (var kvp in _locks)
        {
            kvp.Value.Dispose();
        }
        _locks.Clear();
    }
}
