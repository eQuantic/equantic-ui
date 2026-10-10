namespace eQuantic.UI.Images;

/// <summary>
/// Generates tiny blur placeholder images as base64 data URLs.
/// Following Next.js pattern: 8px max dimension, quality 70.
/// </summary>
public class BlurPlaceholderGenerator
{
    private const int MaxDimension = 8;
    private const int BlurQuality = 70;

    private readonly ImageOptimizationOptions _options;

    /// <summary>
    /// A generator that reads no source past <see cref="ImageOptimizationOptions.MaxSourceSize"/>.
    /// Resolved from the services <see cref="ImageExtensions.AddImageOptimization"/> registers, it
    /// reads the app's own options.
    /// </summary>
    public BlurPlaceholderGenerator(ImageOptimizationOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Generates a tiny blur placeholder as a base64 data URL.
    /// The output is an 8px-wide JPEG suitable for CSS background-image, from the source's first
    /// frame as displayed.
    /// </summary>
    /// <param name="source">Source image stream: a JPEG, PNG, GIF, WebP or BMP.</param>
    /// <returns>A data URL like "data:image/jpeg;base64,..."</returns>
    /// <exception cref="InvalidDataException">The source runs past <see cref="ImageOptimizationOptions.MaxSourceSize"/>, is not one of the formats read, or holds too many pixels to decode.</exception>
    public async Task<string> GenerateAsync(Stream source)
    {
        using var image = await SourceImage.ReadAsync(source, _options.MaxSourceSize);
        using var decoded = image.Decode();

        var ratio = (double)decoded.Height / decoded.Width;
        int blurWidth, blurHeight;

        if (decoded.Width >= decoded.Height)
        {
            blurWidth = MaxDimension;
            blurHeight = Math.Max(1, (int)(MaxDimension * ratio));
        }
        else
        {
            blurHeight = MaxDimension;
            blurWidth = Math.Max(1, (int)(MaxDimension / ratio));
        }

        using var blurred = SourceImage.Scaled(decoded, blurWidth, blurHeight);

        var base64 = Convert.ToBase64String(ImageEncoder.Encode(blurred, "image/jpeg", BlurQuality));
        return $"data:image/jpeg;base64,{base64}";
    }

    /// <summary>
    /// Generates a blur placeholder from a file path.
    /// </summary>
    public async Task<string> GenerateFromFileAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await GenerateAsync(stream);
    }
}
