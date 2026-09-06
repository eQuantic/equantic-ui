using System.Reflection;
using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// Minimal Vulkan interop — the OWNED slim bindings of plan open-question 2, inherited from the
/// Metal spike's answer (typed <c>LibraryImport</c> externs, no binding framework, no shims). The
/// C ABI makes this even simpler than Objective-C: core 1.x entry points are exported by the
/// loader (and by a bare ICD like MoltenVK), so direct P/Invoke covers everything the engine's
/// deliberately tiny API surface (plan D4) touches. Dispatchable handles are <see cref="IntPtr"/>;
/// non-dispatchable handles are <see cref="ulong"/> on every platform.
/// </summary>
internal static unsafe partial class Vk
{
    private const string Lib = "vulkan";

    static Vk()
    {
        NativeLibrary.SetDllImportResolver(typeof(Vk).Assembly, static (name, assembly, searchPath) =>
        {
            if (name != Lib) return IntPtr.Zero;
            // Loader first (layers, multi-ICD); a bare MoltenVK dylib works as the dev fallback on
            // macOS hosts (DEV/TEST ONLY — plan D1: the product ships native Vulkan on Android and
            // native Metal on Apple platforms; no translation layer ever ships).
            string[] candidates = OperatingSystem.IsMacOS()
                ? ["libvulkan.dylib", "libvulkan.1.dylib", "/opt/homebrew/lib/libvulkan.dylib",
                   "/usr/local/lib/libvulkan.dylib", "libMoltenVK.dylib", "/opt/homebrew/lib/libMoltenVK.dylib"]
                : OperatingSystem.IsWindows()
                    ? ["vulkan-1.dll"]
                    : ["libvulkan.so.1", "libvulkan.so"];
            foreach (var candidate in candidates)
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                    return handle;
            return IntPtr.Zero;
        });
    }

    internal const uint ApiVersion12 = (1u << 22) | (2u << 12); // VK_API_VERSION_1_2 (SPIR-V 1.5)

    internal const int Success = 0;

    internal static void Check(int result, string operation)
    {
        if (result != Success)
            throw new InvalidOperationException($"Vulkan {operation} failed: VkResult {result}.");
    }

    // ── Instance / physical device ──────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateInstance(VkInstanceCreateInfo* createInfo, IntPtr allocator, IntPtr* instance);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyInstance(IntPtr instance, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkEnumerateInstanceExtensionProperties(byte* layerName, uint* count, VkExtensionProperties* properties);

    [LibraryImport(Lib)]
    internal static partial int vkEnumerateInstanceLayerProperties(uint* count, VkLayerProperties* properties);

    [LibraryImport(Lib)]
    internal static partial int vkEnumeratePhysicalDevices(IntPtr instance, uint* count, IntPtr* devices);

    [LibraryImport(Lib)]
    internal static partial void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physicalDevice, uint* count, VkQueueFamilyProperties* properties);

    [LibraryImport(Lib)]
    internal static partial void vkGetPhysicalDeviceMemoryProperties(IntPtr physicalDevice, VkPhysicalDeviceMemoryProperties* properties);

    [LibraryImport(Lib)]
    internal static partial void vkGetPhysicalDeviceFeatures2(IntPtr physicalDevice, VkPhysicalDeviceFeatures2* features);

    [LibraryImport(Lib)]
    internal static partial int vkEnumerateDeviceExtensionProperties(IntPtr physicalDevice, byte* layerName, uint* count, VkExtensionProperties* properties);

    // ── Device / queue ──────────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateDevice(IntPtr physicalDevice, VkDeviceCreateInfo* createInfo, IntPtr allocator, IntPtr* device);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyDevice(IntPtr device, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial void vkGetDeviceQueue(IntPtr device, uint queueFamilyIndex, uint queueIndex, IntPtr* queue);

    [LibraryImport(Lib)]
    internal static partial int vkDeviceWaitIdle(IntPtr device);

    [LibraryImport(Lib)]
    internal static partial int vkQueueSubmit(IntPtr queue, uint submitCount, VkSubmitInfo* submits, ulong fence);

    [LibraryImport(Lib)]
    internal static partial int vkQueueWaitIdle(IntPtr queue);

    // ── Command pool / buffers ──────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateCommandPool(IntPtr device, VkCommandPoolCreateInfo* createInfo, IntPtr allocator, ulong* commandPool);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyCommandPool(IntPtr device, ulong commandPool, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkAllocateCommandBuffers(IntPtr device, VkCommandBufferAllocateInfo* allocateInfo, IntPtr* commandBuffers);

    [LibraryImport(Lib)]
    internal static partial void vkFreeCommandBuffers(IntPtr device, ulong commandPool, uint count, IntPtr* commandBuffers);

    [LibraryImport(Lib)]
    internal static partial int vkBeginCommandBuffer(IntPtr commandBuffer, VkCommandBufferBeginInfo* beginInfo);

    [LibraryImport(Lib)]
    internal static partial int vkEndCommandBuffer(IntPtr commandBuffer);

    // ── Memory / buffers ────────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateBuffer(IntPtr device, VkBufferCreateInfo* createInfo, IntPtr allocator, ulong* buffer);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyBuffer(IntPtr device, ulong buffer, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial void vkGetBufferMemoryRequirements(IntPtr device, ulong buffer, VkMemoryRequirements* requirements);

    [LibraryImport(Lib)]
    internal static partial int vkAllocateMemory(IntPtr device, VkMemoryAllocateInfo* allocateInfo, IntPtr allocator, ulong* memory);

    [LibraryImport(Lib)]
    internal static partial void vkFreeMemory(IntPtr device, ulong memory, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkBindBufferMemory(IntPtr device, ulong buffer, ulong memory, ulong offset);

    [LibraryImport(Lib)]
    internal static partial int vkMapMemory(IntPtr device, ulong memory, ulong offset, ulong size, uint flags, void** data);

    [LibraryImport(Lib)]
    internal static partial void vkUnmapMemory(IntPtr device, ulong memory);

    // ── Images / views ──────────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateImage(IntPtr device, VkImageCreateInfo* createInfo, IntPtr allocator, ulong* image);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyImage(IntPtr device, ulong image, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial void vkGetImageMemoryRequirements(IntPtr device, ulong image, VkMemoryRequirements* requirements);

    [LibraryImport(Lib)]
    internal static partial int vkBindImageMemory(IntPtr device, ulong image, ulong memory, ulong offset);

    [LibraryImport(Lib)]
    internal static partial int vkCreateImageView(IntPtr device, VkImageViewCreateInfo* createInfo, IntPtr allocator, ulong* view);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyImageView(IntPtr device, ulong view, IntPtr allocator);

    // ── Render pass / framebuffer ───────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateRenderPass(IntPtr device, VkRenderPassCreateInfo* createInfo, IntPtr allocator, ulong* renderPass);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyRenderPass(IntPtr device, ulong renderPass, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreateSemaphore(IntPtr device, VkSemaphoreCreateInfo* createInfo, IntPtr allocator, ulong* semaphore);

    [LibraryImport(Lib)]
    internal static partial void vkDestroySemaphore(IntPtr device, ulong semaphore, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreateFramebuffer(IntPtr device, VkFramebufferCreateInfo* createInfo, IntPtr allocator, ulong* framebuffer);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyFramebuffer(IntPtr device, ulong framebuffer, IntPtr allocator);

    // ── Shaders / pipelines ─────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateShaderModule(IntPtr device, VkShaderModuleCreateInfo* createInfo, IntPtr allocator, ulong* module);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyShaderModule(IntPtr device, ulong module, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreatePipelineLayout(IntPtr device, VkPipelineLayoutCreateInfo* createInfo, IntPtr allocator, ulong* layout);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyPipelineLayout(IntPtr device, ulong layout, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreateGraphicsPipelines(IntPtr device, ulong pipelineCache, uint createInfoCount, VkGraphicsPipelineCreateInfo* createInfos, IntPtr allocator, ulong* pipelines);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyPipeline(IntPtr device, ulong pipeline, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreateSampler(IntPtr device, VkSamplerCreateInfo* createInfo, IntPtr allocator, ulong* sampler);

    [LibraryImport(Lib)]
    internal static partial void vkDestroySampler(IntPtr device, ulong sampler, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreatePipelineCache(IntPtr device, VkPipelineCacheCreateInfo* createInfo, IntPtr allocator, ulong* pipelineCache);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyPipelineCache(IntPtr device, ulong pipelineCache, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkGetPipelineCacheData(IntPtr device, ulong pipelineCache, nuint* dataSize, void* data);

    // ── Descriptors ─────────────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial int vkCreateDescriptorSetLayout(IntPtr device, VkDescriptorSetLayoutCreateInfo* createInfo, IntPtr allocator, ulong* layout);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyDescriptorSetLayout(IntPtr device, ulong layout, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkCreateDescriptorPool(IntPtr device, VkDescriptorPoolCreateInfo* createInfo, IntPtr allocator, ulong* pool);

    [LibraryImport(Lib)]
    internal static partial void vkDestroyDescriptorPool(IntPtr device, ulong pool, IntPtr allocator);

    [LibraryImport(Lib)]
    internal static partial int vkAllocateDescriptorSets(IntPtr device, VkDescriptorSetAllocateInfo* allocateInfo, ulong* sets);

    [LibraryImport(Lib)]
    internal static partial void vkUpdateDescriptorSets(IntPtr device, uint writeCount, VkWriteDescriptorSet* writes, uint copyCount, IntPtr copies);

    // ── Command recording ───────────────────────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial void vkCmdBeginRenderPass(IntPtr commandBuffer, VkRenderPassBeginInfo* beginInfo, uint contents);

    [LibraryImport(Lib)]
    internal static partial void vkCmdEndRenderPass(IntPtr commandBuffer);

    [LibraryImport(Lib)]
    internal static partial void vkCmdBindPipeline(IntPtr commandBuffer, uint bindPoint, ulong pipeline);

    [LibraryImport(Lib)]
    internal static partial void vkCmdBindDescriptorSets(IntPtr commandBuffer, uint bindPoint, ulong layout, uint firstSet, uint setCount, ulong* sets, uint dynamicOffsetCount, uint* dynamicOffsets);

    [LibraryImport(Lib)]
    internal static partial void vkCmdDraw(IntPtr commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    [LibraryImport(Lib)]
    internal static partial void vkCmdSetViewport(IntPtr commandBuffer, uint first, uint count, VkViewport* viewports);

    [LibraryImport(Lib)]
    internal static partial void vkCmdSetScissor(IntPtr commandBuffer, uint first, uint count, VkRect2D* scissors);

    [LibraryImport(Lib)]
    internal static partial void vkCmdPipelineBarrier(IntPtr commandBuffer, uint srcStageMask, uint dstStageMask, uint dependencyFlags, uint memoryBarrierCount, IntPtr memoryBarriers, uint bufferBarrierCount, IntPtr bufferBarriers, uint imageBarrierCount, VkImageMemoryBarrier* imageBarriers);

    [LibraryImport(Lib)]
    internal static partial void vkCmdCopyBufferToImage(IntPtr commandBuffer, ulong buffer, ulong image, uint imageLayout, uint regionCount, VkBufferImageCopy* regions);

    [LibraryImport(Lib)]
    internal static partial void vkCmdCopyImageToBuffer(IntPtr commandBuffer, ulong image, uint imageLayout, ulong buffer, uint regionCount, VkBufferImageCopy* regions);

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Reads a Vulkan fixed 256-byte name field as a managed string.</summary>
    internal static string FixedName(byte* name) => Marshal.PtrToStringUTF8((IntPtr)name) ?? string.Empty;
}

/// <summary>Marshals a string array as a UTF-8 <c>char**</c> for create-info structs.</summary>
internal sealed unsafe class Utf8StringArray : IDisposable
{
    private readonly IntPtr[] _strings;
    private readonly IntPtr _array;

    internal Utf8StringArray(IReadOnlyList<string> values)
    {
        Count = (uint)values.Count;
        _strings = new IntPtr[values.Count];
        for (var i = 0; i < values.Count; i++)
            _strings[i] = Marshal.StringToCoTaskMemUTF8(values[i]);
        _array = Marshal.AllocCoTaskMem(IntPtr.Size * Math.Max(1, values.Count));
        for (var i = 0; i < values.Count; i++)
            ((IntPtr*)_array)[i] = _strings[i];
    }

    internal uint Count { get; }
    internal IntPtr Pointer => Count == 0 ? IntPtr.Zero : _array;

    public void Dispose()
    {
        foreach (var s in _strings) Marshal.FreeCoTaskMem(s);
        Marshal.FreeCoTaskMem(_array);
    }
}

// ── Structs (core 1.2 subset the engine touches) — layouts mirror the C ABI exactly. ────────────

internal enum VkStructureType : uint
{
    ApplicationInfo = 0,
    InstanceCreateInfo = 1,
    DeviceQueueCreateInfo = 2,
    DeviceCreateInfo = 3,
    SubmitInfo = 4,
    MemoryAllocateInfo = 5,
    BufferCreateInfo = 12,
    ImageCreateInfo = 14,
    ImageViewCreateInfo = 15,
    ShaderModuleCreateInfo = 16,
    SamplerCreateInfo = 31,
    PipelineCacheCreateInfo = 17,
    PipelineShaderStageCreateInfo = 18,
    PipelineVertexInputStateCreateInfo = 19,
    PipelineInputAssemblyStateCreateInfo = 20,
    PipelineViewportStateCreateInfo = 22,
    PipelineRasterizationStateCreateInfo = 23,
    PipelineMultisampleStateCreateInfo = 24,
    PipelineColorBlendStateCreateInfo = 26,
    PipelineDynamicStateCreateInfo = 27,
    GraphicsPipelineCreateInfo = 28,
    PipelineLayoutCreateInfo = 30,
    DescriptorSetLayoutCreateInfo = 32,
    DescriptorPoolCreateInfo = 33,
    DescriptorSetAllocateInfo = 34,
    WriteDescriptorSet = 35,
    FramebufferCreateInfo = 37,
    RenderPassCreateInfo = 38,
    CommandPoolCreateInfo = 39,
    SemaphoreCreateInfo = 9,
    // The presentation extensions carry their own numbering, far out of the core range.
    SwapchainCreateInfoKHR = 1000001000,
    PresentInfoKHR = 1000001001,
    AndroidSurfaceCreateInfoKHR = 1000008000,
    Win32SurfaceCreateInfoKHR = 1000009000,
    CommandBufferAllocateInfo = 40,
    CommandBufferBeginInfo = 42,
    RenderPassBeginInfo = 43,
    ImageMemoryBarrier = 45,
    PhysicalDeviceFeatures2 = 1000059000,
    PhysicalDeviceShaderDrawParametersFeatures = 1000063000,
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkApplicationInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public IntPtr PApplicationName;
    public uint ApplicationVersion;
    public IntPtr PEngineName;
    public uint EngineVersion;
    public uint ApiVersion;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkInstanceCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags; // VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR = 0x1
    public VkApplicationInfo* PApplicationInfo;
    public uint EnabledLayerCount;
    public IntPtr PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public IntPtr PpEnabledExtensionNames;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkExtensionProperties
{
    public fixed byte ExtensionName[256];
    public uint SpecVersion;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkLayerProperties
{
    public fixed byte LayerName[256];
    public uint SpecVersion;
    public uint ImplementationVersion;
    public fixed byte Description[256];
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkExtent3D
{
    public uint Width, Height, Depth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkQueueFamilyProperties
{
    public uint QueueFlags; // VK_QUEUE_GRAPHICS_BIT = 0x1
    public uint QueueCount;
    public uint TimestampValidBits;
    public VkExtent3D MinImageTransferGranularity;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPhysicalDeviceMemoryProperties
{
    public uint MemoryTypeCount;
    public fixed uint MemoryTypes[64];  // 32 × { propertyFlags, heapIndex }
    public uint MemoryHeapCount;
    public fixed ulong MemoryHeaps[32]; // 16 × { size, flags+pad } — unused fields, kept for layout

    public readonly uint TypePropertyFlags(int index)
    {
        fixed (uint* types = MemoryTypes) return types[index * 2];
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPhysicalDeviceFeatures2
{
    public VkStructureType SType;
    public IntPtr PNext;
    public fixed uint Features[55]; // VkPhysicalDeviceFeatures — none of them are needed by name
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPhysicalDeviceShaderDrawParametersFeatures
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint ShaderDrawParameters;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDeviceQueueCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint QueueFamilyIndex;
    public uint QueueCount;
    public float* PQueuePriorities;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDeviceCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint QueueCreateInfoCount;
    public VkDeviceQueueCreateInfo* PQueueCreateInfos;
    public uint EnabledLayerCount;
    public IntPtr PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public IntPtr PpEnabledExtensionNames;
    public IntPtr PEnabledFeatures;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkSubmitInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint WaitSemaphoreCount;
    public IntPtr PWaitSemaphores;
    public IntPtr PWaitDstStageMask;
    public uint CommandBufferCount;
    public IntPtr* PCommandBuffers;
    public uint SignalSemaphoreCount;
    public IntPtr PSignalSemaphores;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandPoolCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags; // VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT = 0x2
    public uint QueueFamilyIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandBufferAllocateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public ulong CommandPool;
    public uint Level; // PRIMARY = 0
    public uint CommandBufferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandBufferBeginInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags; // VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT = 0x1
    public IntPtr PInheritanceInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkBufferCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public ulong Size;
    public uint Usage; // TRANSFER_SRC 0x1 · TRANSFER_DST 0x2 · UNIFORM_BUFFER 0x10
    public uint SharingMode; // EXCLUSIVE = 0
    public uint QueueFamilyIndexCount;
    public IntPtr PQueueFamilyIndices;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkMemoryRequirements
{
    public ulong Size;
    public ulong Alignment;
    public uint MemoryTypeBits;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkMemoryAllocateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public ulong AllocationSize;
    public uint MemoryTypeIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint ImageType; // 2D = 1
    public uint Format;
    public VkExtent3D Extent;
    public uint MipLevels;
    public uint ArrayLayers;
    public uint Samples; // 1_BIT = 0x1
    public uint Tiling;  // OPTIMAL = 0
    public uint Usage;   // TRANSFER_SRC 0x1 · TRANSFER_DST 0x2 · SAMPLED 0x4 · COLOR_ATTACHMENT 0x10
    public uint SharingMode;
    public uint QueueFamilyIndexCount;
    public IntPtr PQueueFamilyIndices;
    public uint InitialLayout; // UNDEFINED = 0
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageSubresourceRange
{
    public uint AspectMask; // COLOR = 0x1
    public uint BaseMipLevel;
    public uint LevelCount;
    public uint BaseArrayLayer;
    public uint LayerCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageViewCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public ulong Image;
    public uint ViewType; // 2D = 1
    public uint Format;
    public uint ComponentR, ComponentG, ComponentB, ComponentA; // IDENTITY = 0
    public VkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkAttachmentDescription
{
    public uint Flags;
    public uint Format;
    public uint Samples;
    public uint LoadOp;         // LOAD 0 · CLEAR 1 · DONT_CARE 2
    public uint StoreOp;        // STORE 0 · DONT_CARE 1
    public uint StencilLoadOp;
    public uint StencilStoreOp;
    public uint InitialLayout;
    public uint FinalLayout;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkAttachmentReference
{
    public uint Attachment;
    public uint Layout;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkSubpassDescription
{
    public uint Flags;
    public uint PipelineBindPoint; // GRAPHICS = 0
    public uint InputAttachmentCount;
    public IntPtr PInputAttachments;
    public uint ColorAttachmentCount;
    public VkAttachmentReference* PColorAttachments;
    public IntPtr PResolveAttachments;
    public IntPtr PDepthStencilAttachment;
    public uint PreserveAttachmentCount;
    public IntPtr PPreserveAttachments;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSubpassDependency
{
    public uint SrcSubpass; // VK_SUBPASS_EXTERNAL = ~0u
    public uint DstSubpass;
    public uint SrcStageMask;
    public uint DstStageMask;
    public uint SrcAccessMask;
    public uint DstAccessMask;
    public uint DependencyFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkRenderPassCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint AttachmentCount;
    public VkAttachmentDescription* PAttachments;
    public uint SubpassCount;
    public VkSubpassDescription* PSubpasses;
    public uint DependencyCount;
    public VkSubpassDependency* PDependencies;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkFramebufferCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public ulong RenderPass;
    public uint AttachmentCount;
    public ulong* PAttachments;
    public uint Width;
    public uint Height;
    public uint Layers;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkShaderModuleCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public nuint CodeSize;
    public uint* PCode;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSamplerCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint MagFilter;   // NEAREST 0 · LINEAR 1
    public uint MinFilter;
    public uint MipmapMode;  // NEAREST 0
    public uint AddressModeU; // CLAMP_TO_EDGE = 2
    public uint AddressModeV;
    public uint AddressModeW;
    public float MipLodBias;
    public uint AnisotropyEnable;
    public float MaxAnisotropy;
    public uint CompareEnable;
    public uint CompareOp;
    public float MinLod;
    public float MaxLod;
    public uint BorderColor;
    public uint UnnormalizedCoordinates;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineCacheCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public nuint InitialDataSize;
    public IntPtr PInitialData;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPipelineShaderStageCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint Stage; // VERTEX 0x1 · FRAGMENT 0x10
    public ulong Module;
    public IntPtr PName;
    public IntPtr PSpecializationInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineVertexInputStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint VertexBindingDescriptionCount;
    public IntPtr PVertexBindingDescriptions;
    public uint VertexAttributeDescriptionCount;
    public IntPtr PVertexAttributeDescriptions;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineInputAssemblyStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint Topology; // TRIANGLE_LIST = 3
    public uint PrimitiveRestartEnable;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineViewportStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint ViewportCount;
    public IntPtr PViewports; // null — dynamic
    public uint ScissorCount;
    public IntPtr PScissors;  // null — dynamic
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineRasterizationStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint DepthClampEnable;
    public uint RasterizerDiscardEnable;
    public uint PolygonMode; // FILL = 0
    public uint CullMode;    // NONE = 0 — the fullscreen triangle's winding flips with the API's NDC handedness
    public uint FrontFace;
    public uint DepthBiasEnable;
    public float DepthBiasConstantFactor;
    public float DepthBiasClamp;
    public float DepthBiasSlopeFactor;
    public float LineWidth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineMultisampleStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint RasterizationSamples; // 1_BIT = 0x1
    public uint SampleShadingEnable;
    public float MinSampleShading;
    public IntPtr PSampleMask;
    public uint AlphaToCoverageEnable;
    public uint AlphaToOneEnable;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineColorBlendAttachmentState
{
    public uint BlendEnable;
    public uint SrcColorBlendFactor; // ONE = 1
    public uint DstColorBlendFactor; // ONE_MINUS_SRC_ALPHA = 7
    public uint ColorBlendOp;        // ADD = 0
    public uint SrcAlphaBlendFactor;
    public uint DstAlphaBlendFactor;
    public uint AlphaBlendOp;
    public uint ColorWriteMask; // RGBA = 0xF
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPipelineColorBlendStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint LogicOpEnable;
    public uint LogicOp;
    public uint AttachmentCount;
    public VkPipelineColorBlendAttachmentState* PAttachments;
    public float BlendConstant0, BlendConstant1, BlendConstant2, BlendConstant3;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPipelineDynamicStateCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint DynamicStateCount;
    public uint* PDynamicStates; // VIEWPORT = 0, SCISSOR = 1
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkGraphicsPipelineCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint StageCount;
    public VkPipelineShaderStageCreateInfo* PStages;
    public VkPipelineVertexInputStateCreateInfo* PVertexInputState;
    public VkPipelineInputAssemblyStateCreateInfo* PInputAssemblyState;
    public IntPtr PTessellationState;
    public VkPipelineViewportStateCreateInfo* PViewportState;
    public VkPipelineRasterizationStateCreateInfo* PRasterizationState;
    public VkPipelineMultisampleStateCreateInfo* PMultisampleState;
    public IntPtr PDepthStencilState;
    public VkPipelineColorBlendStateCreateInfo* PColorBlendState;
    public VkPipelineDynamicStateCreateInfo* PDynamicState;
    public ulong Layout;
    public ulong RenderPass;
    public uint Subpass;
    public ulong BasePipelineHandle;
    public int BasePipelineIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkPipelineLayoutCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint SetLayoutCount;
    public IntPtr PSetLayouts;
    public uint PushConstantRangeCount;
    public IntPtr PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkDescriptorSetLayoutBinding
{
    public uint Binding;
    public uint DescriptorType; // SAMPLED_IMAGE 2 · UNIFORM_BUFFER_DYNAMIC 8
    public uint DescriptorCount;
    public uint StageFlags;
    public IntPtr PImmutableSamplers;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDescriptorSetLayoutCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint BindingCount;
    public VkDescriptorSetLayoutBinding* PBindings;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkDescriptorPoolSize
{
    public uint Type;
    public uint DescriptorCount;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDescriptorPoolCreateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint Flags;
    public uint MaxSets;
    public uint PoolSizeCount;
    public VkDescriptorPoolSize* PPoolSizes;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDescriptorSetAllocateInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public ulong DescriptorPool;
    public uint DescriptorSetCount;
    public ulong* PSetLayouts;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkDescriptorImageInfo
{
    public ulong Sampler; // never used — Load-only shaders (nearest by definition)
    public ulong ImageView;
    public uint ImageLayout; // SHADER_READ_ONLY_OPTIMAL = 5
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkDescriptorBufferInfo
{
    public ulong Buffer;
    public ulong Offset;
    public ulong Range;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkWriteDescriptorSet
{
    public VkStructureType SType;
    public IntPtr PNext;
    public ulong DstSet;
    public uint DstBinding;
    public uint DstArrayElement;
    public uint DescriptorCount;
    public uint DescriptorType;
    public VkDescriptorImageInfo* PImageInfo;
    public VkDescriptorBufferInfo* PBufferInfo;
    public IntPtr PTexelBufferView;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkViewport
{
    public float X, Y, Width, Height, MinDepth, MaxDepth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkRect2D
{
    public int OffsetX, OffsetY;
    public uint ExtentWidth, ExtentHeight;
}

/// <summary>VkClearValue's color-float arm (the only one the engine clears with).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VkClearValue
{
    public float R, G, B, A;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkRenderPassBeginInfo
{
    public VkStructureType SType;
    public IntPtr PNext;
    public ulong RenderPass;
    public ulong Framebuffer;
    public VkRect2D RenderArea;
    public uint ClearValueCount;
    public VkClearValue* PClearValues;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageMemoryBarrier
{
    public VkStructureType SType;
    public IntPtr PNext;
    public uint SrcAccessMask;
    public uint DstAccessMask;
    public uint OldLayout;
    public uint NewLayout;
    public uint SrcQueueFamilyIndex; // VK_QUEUE_FAMILY_IGNORED = ~0u
    public uint DstQueueFamilyIndex;
    public ulong Image;
    public VkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageSubresourceLayers
{
    public uint AspectMask;
    public uint MipLevel;
    public uint BaseArrayLayer;
    public uint LayerCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkBufferImageCopy
{
    public ulong BufferOffset;
    public uint BufferRowLength;
    public uint BufferImageHeight;
    public VkImageSubresourceLayers ImageSubresource;
    public int ImageOffsetX, ImageOffsetY, ImageOffsetZ;
    public VkExtent3D ImageExtent;
}
