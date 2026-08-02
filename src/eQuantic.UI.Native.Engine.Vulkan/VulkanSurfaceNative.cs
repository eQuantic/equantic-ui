using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The presentation half of Vulkan: a surface to draw at, a swapchain of images to draw into, and
/// the semaphores that keep the GPU and the display engine out of each other's way. All of it is
/// extension territory, which is why it sits beside the core bindings rather than inside them.
/// </summary>
internal static unsafe partial class VkSurface
{
    private const string Lib = "vulkan";

    /// <summary>An Android surface, from the window a SurfaceView handed us.</summary>
    [LibraryImport(Lib)]
    internal static partial int vkCreateAndroidSurfaceKHR(IntPtr instance,
        VkAndroidSurfaceCreateInfoKHR* createInfo, IntPtr allocator, ulong* surface);

    [LibraryImport(Lib)]
    internal static partial void vkDestroySurfaceKHR(IntPtr instance, ulong surface, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkGetPhysicalDeviceSurfaceSupportKHR(IntPtr physicalDevice,
        uint queueFamily, ulong surface, uint* supported);

    [LibraryImport(Lib)]
    internal static partial int vkGetPhysicalDeviceSurfaceCapabilitiesKHR(IntPtr physicalDevice,
        ulong surface, VkSurfaceCapabilitiesKHR* capabilities);

    [LibraryImport(Lib)]
    internal static partial int vkGetPhysicalDeviceSurfaceFormatsKHR(IntPtr physicalDevice,
        ulong surface, uint* count, VkSurfaceFormatKHR* formats);

    [LibraryImport(Lib)]
    internal static partial int vkGetPhysicalDeviceSurfacePresentModesKHR(IntPtr physicalDevice,
        ulong surface, uint* count, uint* modes);

    [LibraryImport(Lib)]
    internal static partial int vkCreateSwapchainKHR(IntPtr device,
        VkSwapchainCreateInfoKHR* createInfo, IntPtr allocator, ulong* swapchain);

    [LibraryImport(Lib)]
    internal static partial void vkDestroySwapchainKHR(IntPtr device, ulong swapchain, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkGetSwapchainImagesKHR(IntPtr device, ulong swapchain,
        uint* count, ulong* images);

    [LibraryImport(Lib)]
    internal static partial int vkAcquireNextImageKHR(IntPtr device, ulong swapchain, ulong timeout,
        ulong semaphore, ulong fence, uint* imageIndex);

    [LibraryImport(Lib)]
    internal static partial int vkQueuePresentKHR(IntPtr queue, VkPresentInfoKHR* presentInfo);

    /// <summary>The window behind a Java <c>Surface</c>. libandroid is the platform's own.</summary>
    [LibraryImport("android", EntryPoint = "ANativeWindow_fromSurface")]
    internal static partial IntPtr NativeWindowFromSurface(IntPtr env, IntPtr surface);

    [LibraryImport("android", EntryPoint = "ANativeWindow_release")]
    internal static partial void NativeWindowRelease(IntPtr window);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkAndroidSurfaceCreateInfoKHR
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public IntPtr Window;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkExtent2D
{
    public uint Width;
    public uint Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSurfaceCapabilitiesKHR
{
    public uint MinImageCount;
    public uint MaxImageCount;
    public VkExtent2D CurrentExtent;
    public VkExtent2D MinImageExtent;
    public VkExtent2D MaxImageExtent;
    public uint MaxImageArrayLayers;
    public uint SupportedTransforms;
    public uint CurrentTransform;
    public uint SupportedCompositeAlpha;
    public uint SupportedUsageFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSurfaceFormatKHR
{
    public uint Format;
    public uint ColorSpace;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkSwapchainCreateInfoKHR
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public ulong Surface;
    public uint MinImageCount;
    public uint ImageFormat;
    public uint ImageColorSpace;
    public VkExtent2D ImageExtent;
    public uint ImageArrayLayers;
    public uint ImageUsage;
    public uint ImageSharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
    public uint PreTransform;
    public uint CompositeAlpha;
    public uint PresentMode;
    public uint Clipped;
    public ulong OldSwapchain;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPresentInfoKHR
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint WaitSemaphoreCount;
    public ulong* PWaitSemaphores;
    public uint SwapchainCount;
    public ulong* PSwapchains;
    public uint* PImageIndices;
    public int* PResults;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSemaphoreCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
}
