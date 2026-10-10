using SkiaSharp;

namespace eQuantic.UI.Images.Tests;

public class BlurPlaceholderGeneratorTests
{
    private readonly BlurPlaceholderGenerator _generator = new(new ImageOptimizationOptions());

    private static Stream CreateTestImage(int width, int height) =>
        TestImages.Stream(TestImages.Solid(width, height, SKColors.Blue));

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

        var blurImage = TestImages.SizeOf(bytes);
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

        var blurImage = TestImages.SizeOf(bytes);
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

        var blurImage = TestImages.SizeOf(bytes);
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

        var blurImage = TestImages.SizeOf(bytes);
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

        var blurImage = TestImages.SizeOf(bytes);
        blurImage.Width.Should().BeLessThanOrEqualTo(8);
        blurImage.Height.Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task GenerateAsync_OrientedSource_IsShownAsDisplayed()
    {
        // Stored landscape with orientation 6: displayed portrait, so the placeholder is too.
        var jpeg = TestImages.WithOrientation(TestImages.Solid(1920, 1080, SKColors.Blue), 6);
        using var source = TestImages.Stream(jpeg);

        var result = await _generator.GenerateAsync(source);
        var bytes = Convert.FromBase64String(result.Replace("data:image/jpeg;base64,", ""));

        var blurImage = TestImages.SizeOf(bytes);
        blurImage.Height.Should().Be(8);
        blurImage.Width.Should().BeLessThan(8);
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
            await File.WriteAllBytesAsync(tempFile, TestImages.Solid(800, 600, SKColors.Green));

            var result = await _generator.GenerateFromFileAsync(tempFile);

            result.Should().StartWith("data:image/jpeg;base64,");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateAsync_RefusesASourcePastMaxSourceSize_WithoutReadingItToItsEnd()
    {
        var jpeg = TestImages.WithPaddingSegments(TestImages.Solid(64, 32, SKColors.Blue), 16);
        await using var source = new AsyncOnlyStream(jpeg);
        var generator = new BlurPlaceholderGenerator(new ImageOptimizationOptions { MaxSourceSize = 64 * 1024 });

        var act = () => generator.GenerateAsync(source);

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*MaxSourceSize*");
        source.BytesRead.Should().BeLessThan(256 * 1024);
    }
}
