namespace eQuantic.UI.Images;

/// <summary>
/// Configuration options for server-side image optimization.
/// </summary>
public class ImageOptimizationOptions
{
    // The formats the optimizer has an encoder for. AVIF is not one: listed first, it was negotiated
    // with every browser that takes it and answered with JPEG, past the WebP listed after it.
    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/webp", "image/png", "image/jpeg"
    };

    /// <summary>
    /// Allowed output widths for full-width responsive images (matches Next.js defaults).
    /// </summary>
    public int[] DeviceSizes { get; set; } = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];

    /// <summary>
    /// Allowed output widths for smaller-than-viewport images.
    /// </summary>
    public int[] ImageSizes { get; set; } = [32, 48, 64, 96, 128, 256, 384];

    /// <summary>
    /// Preferred output formats, ordered by priority: "image/webp", "image/png" or "image/jpeg".
    /// The first format that matches the browser's Accept header wins.
    /// </summary>
    public string[] Formats { get; set; } = ["image/webp"];

    /// <summary>
    /// Default quality (1-100), one of <see cref="Qualities"/>. Default: 75.
    /// </summary>
    public int DefaultQuality { get; set; } = 75;

    /// <summary>
    /// The qualities the optimizer encodes at (1-100); a request's <c>q</c> is rounded to the nearest one.
    /// Any <c>q</c> from 1 to 100 was a decode, an encode and a cached copy of its own (#1289). Default:
    /// 50, 75, 90 and 100.
    /// </summary>
    public int[] Qualities { get; set; } = [50, 75, 90, 100];

    /// <summary>
    /// The most bytes the cache of optimized images keeps on disk; past it, the oldest copies are removed.
    /// It was bounded by age alone (#1289). Default: 512 MiB.
    /// </summary>
    public long MaxCacheSize { get; set; } = 512L * 1024 * 1024;

    /// <summary>
    /// Cache TTL in seconds. Default: 14400 (4 hours).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 14400;

    /// <summary>
    /// Cache directory for optimized images. Default: obj/eQuantic/image-cache.
    /// </summary>
    public string CacheDirectory { get; set; } = "obj/eQuantic/image-cache";

    /// <summary>
    /// Maximum allowed source image size in bytes. Default: 10MB.
    /// </summary>
    public long MaxSourceSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// All allowed widths (DeviceSizes + ImageSizes), sorted ascending.
    /// </summary>
    public int[] AllowedWidths =>
        ImageSizes.Concat(DeviceSizes).Distinct().OrderBy(x => x).ToArray();

    /// <summary>The quality of <see cref="Qualities"/> nearest <paramref name="requested"/>, the higher of two
    /// as near.</summary>
    public int QualityFor(int requested) =>
        Qualities.OrderBy(q => Math.Abs(q - requested)).ThenByDescending(q => q).First();

    /// <summary>
    /// Validates all configuration values. Throws <see cref="ArgumentException"/> on invalid config.
    /// </summary>
    public void Validate()
    {
        if (DefaultQuality < 1 || DefaultQuality > 100)
            throw new ArgumentException($"DefaultQuality must be between 1 and 100, got {DefaultQuality}.");

        if (Qualities == null || Qualities.Length == 0)
            throw new ArgumentException("Qualities must contain at least one quality.");

        foreach (var q in Qualities)
        {
            if (q < 1 || q > 100)
                throw new ArgumentException($"Every quality must be between 1 and 100, got {q}.");
        }

        if (!Qualities.Contains(DefaultQuality))
            throw new ArgumentException(
                $"DefaultQuality {DefaultQuality} must be one of Qualities ({string.Join(", ", Qualities)}).");

        if (MaxCacheSize <= 0)
            throw new ArgumentException($"MaxCacheSize must be positive, got {MaxCacheSize}.");

        if (CacheTtlSeconds < 0)
            throw new ArgumentException($"CacheTtlSeconds must be non-negative, got {CacheTtlSeconds}.");

        if (MaxSourceSize <= 0)
            throw new ArgumentException($"MaxSourceSize must be positive, got {MaxSourceSize}.");

        if (string.IsNullOrWhiteSpace(CacheDirectory))
            throw new ArgumentException("CacheDirectory must not be empty.");

        if (Formats == null || Formats.Length == 0)
            throw new ArgumentException("Formats must contain at least one format.");

        foreach (var format in Formats)
        {
            if (!ValidFormats.Contains(format))
                throw new ArgumentException(
                    $"Invalid format '{format}'. Supported: {string.Join(", ", ValidFormats)}.");
        }

        if (DeviceSizes == null || DeviceSizes.Length == 0)
            throw new ArgumentException("DeviceSizes must contain at least one width.");

        if (ImageSizes == null || ImageSizes.Length == 0)
            throw new ArgumentException("ImageSizes must contain at least one width.");

        foreach (var w in DeviceSizes.Concat(ImageSizes))
        {
            if (w <= 0)
                throw new ArgumentException($"All widths must be positive, got {w}.");
        }
    }
}
