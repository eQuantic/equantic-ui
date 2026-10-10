namespace eQuantic.UI.Images;

/// <summary>
/// Core image processing service, on SkiaSharp: resizes a source and encodes it in the format the
/// browser asked for.
/// </summary>
public class ImageOptimizer
{
    private readonly ImageOptimizationOptions _options;

    /// <summary>
    /// An optimizer that reads no source past <see cref="ImageOptimizationOptions.MaxSourceSize"/>.
    /// Resolved from the services <see cref="ImageExtensions.AddImageOptimization"/> registers, it
    /// reads the app's own options.
    /// </summary>
    public ImageOptimizer(ImageOptimizationOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Optimizes an image by resizing to the specified width and encoding in the target format.
    /// <para>
    /// An animated source is handed back as it is, as the Next.js optimizer hands one back: no
    /// encoder here writes frames, and the first frame alone would stop the animation. Read the
    /// format of the result from its bytes.
    /// </para>
    /// </summary>
    /// <param name="source">Source image stream: a JPEG, PNG, GIF, WebP or BMP.</param>
    /// <param name="width">Target width in pixels, as displayed. Height is auto-calculated to maintain aspect ratio.</param>
    /// <param name="quality">Output quality (1-100).</param>
    /// <param name="outputFormat">Target MIME type ("image/webp", "image/png"); any other is JPEG.</param>
    /// <returns>Optimized image bytes.</returns>
    /// <exception cref="InvalidDataException">The source runs past <see cref="ImageOptimizationOptions.MaxSourceSize"/>, is not one of the formats read, or holds too many pixels to decode.</exception>
    public async Task<byte[]> OptimizeAsync(Stream source, int width, int quality, string outputFormat)
    {
        using var image = await SourceImage.ReadAsync(source, _options.MaxSourceSize);
        if (image.IsAnimated)
            return image.Bytes;

        using var decoded = image.Decode();

        // Only resize if the target width is smaller than the source
        if (decoded.Width > width)
        {
            var height = Math.Max(1, (int)Math.Round((double)decoded.Height * width / decoded.Width));
            using var resized = SourceImage.Scaled(decoded, width, height);
            return ImageEncoder.Encode(resized, outputFormat, quality);
        }

        return ImageEncoder.Encode(decoded, outputFormat, quality);
    }

    /// <summary>
    /// Gets the dimensions of a source image as displayed, its orientation applied, from its header,
    /// read asynchronously: no pixel is decoded, and the stream is left open.
    /// </summary>
    /// <exception cref="InvalidDataException">The source is not one of the formats read.</exception>
    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream source)
    {
        var size = await SourceImage.MeasureAsync(source);
        return (size.Width, size.Height);
    }
}
