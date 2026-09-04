using System.Linq;

namespace eQuantic.UI.Images;

/// <summary>
/// Static state bridge for image optimization.
/// Set by eQuantic.UI.Images package during UseImageOptimization(),
/// read by Image component during Build().
/// </summary>
public static class ImageOptimizationState
{
    /// <summary>
    /// Whether image optimization is globally enabled.
    /// </summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Default quality (1-100). Default: 75.
    /// </summary>
    public static int DefaultQuality { get; set; } = 75;

    /// <summary>
    /// Allowed output widths for full-width responsive images.
    /// </summary>
    public static int[] DeviceSizes { get; set; } = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];

    /// <summary>
    /// Allowed output widths for smaller-than-viewport images.
    /// </summary>
    public static int[] ImageSizes { get; set; } = [32, 48, 64, 96, 128, 256, 384];

    /// <summary>
    /// All allowed widths (DeviceSizes + ImageSizes), sorted ascending.
    /// </summary>
    public static int[] AllowedWidths =>
        ImageSizes.Concat(DeviceSizes).Distinct().OrderBy(x => x).ToArray();

    /// <summary>
    /// Snaps a desired width to the nearest allowed width (>= desired).
    /// </summary>
    public static int SnapToAllowed(int width)
    {
        var allowed = AllowedWidths;
        foreach (var w in allowed)
        {
            if (w >= width) return w;
        }
        return allowed.Length > 0 ? allowed[allowed.Length - 1] : width;
    }
}
