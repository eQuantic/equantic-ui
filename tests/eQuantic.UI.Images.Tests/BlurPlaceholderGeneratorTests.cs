using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eQuantic.UI.Images.Tests;

public class BlurPlaceholderGeneratorTests
{
    private readonly BlurPlaceholderGenerator _generator = new();

    private static Stream CreateTestImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height, Color.Blue);
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task GenerateAsync_ReturnsDataUrl()
    {
        using var source = CreateTestImage(1920, 1080);

        var result = await _generator.GenerateAsync(source);

        result.Should().StartWith("data:image/jpeg;base64,");
    }

    [Fact]
    public async Task GenerateAsync_ProducesValidBase64()
    {
        using var source = CreateTestImage(1920, 1080);

        var result = await _generator.GenerateAsync(source);

        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ProducesTinyImage()
    {
        using var source = CreateTestImage(1920, 1080);

        var result = await _generator.GenerateAsync(source);

        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);

        using var blurImage = Image.Load(bytes);
        blurImage.Width.Should().BeLessThanOrEqualTo(8);
        blurImage.Height.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task GenerateAsync_LandscapeImage_MaxWidth8()
    {
        using var source = CreateTestImage(1920, 1080); // Landscape

        var result = await _generator.GenerateAsync(source);
        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);

        using var blurImage = Image.Load(bytes);
        blurImage.Width.Should().Be(8);
        blurImage.Height.Should().BeGreaterThan(0);
        blurImage.Height.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task GenerateAsync_PortraitImage_MaxHeight8()
    {
        using var source = CreateTestImage(1080, 1920); // Portrait

        var result = await _generator.GenerateAsync(source);
        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);

        using var blurImage = Image.Load(bytes);
        blurImage.Height.Should().Be(8);
        blurImage.Width.Should().BeGreaterThan(0);
        blurImage.Width.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task GenerateAsync_SquareImage_Produces8x8()
    {
        using var source = CreateTestImage(1000, 1000);

        var result = await _generator.GenerateAsync(source);
        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);

        using var blurImage = Image.Load(bytes);
        blurImage.Width.Should().Be(8);
        blurImage.Height.Should().Be(8);
    }

    [Fact]
    public async Task GenerateAsync_SmallImage_StillProducesTinyOutput()
    {
        using var source = CreateTestImage(16, 16); // Already small

        var result = await _generator.GenerateAsync(source);

        result.Should().StartWith("data:image/jpeg;base64,");
        var base64Part = result.Replace("data:image/jpeg;base64,", "");
        var bytes = Convert.FromBase64String(base64Part);

        using var blurImage = Image.Load(bytes);
        blurImage.Width.Should().BeLessThanOrEqualTo(8);
        blurImage.Height.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task GenerateAsync_DataUrlIsCompact()
    {
        using var source = CreateTestImage(1920, 1080);

        var result = await _generator.GenerateAsync(source);

        // The data URL for an 8px blur should be very small (< 1KB)
        result.Length.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task GenerateFromFileAsync_ReadsFromFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write a test image to file
            using (var image = new Image<Rgba32>(800, 600, Color.Green))
            {
                await image.SaveAsJpegAsync(tempFile);
            }

            var result = await _generator.GenerateFromFileAsync(tempFile);

            result.Should().StartWith("data:image/jpeg;base64,");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
