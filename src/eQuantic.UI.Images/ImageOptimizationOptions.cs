namespace eQuantic.UI.Images;

/// <summary>
/// Configuration options for server-side image optimization.
/// </summary>
public class ImageOptimizationOptions
{
    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/webp", "image/avif", "image/png", "image/jpeg"
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
    /// Preferred output formats, ordered by priority.
    /// The first format that matches the browser's Accept header wins.
    /// </summary>
    public string[] Formats { get; set; } = ["image/webp"];

    /// <summary>
    /// Default quality (1-100). Default: 75.
    /// </summary>
    public int DefaultQuality { get; set; } = 75;

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

    /// <summary>
    /// Validates all configuration values. Throws <see cref="ArgumentException"/> on invalid config.
    /// </summary>
    public void Validate()
    {
        if (DefaultQuality < 1 || DefaultQuality > 100)
            throw new ArgumentException($"DefaultQuality must be between 1 and 100, got {DefaultQuality}.");

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
