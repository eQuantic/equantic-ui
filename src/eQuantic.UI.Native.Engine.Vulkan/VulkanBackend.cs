namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The Photon Vulkan backend: the coarse <see cref="IRenderBackend"/> adapter over
/// <see cref="VulkanDevice"/> (the Vulkan RHI implementation) and the SHARED
/// <see cref="RhiRenderer"/> encode loop — the same loop the Metal backend runs, so the two GPU
/// backends can only differ in API calls, never in frame semantics. It draws offscreen for
/// readback and INTO A WINDOW through a swapchain; on macOS dev hosts it runs through MoltenVK as a
/// DEV/TEST ICD only — plan D1 stands: the product ships native Vulkan on Android and native
/// Metal on Apple platforms, never a translation layer.
/// </summary>
public sealed unsafe class VulkanBackend : IRenderBackend
{
    private static readonly Lazy<bool> Supported = new(VulkanDevice.Probe);

    /// <summary>True when a Vulkan loader/ICD with a usable device exists (CI without one skips).</summary>
    public static bool IsSupported => Supported.Value;

    private readonly VulkanDevice _device;
    private readonly RhiRenderer _renderer;

    public VulkanBackend()
    {
        _device = new VulkanDevice();
        _renderer = new RhiRenderer(_device);
    }

    public RenderBackendKind Kind => RenderBackendKind.Vulkan;

    public IRenderSurface CreateSurface(int width, int height) =>
        new RhiSurface(_device.CreateRenderTarget(width, height));

    public void Render(DisplayList displayList, IRenderSurface surface) =>
        _renderer.Render(displayList, ((RhiSurface)surface).Target);

    /// <summary>
    /// A swapchain for a platform window — on Android, the <c>ANativeWindow</c> behind a
    /// SurfaceView. This is the whole difference between drawing offscreen and drawing on a screen:
    /// everything above the swapchain is the code that already existed.
    /// </summary>
    public VulkanSwapchain CreateSwapchain(IntPtr nativeWindow, int width, int height)
    {
        var createInfo = new VkAndroidSurfaceCreateInfoKHR
        {
            SType = VkStructureType.AndroidSurfaceCreateInfoKHR,
            Window = nativeWindow,
        };
        ulong surface;
        Vk.Check(VkSurface.vkCreateAndroidSurfaceKHR(
            _device.Instance, &createInfo, IntPtr.Zero, &surface), "android surface creation");

        // A queue that cannot present to this surface would draw frames nobody ever sees.
        uint supported;
        Vk.Check(VkSurface.vkGetPhysicalDeviceSurfaceSupportKHR(
            _device.PhysicalDevice, _device.QueueFamily, surface, &supported), "surface support");
        if (supported == 0)
            throw new PlatformNotSupportedException(
                "This device's graphics queue cannot present to the window's surface.");

        return new VulkanSwapchain(_device, surface, width, height);
    }

    /// <summary>Draws one frame straight into the window, through the same renderer as everything else.</summary>
    public bool Present(DisplayList displayList, VulkanSwapchain swapchain) =>
        swapchain.Present(displayList, _renderer);

    public void Dispose()
    {
        _renderer.Dispose();
        _device.Dispose();
    }
}
