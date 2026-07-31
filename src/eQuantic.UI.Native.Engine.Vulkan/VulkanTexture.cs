namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>An uploaded sampled texture (A8 → <c>R8_UNORM</c>, Rgba8 → <c>R8G8B8A8_SRGB</c>) in
/// <c>SHADER_READ_ONLY_OPTIMAL</c> layout, fetched via <c>Load</c> — no sampler exists anywhere
/// in the engine (nearest by definition, plan D6/W4 texel-exact rasters).</summary>
public sealed class VulkanTexture : IRhiTexture
{
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
public sealed unsafe class VulkanRenderTarget : IRhiRenderTarget
{
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
            var region = new VkBufferImageCopy
            {
                ImageSubresource = new VkImageSubresourceLayers { AspectMask = 0x1, LayerCount = 1 },
                ImageExtent = new VkExtent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 },
            };
            Vk.vkCmdCopyImageToBuffer(command, Image, 6 /* TRANSFER_SRC_OPTIMAL — the pass's final layout */, buffer, 1, &region);
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
        Vk.vkDestroyFramebuffer(_device.Device, Framebuffer, IntPtr.Zero);
        Vk.vkDestroyImageView(_device.Device, _view, IntPtr.Zero);
        Vk.vkDestroyImage(_device.Device, Image, IntPtr.Zero);
        Vk.vkFreeMemory(_device.Device, _memory, IntPtr.Zero);
    }
}
