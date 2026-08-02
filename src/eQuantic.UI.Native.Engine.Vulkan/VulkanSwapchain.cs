using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The images a device shows on screen, and the handshake around them. A frame acquires one image
/// from the presentation engine, draws into it, and hands it back — with SEMAPHORES rather than
/// waits, because the two sides run on different clocks and neither can be told to stand still.
/// <para>
/// The render targets here wrap images the swapchain owns, in a pass whose final layout is
/// PRESENT_SRC. Everything above — the display list, the renderer, the layout — is the same code
/// that draws offscreen, and never learns which it was.
/// </para>
/// </summary>
public sealed unsafe class VulkanSwapchain : IDisposable
{
    /// <summary>sRGB, so the shader's output lands exactly where the offscreen target's does.</summary>
    private const uint FormatR8G8B8A8Srgb = 43;
    private const uint FormatB8G8R8A8Srgb = 50;
    private const uint ColorSpaceSrgbNonlinear = 0;

    /// <summary>FIFO — vsync, and the only mode a Vulkan implementation must support.</summary>
    private const uint PresentModeFifo = 2;

    private readonly VulkanDevice _device;
    private readonly ulong _surface;
    private readonly ulong _acquired;
    private readonly ulong _rendered;
    private ulong _swapchain;
    private VulkanRenderTarget[] _targets = [];
    private bool _disposed;

    internal VulkanSwapchain(VulkanDevice device, ulong surface, int width, int height)
    {
        _device = device;
        _surface = surface;
        _acquired = device.CreateSemaphore();
        _rendered = device.CreateSemaphore();
        Create(width, height);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// Draws one frame and shows it. Returns false when the presentation engine says the swapchain
    /// no longer matches the surface — a rotation, a resize — which is a fact about the WINDOW, not
    /// an error: the caller rebuilds and carries on.
    /// </summary>
    public bool Present(DisplayList displayList, RhiRenderer renderer)
    {
        uint index;
        var acquire = VkSurface.vkAcquireNextImageKHR(_device.Device, _swapchain, ulong.MaxValue,
            _acquired, 0, &index);
        if (acquire is -1000001004 or -1000001002) return false; // OUT_OF_DATE / SUBOPTIMAL
        Vk.Check(acquire, "swapchain acquire");

        var target = _targets[index];
        var commands = (VulkanCommandList)_device.Begin(target, RhiRenderer.ClearColorOf(displayList));
        renderer.Encode(displayList, commands);
        commands.SubmitPresenting(_acquired, _rendered);

        var swapchain = _swapchain;
        var rendered = _rendered;
        var presentInfo = new VkPresentInfoKHR
        {
            SType = VkStructureType.PresentInfoKHR,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendered,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &index,
        };
        var present = VkSurface.vkQueuePresentKHR(_device.Queue, &presentInfo);
        if (present is -1000001004 or -1000001002) return false;
        Vk.Check(present, "queue present");
        return true;
    }

    /// <summary>Rebuilds for the surface's current size — a rotation, a resize, a keyboard.</summary>
    public void Resize(int width, int height)
    {
        DestroyTargets();
        Create(width, height);
    }

    private void Create(int width, int height)
    {
        VkSurfaceCapabilitiesKHR capabilities;
        Vk.Check(VkSurface.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            _device.PhysicalDevice, _surface, &capabilities), "surface capabilities");

        // The surface's own extent wins when it states one: on a phone it always does, and a size
        // we picked ourselves would simply be rejected.
        var extent = capabilities.CurrentExtent.Width != uint.MaxValue
            ? capabilities.CurrentExtent
            : new VkExtent2D { Width = (uint)width, Height = (uint)height };
        Width = (int)extent.Width;
        Height = (int)extent.Height;

        var format = PickFormat();
        // One more than the minimum: with exactly the minimum, acquiring blocks until the display
        // engine has finished with an image, which serialises the frame against the panel.
        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        var createInfo = new VkSwapchainCreateInfoKHR
        {
            SType = VkStructureType.SwapchainCreateInfoKHR,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = format.Format,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = 0x10,          // COLOR_ATTACHMENT
            ImageSharingMode = 0,       // EXCLUSIVE — one queue family draws and presents
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = 0x1,       // OPAQUE — the app owns every pixel of its window
            PresentMode = PresentModeFifo,
            Clipped = 1,
            OldSwapchain = _swapchain,
        };
        ulong swapchain;
        Vk.Check(VkSurface.vkCreateSwapchainKHR(_device.Device, &createInfo, IntPtr.Zero, &swapchain),
            "swapchain creation");

        if (_swapchain != 0) VkSurface.vkDestroySwapchainKHR(_device.Device, _swapchain, IntPtr.Zero);
        _swapchain = swapchain;

        uint count = 0;
        Vk.Check(VkSurface.vkGetSwapchainImagesKHR(_device.Device, _swapchain, &count, null),
            "swapchain images");
        var images = new ulong[count];
        fixed (ulong* imagePtr = images)
            Vk.Check(VkSurface.vkGetSwapchainImagesKHR(_device.Device, _swapchain, &count, imagePtr),
                "swapchain images");

        _targets = images
            .Select(image => _device.AdoptImage(image, format.Format, Width, Height))
            .ToArray();
    }

    /// <summary>
    /// sRGB, in whichever channel order the surface offers. Anything else would make the engine's
    /// output mean something different on screen than it does in a golden.
    /// </summary>
    private VkSurfaceFormatKHR PickFormat()
    {
        uint count = 0;
        Vk.Check(VkSurface.vkGetPhysicalDeviceSurfaceFormatsKHR(
            _device.PhysicalDevice, _surface, &count, null), "surface formats");
        var formats = new VkSurfaceFormatKHR[count];
        fixed (VkSurfaceFormatKHR* formatPtr = formats)
            Vk.Check(VkSurface.vkGetPhysicalDeviceSurfaceFormatsKHR(
                _device.PhysicalDevice, _surface, &count, formatPtr), "surface formats");

        foreach (var candidate in formats)
            if (candidate.ColorSpace == ColorSpaceSrgbNonlinear
                && candidate.Format is FormatR8G8B8A8Srgb or FormatB8G8R8A8Srgb)
                return candidate;

        throw new PlatformNotSupportedException(
            "This surface offers no sRGB format. The engine's colours are defined in sRGB, and a "
            + "linear surface would show every one of them wrong.");
    }

    private void DestroyTargets()
    {
        foreach (var target in _targets) target.Dispose();
        _targets = [];
    }

    public void Dispose()
    {
        if (_disposed || _device.IsDisposed) return;
        _disposed = true;
        DestroyTargets();
        if (_swapchain != 0) VkSurface.vkDestroySwapchainKHR(_device.Device, _swapchain, IntPtr.Zero);
        Vk.vkDestroySemaphore(_device.Device, _acquired, IntPtr.Zero);
        Vk.vkDestroySemaphore(_device.Device, _rendered, IntPtr.Zero);
        VkSurface.vkDestroySurfaceKHR(_device.Instance, _surface, IntPtr.Zero);
    }
}
