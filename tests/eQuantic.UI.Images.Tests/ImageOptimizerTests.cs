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

    // Stored 64 × 32 in four quadrants: red, lime / blue, yellow. Each EXIF orientation is the turn
    // or mirror a viewer applies to show it, so the corners as displayed are known without Skia:
    // 2 mirrors left to right, 3 turns half way, 4 mirrors top to bottom, 5 transposes, 6 turns a
    // quarter clockwise, 7 transverses, 8 turns a quarter anticlockwise.
    [Theory]
    [InlineData(1, "red", "lime", "blue", "yellow")]
    [InlineData(2, "lime", "red", "yellow", "blue")]
    [InlineData(3, "yellow", "blue", "lime", "red")]
    [InlineData(4, "blue", "yellow", "red", "lime")]
    [InlineData(5, "red", "blue", "lime", "yellow")]
    [InlineData(6, "blue", "red", "yellow", "lime")]
    [InlineData(7, "yellow", "lime", "blue", "red")]
    [InlineData(8, "lime", "yellow", "red", "blue")]
    public async Task OptimizeAsync_AppliesEveryExifOrientation_ToThePixels(
        ushort orientation, string topLeft, string topRight, string bottomLeft, string bottomRight)
    {
        // The encoders write no EXIF, so the turn has to be in the pixels.
        var jpeg = TestImages.Quadrants(64, 32, SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow);
        using var source = TestImages.Stream(TestImages.WithOrientation(jpeg, orientation));

        var result = await _optimizer.OptimizeAsync(source, 640, 100, "image/png");

        var (width, height) = TestImages.SizeOf(result);
        (width, height).Should().Be(orientation >= 5 ? (32, 64) : (64, 32));
        TestImages.NameOf(TestImages.PixelAt(result, width / 4, height / 4)).Should().Be(topLeft);
        TestImages.NameOf(TestImages.PixelAt(result, width * 3 / 4, height / 4)).Should().Be(topRight);
        TestImages.NameOf(TestImages.PixelAt(result, width / 4, height * 3 / 4)).Should().Be(bottomLeft);
        TestImages.NameOf(TestImages.PixelAt(result, width * 3 / 4, height * 3 / 4)).Should().Be(bottomRight);
    }

    [Fact]
    public async Task OptimizeAsync_AnExactHalving_KeepsItsEdgeSharp()
    {
        // 1280 to 640 is one halving that lands on the size. A cubic pass after it would mix
        // 1/18 of each neighbour into every pixel, Mitchell not being interpolating.
        using var source = TestImages.Stream(
            TestImages.Halves(1280, 720, SKColors.Red, SKColors.Blue, SKEncodedImageFormat.Png));

        var result = await _optimizer.OptimizeAsync(source, 640, 75, "image/png");

        TestImages.SizeOf(result).Should().Be((640, 360));
        TestImages.PixelAt(result, 319, 180).Should().Be(SKColors.Red);
        TestImages.PixelAt(result, 320, 180).Should().Be(SKColors.Blue);
    }

    [Fact]
    public async Task OptimizeAsync_AnExtremeBanner_IsAveragedAlongItsLength()
    {
        // 4000 × 2 in two-pixel stripes, asked for at 100 wide (1 high). Its height stops halving
        // after one step; its length must keep halving, or one cubic step reads 4 of every 20
        // columns and the stripes alias into red and blue patches.
        using var source = TestImages.Stream(TestImages.Stripes(4000, 2, 2, SKColors.Red, SKColors.Blue));

        var result = await _optimizer.OptimizeAsync(source, 100, 75, "image/png");

        TestImages.SizeOf(result).Should().Be((100, 1));
        for (var x = 0; x < 100; x++)
        {
            var pixel = TestImages.PixelAt(result, x, 0);
            pixel.Red.Should().BeInRange(90, 165, $"column {x} averages red and blue");
            pixel.Blue.Should().BeInRange(90, 165, $"column {x} averages red and blue");
        }
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
    public async Task GetDimensionsAsync_ReadsOnlyTheHeader_AndLeavesTheStreamOpen()
    {
        // Measuring decodes nothing, so a size past the decode ceiling is answered.
        using var source = TestImages.Stream(TestImages.PngHeaderOnly(20_000, 20_000));

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((20_000, 20_000));
        source.CanRead.Should().BeTrue("the caller owns the stream");
    }

    [Fact]
    public async Task GetDimensionsAsync_ReadsAStreamThatRefusesSynchronousReads()
    {
        // An ASP.NET Core request body with AllowSynchronousIO off, its default. Skia reads
        // synchronously, so the header is read here first, asynchronously.
        var jpeg = TestImages.WithOrientation(TestImages.Solid(1920, 1080, SKColors.Red), 6);
        await using var source = new AsyncOnlyStream(jpeg);

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((1080, 1920));
    }

    [Fact]
    public async Task GetDimensionsAsync_ReadsOneBlock_NotTheWholeSource()
    {
        // A PNG followed by a megabyte its codec never needs.
        byte[] bytes = [.. TestImages.Solid(64, 32, SKColors.Red, SKEncodedImageFormat.Png), .. new byte[1024 * 1024]];
        await using var source = new AsyncOnlyStream(bytes);

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((64, 32));
        source.BytesRead.Should().Be(16 * 1024);
    }

    [Fact]
    public async Task GetDimensionsAsync_ReadsOnWhileTheHeaderOutgrowsTheBlock()
    {
        // 60 KB of metadata before the frame header: the blocks double (16, 16, 32 KB) until the
        // codec has it, the EXIF orientation included, and stop there.
        var jpeg = TestImages.WithOrientation(TestImages.WithPadding(TestImages.Solid(400, 200, SKColors.Red), 60_000), 6);
        byte[] bytes = [.. jpeg, .. new byte[1024 * 1024]];
        await using var source = new AsyncOnlyStream(bytes);

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((200, 400));
        source.BytesRead.Should().Be(64 * 1024);
    }

    [Fact]
    public async Task GetDimensionsAsync_RefusesBytesWithNoImageSignature_AfterOneBlock()
    {
        var text = System.Text.Encoding.ASCII.GetBytes(new string('x', 1024 * 1024));
        await using var source = new AsyncOnlyStream(text);

        var act = () => _optimizer.GetDimensionsAsync(source);

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*JPEG, PNG, GIF, WebP or BMP*");
        source.BytesRead.Should().Be(16 * 1024);
    }

    [Fact]
    public async Task GetDimensionsAsync_AnswersTheSizeAsDisplayed()
    {
        using var source = TestImages.Stream(TestImages.WithOrientation(TestImages.Solid(1920, 1080, SKColors.Red), 6));

        var (width, height) = await _optimizer.GetDimensionsAsync(source);

        (width, height).Should().Be((1080, 1920));
    }
}
