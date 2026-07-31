namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The Photon Vulkan backend: the coarse <see cref="IRenderBackend"/> adapter over
/// <see cref="VulkanDevice"/> (the Vulkan RHI implementation) and the SHARED
/// <see cref="RhiRenderer"/> encode loop — the same loop the Metal backend runs, so the two GPU
/// backends can only differ in API calls, never in frame semantics. Offscreen-only until the
/// Android shell brings the swapchain (plan W5); on macOS dev hosts it runs through MoltenVK as a
/// DEV/TEST ICD only — plan D1 stands: the product ships native Vulkan on Android and native
/// Metal on Apple platforms, never a translation layer.
/// </summary>
public sealed class VulkanBackend : IRenderBackend
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

    public void Dispose()
    {
        _renderer.Dispose();
        _device.Dispose();
    }
}
