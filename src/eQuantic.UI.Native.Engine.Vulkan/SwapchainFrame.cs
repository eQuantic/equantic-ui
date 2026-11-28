using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// A window's frame on Vulkan: the last pass draws into the image the swapchain handed over, waiting
/// on the semaphore that says the display engine is done with it and signalling the one the present
/// waits on (#1952).
/// <para>
/// The passes before it (the layers, a backdrop's content and its blur) are drawn into the
/// renderer's own targets and each is SUBMITTED AND WAITED FOR before the next is recorded: the
/// device's submit waits for its queue to go idle. So when this pass reads a layer, the layer is
/// finished, by the CPU's wait rather than by a semaphore between submissions, and the uniform ring
/// every pass writes from its start is free again. The order is measured, not assumed:
/// <c>AWindowFrameCompositesItsLayersTests</c> draws through this frame on a real device and reads
/// a group that composited once.
/// </para>
/// </summary>
internal sealed class SwapchainFrame : IRhiFrameTarget
{
    private readonly VulkanDevice _device;
    private readonly VulkanRenderTarget _image;
    private readonly ulong _acquired;
    private readonly ulong _rendered;

    /// <param name="device">The device the swapchain belongs to.</param>
    /// <param name="image">The swapchain image this frame draws into.</param>
    /// <param name="acquired">Signalled when the image is free to draw into; 0 waits on nothing.</param>
    /// <param name="rendered">Signalled when the frame is drawn, for the present; 0 signals nothing.</param>
    public SwapchainFrame(VulkanDevice device, VulkanRenderTarget image, ulong acquired, ulong rendered)
    {
        _device = device;
        _image = image;
        _acquired = acquired;
        _rendered = rendered;
    }

    public int Width => _image.Width;

    public int Height => _image.Height;

    public IRhiCommandList Begin(Color clearColor) => _device.Begin(_image, clearColor);

    public void Submit(IRhiCommandList commands) =>
        ((VulkanCommandList)commands).SubmitPresenting(_acquired, _rendered);
}
