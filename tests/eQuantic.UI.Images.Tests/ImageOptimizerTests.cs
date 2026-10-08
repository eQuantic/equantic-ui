using SkiaSharp;

namespace eQuantic.UI.Images.Tests;

public class ImageOptimizerTests
{
    private readonly ImageOptimizer _optimizer = new();

    private static Stream CreateTestImage(int width, int height, string format = "jpeg") =>
        TestImages.Stream(TestImages.Solid(width, height, SKColors.Red,
            format == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg));

    [Fact]
    public async Task OptimizeAsync_ResizesImage_MaintainsAspectRatio()
    {
        using var source = CreateTestImage(1000, 500);

        var result = await _optimizer.OptimizeAsync(source, 500, 75, "image/jpeg");

        TestImages.SizeOf(result).Should().Be((500, 250)); // 1000:500 = 2:1 ratio maintained
    }

    [Fact]
    public async Task OptimizeAsync_DoesNotUpscale_WhenTargetWidthLarger()
    {
        using var source = CreateTestImage(400, 300);

        var result = await _optimizer.OptimizeAsync(source, 800, 75, "image/jpeg");

        TestImages.SizeOf(result).Should().Be((400, 300)); // Should NOT upscale
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
        TestImages.FormatOf(result).Should().Be(SKEncodedImageFormat.Webp);
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
        // A solid colour compresses to almost nothing at any quality, so the comparison needs
        // an edge for the quantization to spend bits on.
        var halves = TestImages.Halves(800, 600, SKColors.Red, SKColors.Blue, SKEncodedImageFormat.Jpeg);
        using var source1 = TestImages.Stream(halves);
        using var source2 = TestImages.Stream(halves);

        var lowQuality = await _optimizer.OptimizeAsync(source1, 400, 10, "image/jpeg");
        var highQuality = await _optimizer.OptimizeAsync(source2, 400, 100, "image/jpeg");

        lowQuality.Length.Should().BeLessThan(highQuality.Length);
    }

    [Fact]
    public async Task OptimizeAsync_SquareImage_MaintainsSquare()
    {
        using var source = CreateTestImage(1000, 1000);

        var result = await _optimizer.OptimizeAsync(source, 500, 75, "image/jpeg");

        TestImages.SizeOf(result).Should().Be((500, 500));
    }

    [Fact]
    public async Task OptimizeAsync_PortraitImage_MaintainsAspectRatio()
    {
        using var source = CreateTestImage(500, 1000); // Tall image

        var result = await _optimizer.OptimizeAsync(source, 250, 75, "image/jpeg");

        TestImages.SizeOf(result).Should().Be((250, 500)); // 500:1000 = 1:2 ratio maintained
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
    public async Task OptimizeAsync_ShrinksToTheTargetWidth_ThroughEveryHalving()
    {
        // 3840 to 640 halves twice (to 960) before the cubic step: the size is the target's, and
        // a colour that fills the source fills the result.
        using var source = TestImages.Stream(TestImages.Solid(3840, 2160, SKColors.Blue, SKEncodedImageFormat.Png));

        var result = await _optimizer.OptimizeAsync(source, 640, 90, "image/png");

        TestImages.SizeOf(result).Should().Be((640, 360));
        TestImages.PixelAt(result, 320, 180).Should().Be(SKColors.Blue);
    }

    [Fact]
    public async Task OptimizeAsync_AppliesTheExifOrientation_BeforeItResizes()
    {
        // Stored 40 × 20, red on the left, with orientation 6: a camera held upright, whose
        // picture is shown turned a quarter clockwise, 20 × 40 with red on top. The encoders
        // write no EXIF, so the turn has to be in the pixels.
        var jpeg = TestImages.Halves(40, 20, SKColors.Red, SKColors.Blue, SKEncodedImageFormat.Jpeg);
        using var source = TestImages.Stream(TestImages.WithOrientation(jpeg, 6));

        var result = await _optimizer.OptimizeAsync(source, 640, 100, "image/png");

        TestImages.SizeOf(result).Should().Be((20, 40));
        TestImages.PixelAt(result, 10, 5).Red.Should().BeGreaterThan(200, "the stored left edge is the displayed top");
        TestImages.PixelAt(result, 10, 35).Blue.Should().BeGreaterThan(200, "the stored right edge is the displayed bottom");
    }

    [Fact]
    public async Task OptimizeAsync_ResizesAnOrientedImage_ByItsDisplayedWidth()
    {
        var jpeg = TestImages.Solid(400, 200, SKColors.Red);
        using var source = TestImages.Stream(TestImages.WithOrientation(jpeg, 8));

        var result = await _optimizer.OptimizeAsync(source, 100, 75, "image/jpeg");

        TestImages.SizeOf(result).Should().Be((100, 200));
    }

    [Theory]
    [InlineData("image/jpeg", "ICC_PROFILE")]
    [InlineData("image/webp", "ICCP")]
    public async Task OptimizeAsync_WritesTheSrgbPixelsUntagged(string format, string profileMarker)
    {
        // Tagged, Skia embeds a 472-byte sRGB profile in every result; untagged is read as sRGB.
        using var source = CreateTestImage(800, 600);

        var result = await _optimizer.OptimizeAsync(source, 400, 75, format);

        System.Text.Encoding.ASCII.GetString(result).Should().NotContain(profileMarker);
    }

    [Fact]
    public async Task OptimizeAsync_HandsAnAnimatedSourceBack_AsItIs()
    {
        var gif = TestImages.AnimatedGif();
        using var source = TestImages.Stream(gif);

        var result = await _optimizer.OptimizeAsync(source, 640, 75, "image/webp");

        result.Should().Equal(gif, "no encoder here writes frames, and the first alone would stop the animation");
    }

    [Fact]
    public async Task OptimizeAsync_RefusesAFormatItDoesNotRead()
    {
        // A little-endian TIFF header: SkiaSharp reads no TIFF, and the optimizer reads only the
        // web's formats.
        using var source = TestImages.Stream([0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var act = () => _optimizer.OptimizeAsync(source, 640, 75, "image/webp");

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*JPEG, PNG, GIF, WebP or BMP*");
    }

    [Fact]
    public async Task OptimizeAsync_RefusesASourceWithMorePixelsThanItMayDecode_FromItsHeader()
    {
        using var source = TestImages.Stream(TestImages.PngHeaderOnly(20_000, 20_000));

        var act = () => _optimizer.OptimizeAsync(source, 640, 75, "image/webp");

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*20000 × 20000 pixels*");
    }

    [Fact]
    public async Task GetDimensionsAsync_ReturnsCorrectDimensions()
    {
        using var source = CreateTestImage(1920, 1080);

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        width.Should().Be(1920);
        height.Should().Be(1080);
    }

    [Fact]
    public async Task GetDimensionsAsync_AnswersTheSizeAsDisplayed()
    {
        using var source = TestImages.Stream(TestImages.WithOrientation(TestImages.Solid(1920, 1080, SKColors.Red), 6));

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((1080, 1920));
    }
}
