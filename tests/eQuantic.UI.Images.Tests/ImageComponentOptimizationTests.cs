using eQuantic.UI.Web.Components;
using eQuantic.UI.Web.Components.Display;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Images;

namespace eQuantic.UI.Images.Tests;

[Collection("ImageState")]
public class ImageComponentOptimizationTests : IDisposable
{
    public ImageComponentOptimizationTests()
    {
        // Reset state before each test
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
        ImageOptimizationState.DeviceSizes = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];
        ImageOptimizationState.ImageSizes = [32, 48, 64, 96, 128, 256, 384];
    }

    public void Dispose()
    {
        ImageOptimizationState.IsEnabled = false;
        ImageOptimizationState.DefaultQuality = 75;
        ImageOptimizationState.DeviceSizes = [640, 750, 828, 1080, 1200, 1920, 2048, 3840];
        ImageOptimizationState.ImageSizes = [32, 48, 64, 96, 128, 256, 384];
    }

    private static DynamicElement BuildImage(eQuantic.UI.Web.Components.Display.Image image)
    {
        var context = new RenderContext();
        var result = image.Build(context);

        // For Fill mode, the result is a wrapper div, get the inner img
        if (result is DynamicElement wrapper && wrapper.TagName == "div")
        {
            return wrapper.Children.OfType<DynamicElement>().First();
        }

        return (DynamicElement)result;
    }

    // ── Without optimization (default behavior) ──────────────────────────

    [Fact]
    public void Build_NoOptimization_UsesOriginalSrc()
    {
        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero"
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Be("/images/hero.jpg");
    }

    [Fact]
    public void Build_NoOptimization_UsesManualSrcSet()
    {
        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            SrcSet = "/images/hero-640.jpg 640w, /images/hero-1280.jpg 1280w"
        };

        var img = BuildImage(image);

        img.CustomAttributes["srcset"].Should().Be("/images/hero-640.jpg 640w, /images/hero-1280.jpg 1280w");
    }

    [Fact]
    public void Build_OptimizationDisabledGlobally_UsesOriginalSrc()
    {
        ImageOptimizationState.IsEnabled = false;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero"
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Be("/images/hero.jpg");
        img.CustomAttributes.Should().NotContainKey("srcset");
    }

    // ── With global optimization enabled ─────────────────────────────────

    [Fact]
    public void Build_GlobalOptimization_FixedSize_GeneratesDensityDescriptors()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("/_equantic/image?url=");
        img.CustomAttributes["src"].Should().Contain("w=640");
        img.CustomAttributes["srcset"].Should().Contain("1x");
        img.CustomAttributes["srcset"].Should().Contain("2x");
    }

    [Fact]
    public void Build_GlobalOptimization_Responsive_GeneratesWidthDescriptors()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero"
            // No Width/Height → responsive mode
        };

        var img = BuildImage(image);

        img.CustomAttributes["srcset"].Should().Contain("640w");
        img.CustomAttributes["srcset"].Should().Contain("1920w");
        img.CustomAttributes["srcset"].Should().Contain("3840w");
        img.CustomAttributes["sizes"].Should().Be("100vw");
    }

    [Fact]
    public void Build_GlobalOptimization_Responsive_UsesCustomSizes()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Sizes = "(max-width: 768px) 100vw, 50vw"
        };

        var img = BuildImage(image);

        img.CustomAttributes["sizes"].Should().Be("(max-width: 768px) 100vw, 50vw");
    }

    [Fact]
    public void Build_GlobalOptimization_UsesDefaultQuality()
    {
        ImageOptimizationState.IsEnabled = true;
        ImageOptimizationState.DefaultQuality = 80;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("q=80");
    }

    [Fact]
    public void Build_GlobalOptimization_CustomQuality_OverridesDefault()
    {
        ImageOptimizationState.IsEnabled = true;
        ImageOptimizationState.DefaultQuality = 75;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480,
            Quality = 90
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("q=90");
    }

    // ── Per-image Optimize property ──────────────────────────────────────

    [Fact]
    public void Build_PerImageOptimizeTrue_OverridesGlobalDisabled()
    {
        ImageOptimizationState.IsEnabled = false;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480,
            Optimize = true
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("/_equantic/image?url=");
    }

    [Fact]
    public void Build_PerImageOptimizeFalse_OverridesGlobalEnabled()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480,
            Optimize = false
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Be("/images/hero.jpg");
    }

    // ── External URLs are never optimized ────────────────────────────────

    [Fact]
    public void Build_ExternalUrl_NeverOptimized()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "https://cdn.example.com/image.jpg",
            Alt = "External",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Be("https://cdn.example.com/image.jpg");
    }

    [Fact]
    public void Build_ExternalUrl_WithOptimizeTrue_StillNotOptimized()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "https://cdn.example.com/image.jpg",
            Alt = "External",
            Optimize = true
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Be("https://cdn.example.com/image.jpg");
    }

    // ── URL encoding ─────────────────────────────────────────────────────

    [Fact]
    public void Build_Optimized_UrlIsEncoded()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/my photo.jpg",
            Alt = "Photo",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("%2Fimages%2Fmy%20photo.jpg");
    }

    // ── Width snapping ───────────────────────────────────────────────────

    [Fact]
    public void Build_FixedSize_SnapsWidthToAllowed()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 500, // Not in allowed sizes → snaps to 640
            Height = 375
        };

        var img = BuildImage(image);

        img.CustomAttributes["src"].Should().Contain("w=640");
    }

    [Fact]
    public void Build_FixedSize_2x_SnapsToAllowed()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        // 2x of 640 = 1280, should snap to nearest allowed (1200 or 1920 depending on direction)
        var srcset = img.CustomAttributes["srcset"];
        srcset.Should().Contain("2x");
    }

    // ── Fill mode ────────────────────────────────────────────────────────

    [Fact]
    public void Build_FillMode_Optimized_UsesResponsiveSrcset()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Fill = true
        };

        var img = BuildImage(image);

        // Fill mode should use responsive (width descriptors), not density descriptors
        img.CustomAttributes["srcset"].Should().Contain("640w");
        img.CustomAttributes["srcset"].Should().Contain("3840w");
    }

    // ── Existing features still work ─────────────────────────────────────

    [Fact]
    public void Build_Optimized_PreservesLazyLoading()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480
        };

        var img = BuildImage(image);

        img.CustomAttributes["loading"].Should().Be("lazy");
        img.CustomAttributes["decoding"].Should().Be("async");
    }

    [Fact]
    public void Build_Optimized_PreservesPriority()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480,
            Priority = true
        };

        var img = BuildImage(image);

        img.CustomAttributes["loading"].Should().Be("eager");
        img.CustomAttributes["fetchpriority"].Should().Be("high");
    }

    [Fact]
    public void Build_Optimized_PreservesBlurPlaceholder()
    {
        ImageOptimizationState.IsEnabled = true;

        var image = new eQuantic.UI.Web.Components.Display.Image
        {
            Src = "/images/hero.jpg",
            Alt = "Hero",
            Width = 640,
            Height = 480,
            Placeholder = ImagePlaceholder.Blur,
            BlurDataURL = "data:image/jpeg;base64,abc123"
        };

        var img = BuildImage(image);

        img.CustomAttributes["style"].Should().Contain("background-image");
        img.CustomAttributes["onload"].Should().Contain("filter");
    }
}
