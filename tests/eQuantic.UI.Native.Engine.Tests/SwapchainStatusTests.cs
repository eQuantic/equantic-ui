using eQuantic.UI.Native.Engine.Vulkan;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The presentation engine's answers, as a frame reads them. A rotation crashed every Android app
/// because SUBOPTIMAL was tested under a value Vulkan does not define, so the numbers are pinned
/// here against the specification's own, not against the code's names for them.
/// </summary>
public class SwapchainStatusTests
{
    [Fact]
    public void TheNamesHoldTheSpecificationsValues()
    {
        SwapchainStatus.Suboptimal.Should().Be(1000001003, "VK_SUBOPTIMAL_KHR is a success code");
        SwapchainStatus.OutOfDate.Should().Be(-1000001004, "VK_ERROR_OUT_OF_DATE_KHR");
    }

    [Fact]
    public void ASuboptimalSwapchainStillShowsTheFrame()
    {
        SwapchainStatus.Usable(1000001003, "queue present").Should().BeTrue(
            "the image was presented; Android answers this on the first frame after a rotation");
        SwapchainStatus.Usable(0, "queue present").Should().BeTrue();
    }

    [Fact]
    public void AnOutOfDateSwapchainIsRebuiltFirst() =>
        SwapchainStatus.Usable(-1000001004, "queue present").Should().BeFalse();

    [Fact]
    public void AnythingElseIsAnError()
    {
        var lost = () => SwapchainStatus.Usable(-1000000000, "queue present"); // SURFACE_LOST
        lost.Should().Throw<InvalidOperationException>().WithMessage("*queue present*-1000000000*");
    }
}
