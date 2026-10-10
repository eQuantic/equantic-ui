using SkiaSharp;

namespace eQuantic.UI.Images;

/// <summary>
/// The formats the optimizer reads, the extensions they are named by, and the content type each is
/// served as. A format missing here is refused before it is decoded: the web's five are what a page
/// shows, and every other decoder is surface nobody asked for.
/// </summary>
internal static class ImageFormats
{
    private static readonly Dictionary<SKEncodedImageFormat, string> Readable = new()
    {
        [SKEncodedImageFormat.Jpeg] = "image/jpeg",
        [SKEncodedImageFormat.Png] = "image/png",
        [SKEncodedImageFormat.Gif] = "image/gif",
        [SKEncodedImageFormat.Webp] = "image/webp",
        [SKEncodedImageFormat.Bmp] = "image/bmp",
    };

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
    };

    /// <summary>The names of the formats a source may be in, for a refusal to list.</summary>
    internal const string Names = "JPEG, PNG, GIF, WebP or BMP";

    /// <summary>Whether a file of this extension is one of the formats read, so the endpoint can refuse
    /// another before it opens the file. Bytes that disagree with their name are refused by the codec.</summary>
    internal static bool IsReadableExtension(string extension) => Extensions.Contains(extension);

    /// <summary>The content type a readable format is served as, or null for any other.</summary>
    internal static string? ContentTypeOf(SKEncodedImageFormat format) =>
        Readable.GetValueOrDefault(format);

    /// <summary>
    /// The content type of bytes, from their signature: a response the optimizer handed back (the
    /// format it was asked for, or an animated source as it was), or the start of a source being
    /// measured. Twelve bytes answer it, so a cached response is labelled without being copied or
    /// parsed.
    /// </summary>
    internal static string? ContentTypeOf(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => "image/gif",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        [(byte)'B', (byte)'M', ..] => "image/bmp",
        _ => null,
    };
}
