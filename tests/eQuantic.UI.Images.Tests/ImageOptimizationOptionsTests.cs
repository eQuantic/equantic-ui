namespace eQuantic.UI.Images.Tests;

public class ImageOptimizationOptionsTests
{
    [Fact]
    public void DefaultQuality_Is75()
    {
        var options = new ImageOptimizationOptions();

        options.DefaultQuality.Should().Be(75);
    }

    [Fact]
    public void DefaultFormats_IsWebpOnly()
    {
        var options = new ImageOptimizationOptions();

        options.Formats.Should().BeEquivalentTo(["image/webp"]);
    }

    [Fact]
    public void DefaultCacheTtl_Is4Hours()
    {
        var options = new ImageOptimizationOptions();

        options.CacheTtlSeconds.Should().Be(14400);
    }

    [Fact]
    public void DefaultMaxSourceSize_Is10MB()
    {
        var options = new ImageOptimizationOptions();

        options.MaxSourceSize.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public void DefaultDeviceSizes_MatchesNextJsDefaults()
    {
        var options = new ImageOptimizationOptions();

        options.DeviceSizes.Should().BeEquivalentTo([640, 750, 828, 1080, 1200, 1920, 2048, 3840]);
    }

    [Fact]
    public void DefaultImageSizes_MatchesNextJsDefaults()
    {
        var options = new ImageOptimizationOptions();

        options.ImageSizes.Should().BeEquivalentTo([32, 48, 64, 96, 128, 256, 384]);
    }

    [Fact]
    public void AllowedWidths_CombinesAndSorts()
    {
        var options = new ImageOptimizationOptions();

        var allowed = options.AllowedWidths;

        allowed.Should().BeInAscendingOrder();
        allowed.First().Should().Be(32);
        allowed.Last().Should().Be(3840);
    }

    [Fact]
    public void AllowedWidths_HasNoDuplicates()
    {
        var options = new ImageOptimizationOptions();

        options.AllowedWidths.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllowedWidths_WithCustomSizes_CombinesCorrectly()
    {
        var options = new ImageOptimizationOptions
        {
            DeviceSizes = [800, 1600],
            ImageSizes = [50, 100, 800] // 800 duplicated with DeviceSizes
        };

        var allowed = options.AllowedWidths;

        allowed.Should().BeEquivalentTo([50, 100, 800, 1600]);
        allowed.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DefaultCacheDirectory_IsObjBased()
    {
        var options = new ImageOptimizationOptions();

        options.CacheDirectory.Should().Contain("obj");
        options.CacheDirectory.Should().Contain("image-cache");
    }

    [Fact]
    public void AllProperties_AreSettable()
    {
        var options = new ImageOptimizationOptions
        {
            DefaultQuality = 90,
            CacheTtlSeconds = 86400,
            CacheDirectory = "/tmp/cache",
            MaxSourceSize = 5 * 1024 * 1024,
            Formats = ["image/avif", "image/webp"],
            DeviceSizes = [1024, 2048],
            ImageSizes = [64, 128]
        };

        options.DefaultQuality.Should().Be(90);
        options.CacheTtlSeconds.Should().Be(86400);
        options.CacheDirectory.Should().Be("/tmp/cache");
        options.MaxSourceSize.Should().Be(5 * 1024 * 1024);
        options.Formats.Should().BeEquivalentTo(["image/avif", "image/webp"]);
        options.DeviceSizes.Should().BeEquivalentTo([1024, 2048]);
        options.ImageSizes.Should().BeEquivalentTo([64, 128]);
    }
}
