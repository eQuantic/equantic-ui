using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Images;

/// <summary>
/// Filesystem-based cache for optimized images with TTL support.
/// Thread-safe: uses per-key semaphores to prevent duplicate optimization.
/// Limits concurrent optimizations to prevent resource exhaustion.
/// Periodically evicts expired entries, and keeps the copies on disk under
/// <see cref="ImageOptimizationOptions.MaxCacheSize"/>, the oldest removed first (#1289).
/// </summary>
public class ImageCache : IDisposable
{
    private readonly ImageOptimizationOptions _options;
    private readonly ILogger<ImageCache>? _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly SemaphoreSlim _concurrencyLimiter = new(10, 10);
    private readonly Timer _evictionTimer;
    private readonly object _trimGate = new();
    /// <summary>The bytes the copies take, measured at start and after each trim, and added to by each copy.</summary>
    private long _size;
    private bool _disposed;

    public ImageCache(ImageOptimizationOptions options, ILogger<ImageCache>? logger = null)
    {
        _options = options;
        _logger = logger;

        // Run eviction every CacheTtlSeconds (minimum 60s to avoid excessive I/O)
        var evictionInterval = TimeSpan.FromSeconds(Math.Max(options.CacheTtlSeconds, 60));
        _evictionTimer = new Timer(_ => EvictExpiredEntries(), null, evictionInterval, evictionInterval);
        _size = Measure();
    }

    /// <summary>
    /// Tries to get a cached optimized image. Returns null if not found or expired.
    /// </summary>
    public async Task<byte[]?> GetAsync(string source, int width, int quality, string format)
    {
        var path = GetCachePath(source, width, quality, format);

        if (!File.Exists(path))
            return null;

        // Check TTL
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        if (age.TotalSeconds > _options.CacheTtlSeconds)
        {
            try
            {
                var length = new FileInfo(path).Length;
                File.Delete(path);
                Interlocked.Add(ref _size, -length);
            }
            catch { /* ignore cleanup errors */ }
            return null;
        }

        return await File.ReadAllBytesAsync(path);
    }

    /// <summary>
    /// Stores an optimized image in the cache.
    /// </summary>
    public async Task SetAsync(string source, int width, int quality, string format, byte[] data)
    {
        var path = GetCachePath(source, width, quality, format);
        var dir = Path.GetDirectoryName(path)!;

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(path, data);
        if (Interlocked.Add(ref _size, data.Length) > _options.MaxCacheSize) Trim();
    }

    /// <summary>The bytes the copies on disk take.</summary>
    private long Measure()
    {
        try
        {
            return Directory.Exists(_options.CacheDirectory)
                ? new DirectoryInfo(_options.CacheDirectory).EnumerateFiles().Sum(file => file.Length)
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Removes the oldest copies until the cache takes nine tenths of its cap, so a run of new copies does
    /// not trim on every one.
    /// </summary>
    private void Trim()
    {
        lock (_trimGate)
        {
            // Each copy read on its own: one removed while the folder is walked (expired by a read, evicted by
            // the timer) is skipped, never a failure of the request that wrote the last copy.
            var files = new List<(FileInfo File, long Length, DateTime Written)>();
            try
            {
                if (!Directory.Exists(_options.CacheDirectory)) return;
                foreach (var file in new DirectoryInfo(_options.CacheDirectory).EnumerateFiles())
                {
                    try
                    {
                        files.Add((file, file.Length, file.LastWriteTimeUtc));
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch (IOException)
            {
                return;
            }

            var total = files.Sum(file => file.Length);
            // The tally counts an overwritten copy twice: under the cap, it is only corrected.
            if (total <= _options.MaxCacheSize)
            {
                Interlocked.Exchange(ref _size, total);
                return;
            }

            var target = _options.MaxCacheSize / 10 * 9;
            var removed = 0;
            foreach (var (file, length, _) in files.OrderBy(file => file.Written))
            {
                if (total <= target) break;
                try
                {
                    file.Delete();
                    total -= length;
                    removed++;
                }
                catch (IOException)
                {
                    // A copy being read is left for the next trim.
                }
            }
            Interlocked.Exchange(ref _size, total);
            if (removed > 0) _logger?.LogInformation("Image cache trim: removed {Count} entries to stay under {Max} bytes", removed, _options.MaxCacheSize);
        }
    }

    /// <summary>
    /// Gets or creates an optimized image, preventing duplicate optimization for the same key.
    /// Limits total concurrent optimizations to prevent resource exhaustion.
    /// </summary>
    public async Task<byte[]> GetOrCreateAsync(
        string source, int width, int quality, string format,
        Func<Task<byte[]>> factory)
    {
        var cached = await GetAsync(source, width, quality, format);
        if (cached != null)
            return cached;

        var key = ComputeKey(source, width, quality, format);
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        // Limit total concurrent optimizations
        await _concurrencyLimiter.WaitAsync();
        try
        {
            await semaphore.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                cached = await GetAsync(source, width, quality, format);
                if (cached != null)
                    return cached;

                var data = await factory();
                await SetAsync(source, width, quality, format, data);
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
                Interlocked.Exchange(ref _size, Measure());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Image cache eviction failed");
        }
    }

    private string GetCachePath(string source, int width, int quality, string format)
    {
        var key = ComputeKey(source, width, quality, format);
        var ext = format switch
        {
            "image/webp" => "webp",
            "image/png" => "png",
            _ => "jpg"
        };

        return Path.Combine(_options.CacheDirectory, $"{key}.{ext}");
    }

    private static string ComputeKey(string source, int width, int quality, string format)
    {
        var input = $"{source}|{width}|{quality}|{format}";
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
