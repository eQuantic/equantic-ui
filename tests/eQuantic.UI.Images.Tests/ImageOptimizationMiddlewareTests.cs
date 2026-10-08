using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace eQuantic.UI.Images.Tests;

public class ImageOptimizationMiddlewareTests : IDisposable
{
    private readonly string _webRoot;
    private readonly string _cacheDir;

    public ImageOptimizationMiddlewareTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"equantic-webroot-{Guid.NewGuid():N}");
        _cacheDir = Path.Combine(Path.GetTempPath(), $"equantic-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "images"));

        // Create the test images in wwwroot/images/
        var images = Path.Combine(_webRoot, "images");
        File.WriteAllBytes(Path.Combine(images, "test.jpg"), TestImages.Solid(1920, 1080, SKColors.Red));
        File.WriteAllBytes(Path.Combine(images, "animated.gif"), TestImages.AnimatedGif());
        File.WriteAllBytes(Path.Combine(images, "huge.png"), TestImages.PngHeaderOnly(20_000, 20_000));
        File.WriteAllBytes(Path.Combine(images, "scan.tiff"), [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00]);
        File.WriteAllText(Path.Combine(images, "not-an-image.jpg"), "plain text under an image's name");
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, true);
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true);
    }

    private HttpContext CreateHttpContext(
        string? url = null,
        string? width = null,
        string? quality = null,
        string accept = "image/webp,image/*",
        string[]? formats = null)
    {
        var options = new ImageOptimizationOptions
        {
            CacheDirectory = _cacheDir
        };
        if (formats != null) options.Formats = formats;

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<ImageOptimizer>();
        services.AddSingleton<ImageCache>();
        services.AddLogging();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(_webRoot);
        services.AddSingleton(mockEnv.Object);

        var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var queryParams = new Dictionary<string, string?>();
        if (url != null) queryParams["url"] = url;
        if (width != null) queryParams["w"] = width;
        if (quality != null) queryParams["q"] = quality;

        context.Request.QueryString = QueryString.Create(queryParams!);
        context.Request.Headers["Accept"] = accept;
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task HandleAsync_MissingUrl_Returns400()
    {
        var context = CreateHttpContext(width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("url");
    }

    [Fact]
    public async Task HandleAsync_MissingWidth_Returns400()
    {
        var context = CreateHttpContext(url: "/images/test.jpg");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("w");
    }

    [Fact]
    public async Task HandleAsync_InvalidWidth_Returns400()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "abc");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_DisallowedWidth_Returns400()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "999");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("999");
        body.Should().Contain("Allowed");
    }

    [Fact]
    public async Task HandleAsync_ExternalUrl_Returns400()
    {
        var context = CreateHttpContext(url: "https://evil.com/image.jpg", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("local path");
    }

    [Fact]
    public async Task HandleAsync_PathTraversal_Returns400()
    {
        var context = CreateHttpContext(url: "/../../etc/passwd", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_NonExistentImage_Returns404()
    {
        var context = CreateHttpContext(url: "/images/nonexistent.jpg", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task HandleAsync_InvalidQuality_Returns400()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640", quality: "200");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("quality");
    }

    [Fact]
    public async Task HandleAsync_ZeroQuality_Returns400()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640", quality: "0");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Returns200WithImage()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640", quality: "75");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_SetsCacheHeaders()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640", quality: "75");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Contain("public");
        context.Response.Headers["Vary"].ToString().Should().Contain("Accept");
    }

    [Fact]
    public async Task HandleAsync_AcceptJpeg_ReturnsJpeg()
    {
        var context = CreateHttpContext(
            url: "/images/test.jpg", width: "640", quality: "75",
            accept: "image/jpeg");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task HandleAsync_NoAcceptHeader_DefaultsToJpeg()
    {
        var context = CreateHttpContext(
            url: "/images/test.jpg", width: "640", quality: "75",
            accept: "");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task HandleAsync_DefaultQuality_UsedWhenNotSpecified()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleAsync_ProducesCorrectDimensions()
    {
        var context = CreateHttpContext(url: "/images/test.jpg", width: "640", quality: "75");

        await ImageOptimizationMiddleware.HandleAsync(context);

        var optimized = ((MemoryStream)context.Response.Body).ToArray();
        TestImages.SizeOf(optimized).Width.Should().Be(640);
    }

    [Fact]
    public async Task HandleAsync_TiffSource_Returns400()
    {
        // SkiaSharp reads no TIFF, so the extension is refused before the file is opened.
        var context = CreateHttpContext(url: "/images/scan.tiff", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("image file");
    }

    [Fact]
    public async Task HandleAsync_BytesThatAreNotAnImage_Returns400()
    {
        var context = CreateHttpContext(url: "/images/not-an-image.jpg", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400, "a source the optimizer cannot read is the request's fault, not the server's");
        var body = await ReadResponseBody(context);
        body.Should().Contain("JPEG, PNG, GIF, WebP or BMP");
    }

    [Fact]
    public async Task HandleAsync_SourceWithMorePixelsThanItMayDecode_Returns400()
    {
        var context = CreateHttpContext(url: "/images/huge.png", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(400);
        var body = await ReadResponseBody(context);
        body.Should().Contain("20000 × 20000 pixels");
    }

    [Fact]
    public async Task HandleAsync_AnimatedSource_IsServedAsItIs_WithItsOwnType()
    {
        var context = CreateHttpContext(url: "/images/animated.gif", width: "640");

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("image/gif");
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(TestImages.AnimatedGif());
    }

    [Fact]
    public async Task HandleAsync_AFormatNoEncoderWrites_IsServedAsJpeg_AndLabelledSo()
    {
        // AVIF is a valid option and no encoder here writes it: the optimizer falls back to JPEG,
        // and the response says JPEG rather than the format that was asked for.
        var context = CreateHttpContext(
            url: "/images/test.jpg", width: "640", accept: "image/avif,image/*",
            formats: ["image/avif"]);

        await ImageOptimizationMiddleware.HandleAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("image/jpeg");
        TestImages.FormatOf(((MemoryStream)context.Response.Body).ToArray()).Should().Be(SKEncodedImageFormat.Jpeg);
    }
}
