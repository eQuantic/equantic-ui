using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Build;

/// <summary>
/// The same icon, for a launcher. Android asks for one bitmap per density bucket rather than one
/// big square, so that is what gets written — from the very same source, so an app states its icon
/// ONCE and it appears everywhere it belongs.
/// </summary>
public static class AndroidIcons
{
    /// <summary>
    /// The buckets a launcher actually reads, at 48dp: mdpi is 1×, and each step up is the density
    /// it is named for. Nothing below mdpi — no device that ships today is under it.
    /// </summary>
    private static readonly (string Bucket, int Size)[] Densities =
    [
        ("mipmap-mdpi", 48),
        ("mipmap-hdpi", 72),
        ("mipmap-xhdpi", 96),
        ("mipmap-xxhdpi", 144),
        ("mipmap-xxxhdpi", 192),
    ];

    public static void Write(string resDir, int sourceSize, byte[] sourceRgba)
    {
        foreach (var (bucket, size) in Densities)
        {
            var dir = Path.Combine(resDir, bucket);
            Directory.CreateDirectory(dir);
            var pixels = size == sourceSize ? sourceRgba : Downscale.Box(sourceRgba, sourceSize, size);
            File.WriteAllBytes(Path.Combine(dir, "ic_launcher.png"), PngCodec.Encode(size, size, pixels));
        }
    }
}
