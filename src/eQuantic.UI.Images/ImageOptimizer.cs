using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace eQuantic.UI.Images;

/// <summary>
/// Core image processing service using SixLabors.ImageSharp.
/// Handles resizing and format conversion.
/// </summary>
public class ImageOptimizer
{
    /// <summary>
    /// Optimizes an image by resizing to the specified width and encoding in the target format.
    /// </summary>
    /// <param name="source">Source image stream.</param>
    /// <param name="width">Target width in pixels. Height is auto-calculated to maintain aspect ratio.</param>
    /// <param name="quality">Output quality (1-100).</param>
    /// <param name="outputFormat">Target MIME type (e.g., "image/webp", "image/jpeg", "image/png").</param>
    /// <returns>Optimized image bytes.</returns>
    public async Task<byte[]> OptimizeAsync(Stream source, int width, int quality, string outputFormat)
    {
        using var image = await Image.LoadAsync(source);

        // Only resize if the target width is smaller than the source
        if (image.Width > width)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, 0), // 0 = auto height maintaining aspect ratio
                Mode = ResizeMode.Max
            }));
        }

        using var output = new MemoryStream();

        switch (outputFormat)
        {
            case "image/webp":
                await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = quality });
                break;
            case "image/png":
                await image.SaveAsPngAsync(output, new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression
                });
                break;
            default: // image/jpeg and fallback
                await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = quality });
                break;
        }

        return output.ToArray();
    }

    /// <summary>
    /// Gets the dimensions of a source image without fully decoding it.
    /// </summary>
    public async Task<(int Width, int Height)> GetDimensionsAsync(Stream source)
    {
        var info = await Image.IdentifyAsync(source);
        return (info.Width, info.Height);
    }
}
