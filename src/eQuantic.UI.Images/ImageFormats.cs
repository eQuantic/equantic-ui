using SkiaSharp;

namespace eQuantic.UI.Images;

/// <summary>
/// The formats the optimizer reads, and the content type each one is served as. A format missing
/// here is refused before it is decoded: the web's five are what a page shows, and every other
/// decoder is surface nobody asked for.
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

    /// <summary>The names of the formats a source may be in, for a refusal to list.</summary>
    internal const string Names = "JPEG, PNG, GIF, WebP or BMP";

    /// <summary>The content type a readable format is served as, or null for any other.</summary>
    internal static string? ContentTypeOf(SKEncodedImageFormat format) =>
        Readable.GetValueOrDefault(format);

    /// <summary>
    /// The content type of encoded bytes, read from the bytes themselves: what the optimizer hands
    /// back is the format it was asked for, or the source as it was when no encoder here can write
    /// it, and the bytes are the one thing that cannot disagree with themselves.
    /// </summary>
    internal static string? ContentTypeOf(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec is null ? null : ContentTypeOf(codec.EncodedFormat);
    }
}
