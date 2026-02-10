using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Images;

/// <summary>
/// Handles the /_equantic/image endpoint for on-demand image optimization.
/// Validates parameters, negotiates format, caches results, supports ETag.
/// </summary>
public static class ImageOptimizationMiddleware
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff"
    };

    /// <summary>
    /// Handles an image optimization request.
    /// Query params: url (source path), w (width), q (quality).
    /// </summary>
    public static async Task HandleAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<ImageOptimizationOptions>();
        var optimizer = context.RequestServices.GetRequiredService<ImageOptimizer>();
        var cache = context.RequestServices.GetRequiredService<ImageCache>();
        var logger = context.RequestServices.GetService<ILogger<ImageOptimizer>>();

        // Parse and validate query parameters
        var urlParam = context.Request.Query["url"].FirstOrDefault();
        var widthParam = context.Request.Query["w"].FirstOrDefault();
        var qualityParam = context.Request.Query["q"].FirstOrDefault();

        if (string.IsNullOrEmpty(urlParam))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Missing required parameter: url");
            return;
        }

        // Security: only allow local paths (no external URLs)
        if (!urlParam.StartsWith("/") || urlParam.Contains(".."))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid url: must be a local path starting with /");
            return;
        }

        // Security: only allow image file extensions
        var ext = Path.GetExtension(urlParam);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid url: must be an image file (.jpg, .png, .webp, etc.)");
            return;
        }

        if (!int.TryParse(widthParam, out var width) || width <= 0)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid or missing parameter: w (width)");
            return;
        }

        // Validate width is in allowed sizes
        var allowedWidths = options.AllowedWidths;
        if (!allowedWidths.Contains(width))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(
                $"Invalid width: {width}. Allowed values: {string.Join(", ", allowedWidths)}");
            return;
        }

        var quality = options.DefaultQuality;
        if (!string.IsNullOrEmpty(qualityParam))
        {
            if (!int.TryParse(qualityParam, out quality) || quality < 1 || quality > 100)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid parameter: q (quality must be 1-100)");
                return;
            }
        }

        // Resolve source file path
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var sourcePath = Path.Combine(env.WebRootPath, urlParam.TrimStart('/'));

        // Security: verify resolved path is still within wwwroot
        var resolvedPath = Path.GetFullPath(sourcePath);
        var webRootFull = Path.GetFullPath(env.WebRootPath);
        if (!resolvedPath.StartsWith(webRootFull, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid url: path traversal detected");
            return;
        }

        if (!File.Exists(sourcePath))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"Image not found: {urlParam}");
            return;
        }

        // Check file size limit
        var fileInfo = new FileInfo(sourcePath);
        if (fileInfo.Length > options.MaxSourceSize)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(
                $"Image too large: {fileInfo.Length} bytes (max: {options.MaxSourceSize})");
            return;
        }

        // Content negotiation: determine output format from Accept header
        var outputFormat = NegotiateFormat(context.Request.Headers.Accept.ToString(), options.Formats);

        try
        {
            // Get or create optimized image (with caching and dedup)
            var data = await cache.GetOrCreateAsync(urlParam, width, quality, outputFormat, async () =>
            {
                logger?.LogInformation(
                    "Optimizing image: {Url} -> {Width}px, q={Quality}, format={Format}",
                    urlParam, width, quality, outputFormat);

                await using var stream = File.OpenRead(sourcePath);
                return await optimizer.OptimizeAsync(stream, width, quality, outputFormat);
            });

            // ETag support for conditional requests (304 Not Modified)
            var etag = ComputeETag(data);
            context.Response.Headers["ETag"] = etag;

            var ifNoneMatch = context.Request.Headers.IfNoneMatch.FirstOrDefault();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            // Set response headers
            context.Response.ContentType = outputFormat;
            context.Response.Headers["Cache-Control"] = $"public, max-age={options.CacheTtlSeconds}";
            context.Response.Headers["Vary"] = "Accept";
            context.Response.ContentLength = data.Length;

            await context.Response.Body.WriteAsync(data);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to optimize image: {Url}", urlParam);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Failed to optimize image");
        }
    }

    /// <summary>
    /// Negotiates the best output format based on the Accept header and configured formats.
    /// Returns the first configured format that the client supports, or falls back to JPEG.
    /// </summary>
    private static string NegotiateFormat(string acceptHeader, string[] configuredFormats)
    {
        if (string.IsNullOrEmpty(acceptHeader))
            return "image/jpeg";

        foreach (var format in configuredFormats)
        {
            if (acceptHeader.Contains(format, StringComparison.OrdinalIgnoreCase))
                return format;
        }

        // Fallback: if the client accepts any image, use JPEG
        return "image/jpeg";
    }

    /// <summary>
    /// Computes a weak ETag from the image data using SHA256.
    /// </summary>
    private static string ComputeETag(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return $"\"{Convert.ToHexString(hash)[..16].ToLowerInvariant()}\"";
    }
}
