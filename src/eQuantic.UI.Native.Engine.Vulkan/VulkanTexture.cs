namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>Anything the encoder can bind as a sampled image — render targets included, since a
/// composited layer is exactly a render target being read.</summary>
internal interface IVulkanBindable
{
    ulong View { get; }
}

/// <summary>An uploaded sampled texture (A8 → <c>R8_UNORM</c>, Rgba8 → <c>R8G8B8A8_SRGB</c>) in
/// <c>SHADER_READ_ONLY_OPTIMAL</c> layout, fetched via <c>Load</c> — no sampler exists anywhere
/// in the engine (nearest by definition, plan D6/W4 texel-exact rasters).</summary>
public sealed class VulkanTexture : IRhiTexture, IVulkanBindable
{
    ulong IVulkanBindable.View => View;

    private readonly VulkanDevice _device;
    private readonly ulong _image;
    private readonly ulong _memory;
    private bool _disposed;

    internal VulkanTexture(VulkanDevice device, ulong image, ulong memory, ulong view, int width, int height)
    {
        _device = device;
        _image = image;
        _memory = memory;
        View = view;
        Width = width;
        Height = height;
    }

    internal ulong View { get; }

    public int Width { get; }
    public int Height { get; }

    public void Dispose()
    {
        if (_disposed || _device.IsDisposed) return;
        _disposed = true;
        _device.OnViewDestroyed(View); // BEFORE the destroy: stale cache entries outlive reused handles
        Vk.vkDestroyImageView(_device.Device, View, IntPtr.Zero);
        Vk.vkDestroyImage(_device.Device, _image, IntPtr.Zero);
        Vk.vkFreeMemory(_device.Device, _memory, IntPtr.Zero);
    }
}

/// <summary>
/// An offscreen Vulkan render target (<c>R8G8B8A8_SRGB</c>, device-local). The render pass leaves
/// it in <c>TRANSFER_SRC_OPTIMAL</c>, so readback is one copy-to-buffer away; the stored pixels
/// are PREMULTIPLIED sRGB-encoded (blending output) and funnel through
/// <see cref="RhiReadback.UnpremultiplySrgb"/> — the same conversion as the Metal target, so the
/// two GPU readbacks can only agree.
/// </summary>
public sealed unsafe class VulkanRenderTarget : IRhiRenderTarget, IVulkanBindable
{
    ulong IVulkanBindable.View => _view;

    private readonly VulkanDevice _device;
    private readonly ulong _memory;
    private readonly ulong _view;
    private bool _rendered;
    private bool _disposed;

    internal VulkanRenderTarget(VulkanDevice device, ulong image, ulong memory, ulong view, ulong framebuffer, int width, int height)
    {
        _device = device;
        Image = image;
        _memory = memory;
        _view = view;
        Framebuffer = framebuffer;
        Width = width;
        Height = height;
    }

    internal ulong Image { get; }
    internal ulong Framebuffer { get; }

    public int Width { get; }
    public int Height { get; }

    internal void MarkRendered() => _rendered = true;

    public void ReadPixelsSrgb(Span<byte> destinationRgba)
    {
        var required = Width * Height * 4;
        if (destinationRgba.Length < required)
            throw new ArgumentException($"Destination needs {required} bytes, got {destinationRgba.Length}.", nameof(destinationRgba));

        if (!_rendered)
        {
            // Never rendered — the image layout is still UNDEFINED; the surface is defined-empty.
            destinationRgba[..required].Clear();
            return;
        }

        var (buffer, memory) = _device.CreateBuffer((ulong)required, usage: 0x2 /* TRANSFER_DST */, hostVisible: true);
        try
        {
            var command = _device.BeginOneShot();
            // The render pass leaves the target in SHADER_READ_ONLY (its common fate is being
            // composited as a layer), so readback transitions it explicitly rather than making
            // every frame pay for a layout the rare readback prefers.
            var toTransfer = new VkImageMemoryBarrier
            {
                SType = VkStructureType.ImageMemoryBarrier,
                SrcAccessMask = 0x20,   // SHADER_READ
                DstAccessMask = 0x800,  // TRANSFER_READ
                OldLayout = 5,          // SHADER_READ_ONLY_OPTIMAL
                NewLayout = 6,          // TRANSFER_SRC_OPTIMAL
                SrcQueueFamilyIndex = ~0u,
                DstQueueFamilyIndex = ~0u,
                Image = Image,
                SubresourceRange = new VkImageSubresourceRange { AspectMask = 0x1, LevelCount = 1, LayerCount = 1 },
            };
            Vk.vkCmdPipelineBarrier(command, 0x80 /* FRAGMENT_SHADER */, 0x1000 /* TRANSFER */, 0,
                0, IntPtr.Zero, 0, IntPtr.Zero, 1, &toTransfer);

            var region = new VkBufferImageCopy
            {
                ImageSubresource = new VkImageSubresourceLayers { AspectMask = 0x1, LayerCount = 1 },
                ImageExtent = new VkExtent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 },
            };
            Vk.vkCmdCopyImageToBuffer(command, Image, 6 /* TRANSFER_SRC_OPTIMAL */, buffer, 1, &region);
            _device.EndOneShot(command);

            void* mapped;
            Vk.Check(Vk.vkMapMemory(_device.Device, memory, 0, (ulong)required, 0, &mapped), "readback map");
            RhiReadback.UnpremultiplySrgb(new ReadOnlySpan<byte>(mapped, required), destinationRgba);
            Vk.vkUnmapMemory(_device.Device, memory);
        }
        finally
        {
            Vk.vkDestroyBuffer(_device.Device, buffer, IntPtr.Zero);
            Vk.vkFreeMemory(_device.Device, memory, IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_disposed || _device.IsDisposed) return;
        _disposed = true;
        _device.OnViewDestroyed(_view); // BEFORE the destroy: stale cache entries outlive reused handles
        Vk.vkDestroyFramebuffer(_device.Device, Framebuffer, IntPtr.Zero);
        Vk.vkDestroyImageView(_device.Device, _view, IntPtr.Zero);
        Vk.vkDestroyImage(_device.Device, Image, IntPtr.Zero);
        Vk.vkFreeMemory(_device.Device, _memory, IntPtr.Zero);
    }
}
