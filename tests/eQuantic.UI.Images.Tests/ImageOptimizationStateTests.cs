using eQuantic.UI.Core.Images;

namespace eQuantic.UI.Images.Tests;

public class ImageOptimizationStateTests : IDisposable
{
    public ImageOptimizationStateTests()
    {
        // Reset static state before each test
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
        ImageOptimizationState.DeviceSizes = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];
        ImageOptimizationState.ImageSizes = [32, 48, 64, 96, 128, 256, 384];
    }

    public void Dispose()
    {
        // Reset static state after each test
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
        ImageOptimizationState.DeviceSizes = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];
        ImageOptimizationState.ImageSizes = [32, 48, 64, 96, 128, 256, 384];
    }

    [Fact]
    public void IsEnabled_DefaultsToFalse()
    {
        ImageOptimizationState.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void DefaultQuality_DefaultsTo75()
    {
        ImageOptimizationState.DefaultQuality.Should().Be(75);
    }

    [Fact]
    public void AllowedWidths_CombinesAndSortsSizes()
    {
        var allowed = ImageOptimizationState.AllowedWidths;

        allowed.Should().BeInAscendingOrder();
        allowed.Should().Contain(32);   // From ImageSizes
        allowed.Should().Contain(384);  // From ImageSizes
        allowed.Should().Contain(640);  // From DeviceSizes
        allowed.Should().Contain(3840); // From DeviceSizes
    }

    [Fact]
    public void AllowedWidths_HasNoDuplicates()
    {
        var allowed = ImageOptimizationState.AllowedWidths;

        allowed.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllowedWidths_ContainsAllDeviceAndImageSizes()
    {
        var allowed = ImageOptimizationState.AllowedWidths;

        foreach (var size in ImageOptimizationState.DeviceSizes)
            allowed.Should().Contain(size);

        foreach (var size in ImageOptimizationState.ImageSizes)
            allowed.Should().Contain(size);
    }

    [Theory]
    [InlineData(100, 128)]   // Snaps up to nearest allowed
    [InlineData(32, 32)]     // Exact match
    [InlineData(640, 640)]   // Exact match (device size)
    [InlineData(1, 32)]      // Smallest allowed
    [InlineData(500, 640)]   // Between image and device sizes
    [InlineData(700, 750)]   // Between device sizes
    public void SnapToAllowed_SnapsUpToNearest(int input, int expected)
    {
        var result = ImageOptimizationState.SnapToAllowed(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void SnapToAllowed_BeyondLargest_ReturnsLargest()
    {
        var result = ImageOptimizationState.SnapToAllowed(5000);

        result.Should().Be(3840);
    }

    [Fact]
    public void SnapToAllowed_WithCustomSizes_UsesCustomValues()
    {
        ImageOptimizationState.DeviceSizes = [800, 1600];
        ImageOptimizationState.ImageSizes = [100, 200];

        var result = ImageOptimizationState.SnapToAllowed(150);

        result.Should().Be(200);
    }

    [Fact]
    public void AllowedWidths_ReflectsCustomSizes()
    {
        ImageOptimizationState.DeviceSizes = [1000, 2000];
        ImageOptimizationState.ImageSizes = [50, 100];

        var allowed = ImageOptimizationState.AllowedWidths;

        allowed.Should().BeEquivalentTo([50, 100, 1000, 2000]);
        allowed.Should().BeInAscendingOrder();
    }
}
