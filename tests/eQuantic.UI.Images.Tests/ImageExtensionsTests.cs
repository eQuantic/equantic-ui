using eQuantic.UI.Images;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Images.Tests;

[Collection("ImageState")]
public class ImageExtensionsTests : IDisposable
{
    public ImageExtensionsTests()
    {
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
    }

    public void Dispose()
    {
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
        ImageOptimizationState.DeviceSizes = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];
        ImageOptimizationState.ImageSizes = [32, 48, 64, 96, 128, 256, 384];
    }

    [Fact]
    public void AddImageOptimization_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddImageOptimization();

        var provider = services.BuildServiceProvider();

        provider.GetService<ImageOptimizationOptions>().Should().NotBeNull();
        provider.GetService<ImageOptimizer>().Should().NotBeNull();
        provider.GetService<ImageCache>().Should().NotBeNull();
        provider.GetService<BlurPlaceholderGenerator>().Should().NotBeNull();
    }

    [Fact]
    public void AddImageOptimization_WithConfiguration_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddImageOptimization(opts =>
        {
            opts.DefaultQuality = 90;
            opts.CacheTtlSeconds = 86400;
            opts.Formats = ["image/avif", "image/webp"];
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ImageOptimizationOptions>();

        options.DefaultQuality.Should().Be(90);
        options.CacheTtlSeconds.Should().Be(86400);
        options.Formats.Should().BeEquivalentTo(["image/avif", "image/webp"]);
    }

    [Fact]
    public void AddImageOptimization_DefaultOptions_HasCorrectDefaults()
    {
        var services = new ServiceCollection();

        services.AddImageOptimization();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ImageOptimizationOptions>();

        options.DefaultQuality.Should().Be(75);
        options.CacheTtlSeconds.Should().Be(14400);
        options.Formats.Should().BeEquivalentTo(["image/webp"]);
        options.MaxSourceSize.Should().Be(10 * 1024 * 1024);
        options.DeviceSizes.Should().BeEquivalentTo([640, 750, 828, 1080, 1200, 1920, 2048, 3840]);
        options.ImageSizes.Should().BeEquivalentTo([32, 48, 64, 96, 128, 256, 384]);
    }

    [Fact]
    public void AddImageOptimization_NullConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();

        services.AddImageOptimization(null);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ImageOptimizationOptions>();

        options.DefaultQuality.Should().Be(75);
    }
}
