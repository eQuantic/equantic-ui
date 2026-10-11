using eQuantic.UI.Native.Engine.Vulkan;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The size and orientation a swapchain is made in (#794). Measured on the Android emulator before
/// these rules: a phone turned with the app in front kept a portrait buffer in a landscape window,
/// because the surface's current extent still described the old buffer when the rotated window
/// arrived.
/// </summary>
public class SwapchainShapeTests
{
    private static readonly (uint, uint) AnySize = (1u, 1u);
    private static readonly (uint, uint) Large = (4096u, 4096u);

    /// <summary>The renderer draws upright, so the swapchain says so, and the compositor turns the
    /// image; only a surface that cannot take IDENTITY gets what it asks for.</summary>
    [Theory]
    [InlineData(0x1FFu, 0x2u, 0x1u)] // a phone held sideways: ROTATE_90 current, IDENTITY declared
    [InlineData(0x1FFu, 0x1u, 0x1u)]
    [InlineData(0x2u, 0x2u, 0x2u)]   // a surface that offers only its rotation
    public void TheSwapchainDeclaresTheImageUpright(uint supported, uint current, uint declared) =>
        SwapchainShape.PreTransform(supported, current).Should().Be(declared);

    [Fact]
    public void ARotatedPhoneGetsTheRotatedWindowsSize()
    {
        SwapchainShape.Extent(2400, 1080, AnySize, Large).Should().Be((2400u, 1080u),
            "the window is landscape now, whatever the surface last described");
        SwapchainShape.Extent(1080, 2400, AnySize, Large).Should().Be((1080u, 2400u));
    }

    /// <summary>A desktop surface fixes its extent: smallest and largest are both the current one,
    /// so that is what the swapchain is made in, whatever the window reported.</summary>
    [Fact]
    public void ASurfaceThatFixesItsExtentGetsIt() =>
        SwapchainShape.Extent(1283, 801, (1280u, 800u), (1280u, 800u)).Should().Be((1280u, 800u));

    [Fact]
    public void ASizeOutsideTheRangeIsBroughtIntoIt()
    {
        SwapchainShape.Extent(0, 0, AnySize, Large).Should().Be((1u, 1u));
        SwapchainShape.Extent(9000, 300, AnySize, Large).Should().Be((4096u, 300u));
    }
}
