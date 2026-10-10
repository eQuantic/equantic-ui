using SkiaSharp;

namespace eQuantic.UI.Images;

/// <summary>
/// Writes pixels as WebP, PNG or JPEG. The pixels are sRGB (<see cref="SourceImage"/> converts
/// them) and are written untagged, which a browser reads as sRGB: tagged, Skia embeds a 472-byte
/// ICC profile in every JPEG, more than an 8-pixel placeholder's whole picture (288 bytes).
/// </summary>
internal static class ImageEncoder
{
    /// <summary>
    /// The image in the given content type: "image/webp", "image/png", and JPEG for any other. The
    /// type is read without regard to case, as <see cref="ImageOptimizationOptions.Validate"/> accepts
    /// it: a media type is case-insensitive, and "IMAGE/WEBP" was a valid setting answered with JPEG.
    /// </summary>
    public static byte[] Encode(SKImage image, string contentType, int quality)
    {
        using var tagged = image.PeekPixels();
        using var pixels = new SKPixmap(tagged.Info.WithColorSpace(null), tagged.GetPixels(), tagged.RowBytes);
        using var data = contentType.ToLowerInvariant() switch
        {
            "image/webp" => pixels.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality)),
            "image/png" => pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 9)),
            _ => pixels.Encode(new SKJpegEncoderOptions(quality, SKJpegEncoderDownsample.Downsample420,
                SKJpegEncoderAlphaOption.Ignore)),
        } ?? throw new InvalidOperationException($"SkiaSharp could not encode the image as {contentType}.");
        return data.ToArray();
    }
}
