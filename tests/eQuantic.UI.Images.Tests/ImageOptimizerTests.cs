using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace eQuantic.UI.Images.Tests;

public class ImageOptimizerTests
{
    private readonly ImageOptimizer _optimizer = new();

    private static Stream CreateTestImage(int width, int height, string format = "jpeg")
    {
        var image = new Image<Rgba32>(width, height, Color.Red);
        var stream = new MemoryStream();

        if (format == "png")
            image.SaveAsPng(stream);
        else
            image.SaveAsJpeg(stream);

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task OptimizeAsync_ResizesImage_MaintainsAspectRatio()
    {
        using var source = CreateTestImage(1000, 500);

        var result = await _optimizer.OptimizeAsync(source, 500, 75, "image/jpeg");

        using var optimized = Image.Load(result);
        optimized.Width.Should().Be(500);
        optimized.Height.Should().Be(250); // 1000:500 = 2:1 ratio maintained
    }

    [Fact]
    public async Task OptimizeAsync_DoesNotUpscale_WhenTargetWidthLarger()
    {
        using var source = CreateTestImage(400, 300);

        var result = await _optimizer.OptimizeAsync(source, 800, 75, "image/jpeg");

        using var optimized = Image.Load(result);
        optimized.Width.Should().Be(400); // Should NOT upscale
        optimized.Height.Should().Be(300);
    }

    [Fact]
    public async Task OptimizeAsync_WebpFormat_ProducesWebpOutput()
    {
        using var source = CreateTestImage(800, 600);

        var result = await _optimizer.OptimizeAsync(source, 400, 75, "image/webp");

        result.Should().NotBeEmpty();
        // WebP files start with "RIFF" magic bytes
        result[0].Should().Be(0x52); // 'R'
        result[1].Should().Be(0x49); // 'I'
        result[2].Should().Be(0x46); // 'F'
        result[3].Should().Be(0x46); // 'F'
    }

    [Fact]
    public async Task OptimizeAsync_JpegFormat_ProducesJpegOutput()
    {
        using var source = CreateTestImage(800, 600);

        var result = await _optimizer.OptimizeAsync(source, 400, 75, "image/jpeg");

        result.Should().NotBeEmpty();
        // JPEG files start with 0xFF 0xD8 magic bytes
        result[0].Should().Be(0xFF);
        result[1].Should().Be(0xD8);
    }

    [Fact]
    public async Task OptimizeAsync_PngFormat_ProducesPngOutput()
    {
        using var source = CreateTestImage(800, 600, "png");

        var result = await _optimizer.OptimizeAsync(source, 400, 75, "image/png");

        result.Should().NotBeEmpty();
        // PNG files start with specific magic bytes
        result[0].Should().Be(0x89);
        result[1].Should().Be(0x50); // 'P'
        result[2].Should().Be(0x4E); // 'N'
        result[3].Should().Be(0x47); // 'G'
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task OptimizeAsync_DifferentQualities_ProducesDifferentSizes(int quality)
    {
        using var source = CreateTestImage(800, 600);

        var result = await _optimizer.OptimizeAsync(source, 400, quality, "image/jpeg");

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OptimizeAsync_LowQuality_SmallerThanHighQuality()
    {
        using var source1 = CreateTestImage(800, 600);
        using var source2 = CreateTestImage(800, 600);

        var lowQuality = await _optimizer.OptimizeAsync(source1, 400, 10, "image/jpeg");
        var highQuality = await _optimizer.OptimizeAsync(source2, 400, 100, "image/jpeg");

        lowQuality.Length.Should().BeLessThan(highQuality.Length);
    }

    [Fact]
    public async Task OptimizeAsync_SquareImage_MaintainsSquare()
    {
        using var source = CreateTestImage(1000, 1000);

        var result = await _optimizer.OptimizeAsync(source, 500, 75, "image/jpeg");

        using var optimized = Image.Load(result);
        optimized.Width.Should().Be(500);
        optimized.Height.Should().Be(500);
    }

    [Fact]
    public async Task OptimizeAsync_PortraitImage_MaintainsAspectRatio()
    {
        using var source = CreateTestImage(500, 1000); // Tall image

        var result = await _optimizer.OptimizeAsync(source, 250, 75, "image/jpeg");

        using var optimized = Image.Load(result);
        optimized.Width.Should().Be(250);
        optimized.Height.Should().Be(500); // 500:1000 = 1:2 ratio maintained
    }

    [Fact]
    public async Task OptimizeAsync_UnknownFormat_DefaultsToJpeg()
    {
        using var source = CreateTestImage(800, 600);

        var result = await _optimizer.OptimizeAsync(source, 400, 75, "image/unknown");

        result.Should().NotBeEmpty();
        // Should produce JPEG
        result[0].Should().Be(0xFF);
        result[1].Should().Be(0xD8);
    }

    [Fact]
    public async Task GetDimensionsAsync_ReturnsCorrectDimensions()
    {
        using var source = CreateTestImage(1920, 1080);

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        width.Should().Be(1920);
        height.Should().Be(1080);
    }
}
