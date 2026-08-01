namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// One Vulkan render pass as an <see cref="IRhiCommandList"/>. Texture binds are LATCHED (not
/// recorded): the pair resolves to an immutable cached descriptor set at <see cref="Draw"/>, bound
/// together with the draw's dynamic uniform offset — one <c>vkCmdBindDescriptorSets</c> + one
/// <c>vkCmdDraw</c> per display-list command, mirroring Metal's setFragmentBytes + draw shape.
/// </summary>
internal sealed unsafe class VulkanCommandList : IRhiCommandList
{
    private readonly VulkanDevice _device;
    private readonly IntPtr _commandBuffer;
    private readonly VulkanRenderTarget _target;
    private ulong _coverageView;
    private ulong _colorView;
    private RhiFilter _filter = RhiFilter.Nearest;

    internal VulkanCommandList(VulkanDevice device, IntPtr commandBuffer, VulkanRenderTarget target)
    {
        _device = device;
        _commandBuffer = commandBuffer;
        _target = target;
    }

    public void SetPipeline(RhiPipelineKind kind) =>
        Vk.vkCmdBindPipeline(_commandBuffer, 0 /* GRAPHICS */,
            _device.Pipeline(VulkanDevice.FormatR8G8B8A8Srgb, kind));

    public void BindTexture(int slot, IRhiTexture texture, RhiFilter filter = RhiFilter.Nearest)
    {
        var view = ((IVulkanBindable)texture).View;
        if (slot == RhiRenderer.CoverageSlot) _coverageView = view;
        else _colorView = view;
        _filter = filter;
    }

    public void Draw(in DrawUniforms uniforms)
    {
        var dynamicOffset = _device.WriteUniforms(in uniforms);
        var set = _device.DescriptorSetFor(_coverageView, _colorView, _device.Sampler(_filter));
        Vk.vkCmdBindDescriptorSets(_commandBuffer, 0 /* GRAPHICS */, _device.PipelineLayoutHandle,
            0, 1, &set, 1, &dynamicOffset);
        Vk.vkCmdDraw(_commandBuffer, 3, 1, 0, 0);
    }

    /// <summary>v1 always waits — the backend is offscreen-only until the Android swapchain path
    /// (plan W5) brings real fences; the parameter is honored by construction.</summary>
    public void Submit(bool waitUntilCompleted) => _device.Submit(_commandBuffer, _target);
}
