using eQuantic.UI.Native.Engine.Vulkan;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The two answers that mean "this descriptor pool is full", by the numbers Vulkan defines. A wrong
/// constant here is the shape of #794, where a status was tested for under a value Vulkan never
/// answers, and the code that handled it never ran.
/// </summary>
public class DescriptorPoolStatusTests
{
    [Theory]
    [InlineData(-1000069000)]   // VK_ERROR_OUT_OF_POOL_MEMORY
    [InlineData(-12)]           // VK_ERROR_FRAGMENTED_POOL, what the Android emulator answered
    public void AFullPool_IsNotAnError(int result) =>
        DescriptorPoolStatus.IsFull(result).Should().BeTrue("the next set comes from another pool");

    [Theory]
    [InlineData(0)]             // VK_SUCCESS
    [InlineData(-1)]            // VK_ERROR_OUT_OF_HOST_MEMORY
    [InlineData(-2)]            // VK_ERROR_OUT_OF_DEVICE_MEMORY
    [InlineData(-4)]            // VK_ERROR_DEVICE_LOST
    public void AnythingElse_IsNotAFullPool(int result) =>
        DescriptorPoolStatus.IsFull(result).Should().BeFalse();
}
