using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eQuantic.UI.Images.Tests;

[Collection("ImageState")]
public class ImageOptimizationMiddlewareTests : IDisposable
{
    private readonly string _webRoot;
    private readonly string _cacheDir;

    public ImageOptimizationMiddlewareTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), $"equantic-webroot-{Guid.NewGuid():N}");
        _cacheDir = Path.Combine(Path.GetTempPath(), $"equantic-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "images"));

        // Create a test image in wwwroot/images/
        using var image = new Image<Rgba32>(1920, 1080, Color.Red);
        image.SaveAsJpeg(Path.Combine(_webRoot, "images", "test.jpg"));
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
        string accept = "image/webp,image/*")
    {
        var options = new ImageOptimizationOptions
        {
            CacheDirectory = _cacheDir
        };

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

        context.Response.Body.Position = 0;
        using var optimized = await Image.LoadAsync(context.Response.Body);
        optimized.Width.Should().Be(640);
    }
}
