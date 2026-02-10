using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace eQuantic.UI.Images;

/// <summary>
/// Generates tiny blur placeholder images as base64 data URLs.
/// Following Next.js pattern: 8px max dimension, quality 70.
/// </summary>
public class BlurPlaceholderGenerator
{
    private const int MaxDimension = 8;
    private const int BlurQuality = 70;

    /// <summary>
    /// Generates a tiny blur placeholder as a base64 data URL.
    /// The output is an 8px-wide JPEG suitable for CSS background-image.
    /// </summary>
    /// <param name="source">Source image stream.</param>
    /// <returns>A data URL like "data:image/jpeg;base64,..."</returns>
    public async Task<string> GenerateAsync(Stream source)
    {
        using var image = await Image.LoadAsync(source);

        var ratio = (double)image.Height / image.Width;
        int blurWidth, blurHeight;

        if (image.Width >= image.Height)
        {
            blurWidth = MaxDimension;
            blurHeight = Math.Max(1, (int)(MaxDimension * ratio));
        }
        else
        {
            blurHeight = MaxDimension;
            blurWidth = Math.Max(1, (int)(MaxDimension / ratio));
        }

        image.Mutate(x => x.Resize(blurWidth, blurHeight));

        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = BlurQuality });

        var base64 = Convert.ToBase64String(output.ToArray());
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
