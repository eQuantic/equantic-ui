using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The Vulkan <see cref="IRhiDevice"/>: instance → physical device → logical device once, the
/// FIXED pipeline set per target format (plan D5, offline SPIR-V from the ONE Slang source — plan
/// D3), and the per-draw plumbing the shared encoder needs. Per-draw uniforms travel through a
/// persistently-mapped UNIFORM RING bound with dynamic offsets — 160 bytes exceeds the 128-byte
/// push-constant floor the spec guarantees, and the ring works on every device in the Android
/// driver zoo (stride 256 = the spec's ceiling for <c>minUniformBufferOffsetAlignment</c>, so no
/// limits query is needed). The device requires <c>shaderDrawParameters</c> (Vulkan 1.1 feature):
/// slangc lowers <c>SV_VertexID</c> through <c>gl_BaseVertex</c>, which needs the DrawParameters
/// SPIR-V capability. Offscreen-only for now — the swapchain path arrives with the Android shell
/// (plan W5); submits always wait (real fences join the frame loop then).
/// </summary>
public sealed unsafe class VulkanDevice : IRhiDevice
{
    /// <summary>SHADER_READ_ONLY_OPTIMAL — where a layer target is left, to be sampled next.</summary>
    internal const uint LayoutShaderReadOnly = 5;

    /// <summary>PRESENT_SRC_KHR — where a swapchain image is left, to be shown next.</summary>
    internal const uint LayoutPresentSrc = 1000001002;

    internal const uint FormatR8Unorm = 9;        // VK_FORMAT_R8_UNORM (A8 coverage — linear)
    internal const uint FormatR8G8B8A8Srgb = 43;  // VK_FORMAT_R8G8B8A8_SRGB (targets + images)

    /// <summary>Ring stride: the spec caps <c>minUniformBufferOffsetAlignment</c> at 256.</summary>
    private const int UniformStride = 256;

    /// <summary>4096 draws per pass — golden/parity scale with an order-of-magnitude margin;
    /// exceeding it throws (v1 offscreen fence, revisited with the Android frame loop).</summary>
    private const int UniformCapacity = UniformStride * 4096;

    private static readonly RhiPipelineKind[] RegistryKinds =
        [RhiPipelineKind.Sdf, RhiPipelineKind.TexturedA8, RhiPipelineKind.TexturedRgba,
         RhiPipelineKind.LayerComposite, RhiPipelineKind.BlurDown, RhiPipelineKind.BlurUp];

    private readonly IntPtr _instance;
    private readonly IntPtr _physicalDevice;
    private readonly uint _queueFamilyIndex;
    private readonly IntPtr _device;
    private readonly IntPtr _queue;
    private VkPhysicalDeviceMemoryProperties _memoryProperties;
    private readonly ulong _commandPool;
    private readonly ulong _vertexModule;
    private readonly Dictionary<RhiPipelineKind, ulong> _fragmentModules = new();
    private readonly ulong _pipelineCache;
    private readonly string _pipelineCachePath = string.Empty;
    private readonly bool _pipelineCacheExisted;
    private readonly ulong _descriptorSetLayout;
    private readonly ulong _pipelineLayout;
    private readonly ulong _descriptorPool;
    private readonly ulong _uniformBuffer;
    private readonly ulong _uniformMemory;
    private readonly byte* _uniformMapped;
    private int _uniformOffset;
    private bool _encoding;
    private readonly Dictionary<(uint Format, uint FinalLayout), ulong> _renderPasses = new();
    private readonly Dictionary<(uint Format, RhiPipelineKind Kind), ulong> _pipelines = new();
    private readonly ulong _nearestSampler;
    private readonly ulong _linearSampler;
    private readonly Dictionary<(ulong Coverage, ulong Color, ulong Sampler), ulong> _descriptorSets = new();
    private readonly Stack<ulong> _freeDescriptorSets = new();

    public VulkanDevice()
    {
        _instance = CreateInstance();
        try
        {
            (_physicalDevice, var queueFamily) = PickPhysicalDevice(_instance);
            _queueFamilyIndex = queueFamily;
            fixed (VkPhysicalDeviceMemoryProperties* memory = &_memoryProperties)
                Vk.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, memory);

            _device = CreateLogicalDevice(_physicalDevice, queueFamily);
            IntPtr queue;
            Vk.vkGetDeviceQueue(_device, queueFamily, 0, &queue);
            _queue = queue;

            var poolInfo = new VkCommandPoolCreateInfo
            {
                SType = VkStructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamily,
            };
            ulong commandPool;
            Vk.Check(Vk.vkCreateCommandPool(_device, &poolInfo, IntPtr.Zero, &commandPool), "command pool creation");
            _commandPool = commandPool;

            // SINGLE-ENTRY SPIR-V per stage. A multi-entry module is the rarity that breaks
            // drivers: MoltenVK caches the SPIR-V→MSL conversion by module BYTES + state, without
            // the entry-point name, so same-state pipelines out of one module silently ran each
            // other's fragment (a constant-red blur_down painted WHITE — sdf_fragment's colorA).
            // Distinct handles over the same bytes did NOT fix it — the key is the bytes — so the
            // build now emits one file per entry (explicit [[vk::binding]] keeps numbers stable),
            // which is also simply what everyone ships.
            var fingerprint = new List<byte>();
            _vertexModule = CreateShaderModule(ReadStage("fullscreen_vertex", fingerprint));
            foreach (var kind in RegistryKinds)
                _fragmentModules[kind] = CreateShaderModule(ReadStage(FragmentStageName(kind), fingerprint));
            var shaderBytes = fingerprint.ToArray();
            (_pipelineCache, _pipelineCachePath, _pipelineCacheExisted) = OpenPipelineCache(shaderBytes);
            // D6b: the two fixed samplers precede the layout (its binding 3 references them).
            _nearestSampler = CreateSampler(linear: false);
            _linearSampler = CreateSampler(linear: true);
            _descriptorSetLayout = CreateDescriptorSetLayout();
            _pipelineLayout = CreatePipelineLayout();
            _descriptorPool = CreateDescriptorPool();
            (_uniformBuffer, _uniformMemory, var uniformMapped) = CreateUniformRing();
            _uniformMapped = (byte*)uniformMapped;

            // D5: the ENTIRE fixed registry is born with the device — offscreen RGBA8 today (the
            // swapchain format joins the registry with the Android shell, plan W5). First launch
            // compiles through the pipeline cache and persists it; later launches build from it.
            foreach (var kind in RegistryKinds)
                CreatePipeline(FormatR8G8B8A8Srgb, kind);
            SavePipelineCache();
        }
        catch
        {
            // Fatal-init path: the device teardown reclaims child resources; nothing keeps running.
            if (_device != IntPtr.Zero) Vk.vkDestroyDevice(_device, IntPtr.Zero);
            Vk.vkDestroyInstance(_instance, IntPtr.Zero);
            throw;
        }
    }

    internal IntPtr Device => _device;
    internal IntPtr Instance => _instance;
    internal IntPtr PhysicalDevice => _physicalDevice;
    internal IntPtr Queue => _queue;
    internal uint QueueFamily => _queueFamilyIndex;

    /// <summary>Wraps an image this device did NOT allocate — a swapchain's — as a render target.</summary>
    internal VulkanRenderTarget AdoptImage(ulong image, uint format, int width, int height)
    {
        var view = CreateImageView(image, format);
        var renderPass = RenderPass(format, LayoutPresentSrc);
        var framebuffer = CreateFramebuffer(renderPass, view, width, height);
        return new VulkanRenderTarget(this, image, 0, view, framebuffer, renderPass, format, width, height)
        {
            OwnsImage = false,
        };
    }

    internal ulong CreateSemaphore()
    {
        var createInfo = new VkSemaphoreCreateInfo { SType = VkStructureType.SemaphoreCreateInfo };
        ulong semaphore;
        Vk.Check(Vk.vkCreateSemaphore(_device, &createInfo, IntPtr.Zero, &semaphore), "semaphore creation");
        return semaphore;
    }
    internal ulong PipelineLayoutHandle => _pipelineLayout;
    internal bool IsDisposed { get; private set; }

    /// <summary>The <see cref="VulkanBackend.IsSupported"/> probe: loader present, an instance
    /// comes up, and some device has a graphics queue + shaderDrawParameters.</summary>
    internal static bool Probe()
    {
        try
        {
            var instance = CreateInstance();
            try
            {
                _ = PickPhysicalDevice(instance);
                return true;
            }
            finally
            {
                Vk.vkDestroyInstance(instance, IntPtr.Zero);
            }
        }
        catch
        {
            return false;
        }
    }

    // ── IRhiDevice ──────────────────────────────────────────────────────────────────────────────

    public IRhiRenderTarget CreateRenderTarget(int width, int height)
    {
        // COLOR_ATTACHMENT (written) | TRANSFER_SRC (readback) | SAMPLED (composited as a layer).
        const uint usage = 0x10 | 0x1 | 0x4;
        var (image, memory, view) = CreateImage(width, height, FormatR8G8B8A8Srgb, usage);
        var renderPass = RenderPass(FormatR8G8B8A8Srgb);
        var framebuffer = CreateFramebuffer(renderPass, view, width, height);
        return new VulkanRenderTarget(this, image, memory, view, framebuffer, renderPass,
            FormatR8G8B8A8Srgb, width, height);
    }

    public IRhiTexture CreateTexture(TextureData data)
    {
        var rgba = data.Format == TextureFormat.Rgba8;
        var format = rgba ? FormatR8G8B8A8Srgb : FormatR8Unorm;
        const uint usage = 0x2 | 0x4; // TRANSFER_DST | SAMPLED
        var (image, memory, view) = CreateImage(data.Width, data.Height, format, usage);

        // Staging upload — synchronous one-shot, mirroring Metal's replaceRegion (uploads are
        // cached by the renderer and never sit on the frame path).
        var byteCount = (ulong)data.Alpha.Length;
        var (staging, stagingMemory) = CreateBuffer(byteCount, usage: 0x1 /* TRANSFER_SRC */, hostVisible: true);
        void* mapped;
        Vk.Check(Vk.vkMapMemory(_device, stagingMemory, 0, byteCount, 0, &mapped), "staging map");
        data.Alpha.AsSpan().CopyTo(new Span<byte>(mapped, data.Alpha.Length));
        Vk.vkUnmapMemory(_device, stagingMemory);

        var command = BeginOneShot();
        TransitionImage(command, image,
            oldLayout: 0, newLayout: 7,          // UNDEFINED → TRANSFER_DST_OPTIMAL
            srcAccess: 0, dstAccess: 0x1000,     // → TRANSFER_WRITE
            srcStage: 0x1, dstStage: 0x1000);    // TOP_OF_PIPE → TRANSFER
        var region = new VkBufferImageCopy
        {
            ImageSubresource = new VkImageSubresourceLayers { AspectMask = 0x1, LayerCount = 1 },
            ImageExtent = new VkExtent3D { Width = (uint)data.Width, Height = (uint)data.Height, Depth = 1 },
        };
        Vk.vkCmdCopyBufferToImage(command, staging, image, 7, 1, &region);
        TransitionImage(command, image,
            oldLayout: 7, newLayout: 5,          // TRANSFER_DST → SHADER_READ_ONLY_OPTIMAL
            srcAccess: 0x1000, dstAccess: 0x20,  // TRANSFER_WRITE → SHADER_READ
            srcStage: 0x1000, dstStage: 0x80);   // TRANSFER → FRAGMENT_SHADER
        EndOneShot(command);

        Vk.vkDestroyBuffer(_device, staging, IntPtr.Zero);
        Vk.vkFreeMemory(_device, stagingMemory, IntPtr.Zero);
        return new VulkanTexture(this, image, memory, view, data.Width, data.Height);
    }

    public IRhiCommandList Begin(IRhiRenderTarget target, Color clearColor)
    {
        if (_encoding)
            throw new InvalidOperationException(
                "A Vulkan pass is already open — the engine encodes one pass at a time (single render thread, plan D10).");
        _encoding = true;
        _uniformOffset = 0;

        var renderTarget = (VulkanRenderTarget)target;
        var allocateInfo = new VkCommandBufferAllocateInfo
        {
            SType = VkStructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            CommandBufferCount = 1,
        };
        IntPtr command;
        Vk.Check(Vk.vkAllocateCommandBuffers(_device, &allocateInfo, &command), "command buffer allocation");
        var beginInfo = new VkCommandBufferBeginInfo
        {
            SType = VkStructureType.CommandBufferBeginInfo,
            Flags = 0x1, // ONE_TIME_SUBMIT
        };
        Vk.Check(Vk.vkBeginCommandBuffer(command, &beginInfo), "command buffer begin");

        // Clear colors are written pre-encoding (linear) — mirror ColorSpace.ToPremultipliedLinear.
        var linear = ColorSpace.ToPremultipliedLinear(clearColor);
        var clearValue = new VkClearValue { R = linear.R, G = linear.G, B = linear.B, A = linear.A };
        var passBegin = new VkRenderPassBeginInfo
        {
            SType = VkStructureType.RenderPassBeginInfo,
            RenderPass = renderTarget.RenderPass,
            Framebuffer = renderTarget.Framebuffer,
            RenderArea = new VkRect2D { ExtentWidth = (uint)target.Width, ExtentHeight = (uint)target.Height },
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };
        Vk.vkCmdBeginRenderPass(command, &passBegin, 0 /* INLINE */);

        var viewport = new VkViewport { Width = target.Width, Height = target.Height, MinDepth = 0, MaxDepth = 1 };
        Vk.vkCmdSetViewport(command, 0, 1, &viewport);
        var scissor = new VkRect2D { ExtentWidth = (uint)target.Width, ExtentHeight = (uint)target.Height };
        Vk.vkCmdSetScissor(command, 0, 1, &scissor);

        return new VulkanCommandList(this, command, renderTarget);
    }

    // ── Per-draw plumbing (called by VulkanCommandList) ─────────────────────────────────────────

    /// <summary>Writes one uniform block into the ring and returns its dynamic offset.</summary>
    internal uint WriteUniforms(in DrawUniforms uniforms)
    {
        if (_uniformOffset + UniformStride > UniformCapacity)
            throw new InvalidOperationException(
                $"Uniform ring exhausted: more than {UniformCapacity / UniformStride} draws in one pass (v1 offscreen fence).");
        fixed (DrawUniforms* source = &uniforms)
            *(DrawUniforms*)(_uniformMapped + _uniformOffset) = *source;
        var offset = (uint)_uniformOffset;
        _uniformOffset += UniformStride;
        return offset;
    }

    /// <summary>
    /// Drops (and recycles) every cached descriptor set that references the dying view — called by
    /// a bindable's Dispose BEFORE <c>vkDestroyImageView</c>. Without this the cache outlives the
    /// view, and because drivers REUSE handle values, a later lookup can hit a stale entry whose
    /// set points at destroyed memory: an intermittent DEVICE_LOST, first seen when backdrop-blur
    /// splits started churning render targets (and invisible under the validation layer, whose
    /// wrapped handles never collide).
    /// </summary>
    internal void OnViewDestroyed(ulong view)
    {
        List<(ulong, ulong, ulong)>? dead = null;
        foreach (var entry in _descriptorSets)
            if (entry.Key.Coverage == view || entry.Key.Color == view)
                (dead ??= new()).Add(entry.Key);
        if (dead is null) return;
        foreach (var key in dead)
        {
            _freeDescriptorSets.Push(_descriptorSets[key]);
            _descriptorSets.Remove(key);
        }
    }

    /// <summary>The descriptor set for a (coverage, color) view pair — allocated and written once,
    /// cached until a view it references is destroyed (see <see cref="OnViewDestroyed"/>; recycled
    /// sets are rewritten here, which is safe because the offscreen path waits out every submit).
    /// One of the pair is almost always the encoder's 1×1 dummy, so the population stays small.</summary>
    internal ulong DescriptorSetFor(ulong coverageView, ulong colorView, ulong sampler)
    {
        if (_descriptorSets.TryGetValue((coverageView, colorView, sampler), out var cached)) return cached;

        ulong set;
        if (_freeDescriptorSets.Count > 0)
        {
            set = _freeDescriptorSets.Pop();
        }
        else
        {
            var layout = _descriptorSetLayout;
            var allocateInfo = new VkDescriptorSetAllocateInfo
            {
                SType = VkStructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &layout,
            };
            Vk.Check(Vk.vkAllocateDescriptorSets(_device, &allocateInfo, &set), "descriptor set allocation");
        }

        var bufferInfo = new VkDescriptorBufferInfo
        {
            Buffer = _uniformBuffer,
            Offset = 0, // dynamic offset supplies the real position per draw
            Range = (ulong)sizeof(DrawUniforms),
        };
        var coverageInfo = new VkDescriptorImageInfo { ImageView = coverageView, ImageLayout = 5 };
        var colorInfo = new VkDescriptorImageInfo { ImageView = colorView, ImageLayout = 5 };
        var samplerInfo = new VkDescriptorImageInfo { Sampler = sampler };
        var writes = stackalloc VkWriteDescriptorSet[4];
        writes[0] = new VkWriteDescriptorSet
        {
            SType = VkStructureType.WriteDescriptorSet,
            DstSet = set, DstBinding = 0, DescriptorCount = 1,
            DescriptorType = 8, // UNIFORM_BUFFER_DYNAMIC
            PBufferInfo = &bufferInfo,
        };
        writes[1] = new VkWriteDescriptorSet
        {
            SType = VkStructureType.WriteDescriptorSet,
            DstSet = set, DstBinding = 1, DescriptorCount = 1,
            DescriptorType = 2, // SAMPLED_IMAGE (Load-only — no sampler anywhere in the engine)
            PImageInfo = &coverageInfo,
        };
        writes[2] = new VkWriteDescriptorSet
        {
            SType = VkStructureType.WriteDescriptorSet,
            DstSet = set, DstBinding = 2, DescriptorCount = 1,
            DescriptorType = 2,
            PImageInfo = &colorInfo,
        };
        writes[3] = new VkWriteDescriptorSet
        {
            SType = VkStructureType.WriteDescriptorSet,
            DstSet = set, DstBinding = 3, DescriptorCount = 1,
            DescriptorType = 0, // SAMPLER
            PImageInfo = &samplerInfo,
        };
        Vk.vkUpdateDescriptorSets(_device, 4, writes, 0, IntPtr.Zero);
        _descriptorSets[(coverageView, colorView, sampler)] = set;
        return set;
    }

    /// <summary>Ends the pass and submits. v1 always waits (offscreen backend — goldens/parity
    /// read pixels right after; real fences arrive with the Android swapchain frame loop, W5).</summary>
    internal void Submit(IntPtr command, VulkanRenderTarget target) =>
        Submit(command, target, waitSemaphore: 0, signalSemaphore: 0);

    /// <summary>
    /// Submits the frame. With semaphores it is a PRESENTED frame: the GPU waits for the image the
    /// display engine handed over, and signals when it is done drawing, so nothing between them is
    /// left to the queue happening to be idle. Without them the caller wants the pixels back, and
    /// waiting for the queue is exactly what that means.
    /// </summary>
    internal void Submit(IntPtr command, VulkanRenderTarget target, ulong waitSemaphore, ulong signalSemaphore)
    {
        Vk.vkCmdEndRenderPass(command);
        Vk.Check(Vk.vkEndCommandBuffer(command), "command buffer end");
        var waitStage = 0x400u; // COLOR_ATTACHMENT_OUTPUT — the first stage that touches the image
        var submitInfo = new VkSubmitInfo
        {
            SType = VkStructureType.SubmitInfo,
            WaitSemaphoreCount = waitSemaphore != 0 ? 1u : 0u,
            PWaitSemaphores = waitSemaphore != 0 ? (IntPtr)(&waitSemaphore) : IntPtr.Zero,
            PWaitDstStageMask = waitSemaphore != 0 ? (IntPtr)(&waitStage) : IntPtr.Zero,
            CommandBufferCount = 1,
            PCommandBuffers = &command,
            SignalSemaphoreCount = signalSemaphore != 0 ? 1u : 0u,
            PSignalSemaphores = signalSemaphore != 0 ? (IntPtr)(&signalSemaphore) : IntPtr.Zero,
        };
        Vk.Check(Vk.vkQueueSubmit(_queue, 1, &submitInfo, 0), "queue submit");
        // v1 waits either way: a frame in flight needs a fence per swapchain image to be safe, and
        // that lands with the pacing work. What the semaphores buy today is CORRECTNESS against the
        // display engine, which a queue wait alone cannot express.
        Vk.Check(Vk.vkQueueWaitIdle(_queue), "queue wait");
        Vk.vkFreeCommandBuffers(_device, _commandPool, 1, &command);
        target.MarkRendered();
        _encoding = false;
    }

    /// <summary>Registry lookup — plan D5's assert: every pipeline is created at device init, so
    /// a miss here is a bug by definition, never a compile trigger.</summary>
    internal ulong Pipeline(uint format, RhiPipelineKind kind)
    {
        if (_pipelines.TryGetValue((format, kind), out var pipeline)) return pipeline;
        throw new InvalidOperationException(
            $"Pipeline (format {format}, {kind}) is outside the fixed registry (plan D5) — " +
            "extend the registry created at device init; pipelines are never created at draw time.");
    }

    private void CreatePipeline(uint format, RhiPipelineKind kind)
    {
        // Single-entry modules all expose "main" — WHICH code runs is chosen by the module
        // (FragmentStageName), never by a name switch here. The old per-entry name switch is
        // exactly what broke the blur kinds: they fell into its default and silently ran
        // sdf_fragment.
        var vertexName = Marshal.StringToCoTaskMemUTF8("main");
        var fragmentName = Marshal.StringToCoTaskMemUTF8("main");
        try
        {
            var stages = stackalloc VkPipelineShaderStageCreateInfo[2];
            stages[0] = new VkPipelineShaderStageCreateInfo
            {
                SType = VkStructureType.PipelineShaderStageCreateInfo,
                Stage = 0x1, Module = _vertexModule, PName = vertexName,
            };
            stages[1] = new VkPipelineShaderStageCreateInfo
            {
                SType = VkStructureType.PipelineShaderStageCreateInfo,
                Stage = 0x10, Module = _fragmentModules[kind], PName = fragmentName,
            };
            var vertexInput = new VkPipelineVertexInputStateCreateInfo
            {
                SType = VkStructureType.PipelineVertexInputStateCreateInfo, // no vertex buffers — SV_VertexID drives the fullscreen triangle
            };
            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo
            {
                SType = VkStructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = 3, // TRIANGLE_LIST
            };
            var viewportState = new VkPipelineViewportStateCreateInfo
            {
                SType = VkStructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1, ScissorCount = 1, // dynamic — pipelines are size-independent
            };
            var rasterization = new VkPipelineRasterizationStateCreateInfo
            {
                SType = VkStructureType.PipelineRasterizationStateCreateInfo,
                LineWidth = 1f, // cull NONE: Vulkan's y-down NDC flips the triangle's winding vs Metal; coverage is identical
            };
            var multisample = new VkPipelineMultisampleStateCreateInfo
            {
                SType = VkStructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = 0x1,
            };
            // Premultiplied source-over — except the blur kinds, whose levels REPLACE their
            // target (there is no destination to blend with inside the pyramid).
            var blends = kind is not (RhiPipelineKind.BlurDown or RhiPipelineKind.BlurUp);
            var blendAttachment = new VkPipelineColorBlendAttachmentState
            {
                BlendEnable = blends ? 1u : 0u,
                SrcColorBlendFactor = 1, DstColorBlendFactor = 7, ColorBlendOp = 0,
                SrcAlphaBlendFactor = 1, DstAlphaBlendFactor = 7, AlphaBlendOp = 0,
                ColorWriteMask = 0xF,
            };
            var blend = new VkPipelineColorBlendStateCreateInfo
            {
                SType = VkStructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = &blendAttachment,
            };
            var dynamicStates = stackalloc uint[2] { 0, 1 }; // VIEWPORT, SCISSOR
            var dynamic = new VkPipelineDynamicStateCreateInfo
            {
                SType = VkStructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };
            var createInfo = new VkGraphicsPipelineCreateInfo
            {
                SType = VkStructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterization,
                PMultisampleState = &multisample,
                PColorBlendState = &blend,
                PDynamicState = &dynamic,
                Layout = _pipelineLayout,
                RenderPass = RenderPass(format),
            };
            ulong pipeline;
            Vk.Check(Vk.vkCreateGraphicsPipelines(_device, _pipelineCache, 1, &createInfo, IntPtr.Zero, &pipeline), "pipeline creation");
            _pipelines[(format, kind)] = pipeline;
        }
        finally
        {
            Marshal.FreeCoTaskMem(vertexName);
            Marshal.FreeCoTaskMem(fragmentName);
        }
    }

    /// <summary>Opens the cross-launch VkPipelineCache (D3 tail) seeded from disk when present.
    /// The driver validates the blob's header (vendor/device/UUID) itself — a stale or foreign
    /// blob degrades to an empty cache; a rejected one retries empty. Zero on failure: caching
    /// degrades, the device never does.</summary>
    private (ulong Cache, string Path, bool Existed) OpenPipelineCache(byte[] shaderBytes)
    {
        if (!PipelineCache.TryGetFile("Sdf", shaderBytes, ".vkpipelinecache", out var path))
            return (0, string.Empty, false);

        byte[]? initialData = null;
        if (File.Exists(path))
        {
            try { initialData = File.ReadAllBytes(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        fixed (byte* data = initialData)
        {
            var createInfo = new VkPipelineCacheCreateInfo
            {
                SType = VkStructureType.PipelineCacheCreateInfo,
                InitialDataSize = (nuint)(initialData?.Length ?? 0),
                PInitialData = (IntPtr)data,
            };
            ulong cache;
            if (Vk.vkCreatePipelineCache(_device, &createInfo, IntPtr.Zero, &cache) == Vk.Success)
                return (cache, path, initialData is not null);
        }

        var empty = new VkPipelineCacheCreateInfo { SType = VkStructureType.PipelineCacheCreateInfo };
        ulong emptyCache;
        return Vk.vkCreatePipelineCache(_device, &empty, IntPtr.Zero, &emptyCache) == Vk.Success
            ? (emptyCache, path, false)
            : (0, string.Empty, false);
    }

    /// <summary>First launch only: persists the cache holding the whole fixed registry.</summary>
    private void SavePipelineCache()
    {
        if (_pipelineCache == 0 || _pipelineCacheExisted || _pipelineCachePath.Length == 0) return;
        nuint size = 0;
        if (Vk.vkGetPipelineCacheData(_device, _pipelineCache, &size, null) != Vk.Success || size == 0) return;
        var data = new byte[size];
        fixed (byte* dataPtr = data)
        {
            if (Vk.vkGetPipelineCacheData(_device, _pipelineCache, &size, dataPtr) != Vk.Success) return;
        }
        try { File.WriteAllBytes(_pipelineCachePath, data); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ── Shared building blocks (textures, targets, readback) ────────────────────────────────────

    internal (ulong Buffer, ulong Memory) CreateBuffer(ulong size, uint usage, bool hostVisible)
    {
        var createInfo = new VkBufferCreateInfo
        {
            SType = VkStructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
        };
        ulong buffer;
        Vk.Check(Vk.vkCreateBuffer(_device, &createInfo, IntPtr.Zero, &buffer), "buffer creation");
        VkMemoryRequirements requirements;
        Vk.vkGetBufferMemoryRequirements(_device, buffer, &requirements);
        var memory = Allocate(requirements, hostVisible ? 0x2u | 0x4u : 0x1u); // HOST_VISIBLE|COHERENT : DEVICE_LOCAL
        Vk.Check(Vk.vkBindBufferMemory(_device, buffer, memory, 0), "buffer bind");
        return (buffer, memory);
    }

    internal IntPtr BeginOneShot()
    {
        var allocateInfo = new VkCommandBufferAllocateInfo
        {
            SType = VkStructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            CommandBufferCount = 1,
        };
        IntPtr command;
        Vk.Check(Vk.vkAllocateCommandBuffers(_device, &allocateInfo, &command), "command buffer allocation");
        var beginInfo = new VkCommandBufferBeginInfo
        {
            SType = VkStructureType.CommandBufferBeginInfo,
            Flags = 0x1, // ONE_TIME_SUBMIT
        };
        Vk.Check(Vk.vkBeginCommandBuffer(command, &beginInfo), "command buffer begin");
        return command;
    }

    internal void EndOneShot(IntPtr command)
    {
        Vk.Check(Vk.vkEndCommandBuffer(command), "command buffer end");
        var submitInfo = new VkSubmitInfo
        {
            SType = VkStructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &command,
        };
        Vk.Check(Vk.vkQueueSubmit(_queue, 1, &submitInfo, 0), "queue submit");
        Vk.Check(Vk.vkQueueWaitIdle(_queue), "queue wait");
        Vk.vkFreeCommandBuffers(_device, _commandPool, 1, &command);
    }

    private (ulong Image, ulong Memory, ulong View) CreateImage(int width, int height, uint format, uint usage)
    {
        var imageInfo = new VkImageCreateInfo
        {
            SType = VkStructureType.ImageCreateInfo,
            ImageType = 1, // 2D
            Format = format,
            Extent = new VkExtent3D { Width = (uint)width, Height = (uint)height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = 0x1,
            // This assignment was MISSING from the backend's first day: every image was created
            // with usage 0 — invalid per spec, so behavior was undefined — and MoltenVK happened
            // to tolerate it for attachment/Load use, which is why 44 parity scenes were green.
            // The blur was the first path that truly depended on SAMPLED, and the validation layer
            // (first run today) called it out as "pCreateInfo->usage is zero".
            Usage = usage,
        };
        ulong image;
        Vk.Check(Vk.vkCreateImage(_device, &imageInfo, IntPtr.Zero, &image), "image creation");
        VkMemoryRequirements requirements;
        Vk.vkGetImageMemoryRequirements(_device, image, &requirements);
        var memory = Allocate(requirements, 0x1); // DEVICE_LOCAL
        Vk.Check(Vk.vkBindImageMemory(_device, image, memory, 0), "image bind");

        return (image, memory, CreateImageView(image, format));
    }

    /// <summary>A 2D colour view of an image — ours, or a swapchain's.</summary>
    private ulong CreateImageView(ulong image, uint format)
    {
        var viewInfo = new VkImageViewCreateInfo
        {
            SType = VkStructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = 1, // 2D
            Format = format,
            SubresourceRange = new VkImageSubresourceRange { AspectMask = 0x1, LevelCount = 1, LayerCount = 1 },
        };
        ulong view;
        Vk.Check(Vk.vkCreateImageView(_device, &viewInfo, IntPtr.Zero, &view), "image view creation");
        return view;
    }

    private ulong Allocate(in VkMemoryRequirements requirements, uint requiredFlags)
    {
        var allocateInfo = new VkMemoryAllocateInfo
        {
            SType = VkStructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, requiredFlags),
        };
        ulong memory;
        Vk.Check(Vk.vkAllocateMemory(_device, &allocateInfo, IntPtr.Zero, &memory), "memory allocation");
        return memory;
    }

    private uint FindMemoryType(uint typeBits, uint requiredFlags)
    {
        for (var i = 0; i < _memoryProperties.MemoryTypeCount; i++)
            if ((typeBits & (1u << i)) != 0 && (_memoryProperties.TypePropertyFlags(i) & requiredFlags) == requiredFlags)
                return (uint)i;
        throw new InvalidOperationException("No compatible Vulkan memory type.");
    }

    private static void TransitionImage(IntPtr command, ulong image, uint oldLayout, uint newLayout,
        uint srcAccess, uint dstAccess, uint srcStage, uint dstStage)
    {
        var barrier = new VkImageMemoryBarrier
        {
            SType = VkStructureType.ImageMemoryBarrier,
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = ~0u,
            DstQueueFamilyIndex = ~0u,
            Image = image,
            SubresourceRange = new VkImageSubresourceRange { AspectMask = 0x1, LevelCount = 1, LayerCount = 1 },
        };
        Vk.vkCmdPipelineBarrier(command, srcStage, dstStage, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, &barrier);
    }

    /// <summary>
    /// The pass a target is drawn through. <paramref name="finalLayout"/> is where the image is
    /// LEFT: a layer target is sampled next, so it ends shader-readable; a swapchain image is shown
    /// next, so it ends presentable. Everything else about the two passes is identical, which is
    /// why they are one method and not two.
    /// </summary>
    internal ulong RenderPass(uint format, uint finalLayout = LayoutShaderReadOnly)
    {
        if (_renderPasses.TryGetValue((format, finalLayout), out var cached)) return cached;

        // One pass, one color attachment (D4): CLEAR on load (the display list's leading Clear),
        // STORE, finish in TRANSFER_SRC — readback-ready without an extra barrier.
        var attachment = new VkAttachmentDescription
        {
            Format = format,
            Samples = 0x1,
            LoadOp = 1,          // CLEAR
            StoreOp = 0,         // STORE
            StencilLoadOp = 2,   // DONT_CARE
            StencilStoreOp = 1,  // DONT_CARE
            InitialLayout = 0,   // UNDEFINED (re-renders discard — loadOp clears anyway)
            FinalLayout = finalLayout,
        };
        var reference = new VkAttachmentReference { Attachment = 0, Layout = 2 /* COLOR_ATTACHMENT_OPTIMAL */ };
        var subpass = new VkSubpassDescription
        {
            ColorAttachmentCount = 1,
            PColorAttachments = &reference,
        };
        var dependencies = stackalloc VkSubpassDependency[2];
        dependencies[0] = new VkSubpassDependency
        {
            SrcSubpass = ~0u, DstSubpass = 0,
            SrcStageMask = 0x400, DstStageMask = 0x400,  // COLOR_ATTACHMENT_OUTPUT → itself (layout transition ordering)
            SrcAccessMask = 0, DstAccessMask = 0x100,    // → COLOR_ATTACHMENT_WRITE
        };
        dependencies[1] = new VkSubpassDependency
        {
            SrcSubpass = 0, DstSubpass = ~0u,
            SrcStageMask = 0x400, DstStageMask = 0x80,   // COLOR_ATTACHMENT_OUTPUT → FRAGMENT_SHADER
            SrcAccessMask = 0x100, DstAccessMask = 0x20,  // COLOR_ATTACHMENT_WRITE → SHADER_READ
        };
        // A presented image is read by the display engine, not by a shader: nothing downstream in
        // this device waits on it, and the semaphore the present waits on is the real ordering.
        if (finalLayout == LayoutPresentSrc)
        {
            dependencies[1].DstStageMask = 0x2000;  // BOTTOM_OF_PIPE
            dependencies[1].DstAccessMask = 0;
        }
        var createInfo = new VkRenderPassCreateInfo
        {
            SType = VkStructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &attachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 2,
            PDependencies = dependencies,
        };
        ulong renderPass;
        Vk.Check(Vk.vkCreateRenderPass(_device, &createInfo, IntPtr.Zero, &renderPass), "render pass creation");
        _renderPasses[(format, finalLayout)] = renderPass;
        return renderPass;
    }

    private ulong CreateFramebuffer(ulong renderPass, ulong view, int width, int height)
    {
        var attachment = view;
        var createInfo = new VkFramebufferCreateInfo
        {
            SType = VkStructureType.FramebufferCreateInfo,
            RenderPass = renderPass,
            AttachmentCount = 1,
            PAttachments = &attachment,
            Width = (uint)width,
            Height = (uint)height,
            Layers = 1,
        };
        ulong framebuffer;
        Vk.Check(Vk.vkCreateFramebuffer(_device, &createInfo, IntPtr.Zero, &framebuffer), "framebuffer creation");
        return framebuffer;
    }

    // ── Fixed init pieces ───────────────────────────────────────────────────────────────────────

    internal ulong Sampler(RhiFilter filter) => filter == RhiFilter.Linear ? _linearSampler : _nearestSampler;

    private ulong CreateSampler(bool linear)
    {
        var filter = linear ? 1u : 0u;
        var createInfo = new VkSamplerCreateInfo
        {
            SType = VkStructureType.SamplerCreateInfo,
            MagFilter = filter,
            MinFilter = filter,
            AddressModeU = 2, // CLAMP_TO_EDGE — matches the CPU twin's uv clamp
            AddressModeV = 2,
            AddressModeW = 2,
        };
        ulong sampler;
        Vk.Check(Vk.vkCreateSampler(_device, &createInfo, IntPtr.Zero, &sampler), "sampler creation");
        return sampler;
    }

    private static string FragmentStageName(RhiPipelineKind kind) => kind switch
    {
        RhiPipelineKind.TexturedA8 => "textured_fragment",
        RhiPipelineKind.TexturedRgba => "textured_rgba_fragment",
        RhiPipelineKind.LayerComposite => "layer_composite",
        RhiPipelineKind.BlurDown => "blur_down",
        RhiPipelineKind.BlurUp => "blur_up",
        _ => "sdf_fragment",
    };

    /// <summary>Reads one stage's SPIR-V and folds its bytes into the pipeline-cache fingerprint.</summary>
    private static byte[] ReadStage(string name, List<byte> fingerprint)
    {
        using var stream = typeof(VulkanDevice).Assembly.GetManifestResourceStream($"Photon.spirv.{name}.spv")
            ?? throw new InvalidOperationException($"Embedded SPIR-V stage 'Photon.spirv.{name}.spv' missing.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        fingerprint.AddRange(bytes);
        return bytes;
    }

    private ulong CreateShaderModule(byte[] bytes)
    {
        fixed (byte* code = bytes)
        {
            var createInfo = new VkShaderModuleCreateInfo
            {
                SType = VkStructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)bytes.Length,
                PCode = (uint*)code,
            };
            ulong module;
            Vk.Check(Vk.vkCreateShaderModule(_device, &createInfo, IntPtr.Zero, &module), "shader module creation");
            return module;
        }
    }

    private ulong CreateDescriptorSetLayout()
    {
        // The ONE normative shader's interface (set 0): binding 0 = DrawUniforms (dynamic UBO),
        // binding 1 = coverageTexture, binding 2 = colorTexture — pinned by the committed SPIR-V.
        var bindings = stackalloc VkDescriptorSetLayoutBinding[4];
        bindings[0] = new VkDescriptorSetLayoutBinding
        {
            Binding = 0, DescriptorType = 8 /* UNIFORM_BUFFER_DYNAMIC */, DescriptorCount = 1,
            StageFlags = 0x1 | 0x10, // VERTEX | FRAGMENT
        };
        bindings[1] = new VkDescriptorSetLayoutBinding
        {
            Binding = 1, DescriptorType = 2 /* SAMPLED_IMAGE */, DescriptorCount = 1, StageFlags = 0x10,
        };
        bindings[2] = new VkDescriptorSetLayoutBinding
        {
            Binding = 2, DescriptorType = 2, DescriptorCount = 1, StageFlags = 0x10,
        };
        bindings[3] = new VkDescriptorSetLayoutBinding
        {
            Binding = 3, DescriptorType = 0 /* SAMPLER (D6b) */, DescriptorCount = 1, StageFlags = 0x10,
        };
        var createInfo = new VkDescriptorSetLayoutCreateInfo
        {
            SType = VkStructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 4,
            PBindings = bindings,
        };
        ulong layout;
        Vk.Check(Vk.vkCreateDescriptorSetLayout(_device, &createInfo, IntPtr.Zero, &layout), "descriptor set layout creation");
        return layout;
    }

    private ulong CreatePipelineLayout()
    {
        var setLayout = _descriptorSetLayout;
        var createInfo = new VkPipelineLayoutCreateInfo
        {
            SType = VkStructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = (IntPtr)(&setLayout),
        };
        ulong layout;
        Vk.Check(Vk.vkCreatePipelineLayout(_device, &createInfo, IntPtr.Zero, &layout), "pipeline layout creation");
        return layout;
    }

    private ulong CreateDescriptorPool()
    {
        var sizes = stackalloc VkDescriptorPoolSize[3];
        sizes[0] = new VkDescriptorPoolSize { Type = 8, DescriptorCount = 1024 };
        sizes[1] = new VkDescriptorPoolSize { Type = 2, DescriptorCount = 2048 };
        sizes[2] = new VkDescriptorPoolSize { Type = 0, DescriptorCount = 1024 };
        var createInfo = new VkDescriptorPoolCreateInfo
        {
            SType = VkStructureType.DescriptorPoolCreateInfo,
            MaxSets = 1024, // (coverage, color) pairs ≈ live textures + 1 — orders above scene scale
            PoolSizeCount = 3,
            PPoolSizes = sizes,
        };
        ulong pool;
        Vk.Check(Vk.vkCreateDescriptorPool(_device, &createInfo, IntPtr.Zero, &pool), "descriptor pool creation");
        return pool;
    }

    private (ulong Buffer, ulong Memory, IntPtr Mapped) CreateUniformRing()
    {
        var (buffer, memory) = CreateBuffer(UniformCapacity, usage: 0x10 /* UNIFORM_BUFFER */, hostVisible: true);
        void* mapped;
        Vk.Check(Vk.vkMapMemory(_device, memory, 0, UniformCapacity, 0, &mapped), "uniform ring map");
        return (buffer, memory, (IntPtr)mapped);
    }

    private static IntPtr CreateInstance()
    {
        var extensions = new List<string>();
        uint flags = 0;
        // Dev hosts running through MoltenVK need portability enumeration; Android/Windows/Linux
        // loaders don't expose the extension and take the plain path.
        // Presentation: a surface to draw at, and the platform's way of naming one. Absent on a
        // headless host, where the backend stays exactly what it was — offscreen.
        foreach (var surfaceExtension in (string[])["VK_KHR_surface", "VK_KHR_android_surface"])
            if (InstanceExtensionPresent(surfaceExtension))
                extensions.Add(surfaceExtension);

        if (InstanceExtensionPresent("VK_KHR_portability_enumeration"))
        {
            extensions.Add("VK_KHR_portability_enumeration");
            flags = 0x1; // ENUMERATE_PORTABILITY_KHR
        }

        var layers = new List<string>();
        if (Environment.GetEnvironmentVariable("EQ_VULKAN_VALIDATION") == "1" &&
            LayerPresent("VK_LAYER_KHRONOS_validation"))
        {
            // DEV-ONLY plumbing: the brew layer manifest carries a bare file name, which the
            // loader dlopens against the process's DYLD paths — and a .NET host has no
            // /opt/homebrew/lib there, so instance creation dies with LAYER_NOT_PRESENT even
            // though enumeration (manifest-only) lists it. Pre-loading the dylib lets the
            // loader's by-name dlopen resolve against the already-loaded image.
            foreach (var candidate in (string[])
                     ["/opt/homebrew/lib/libVkLayer_khronos_validation.dylib",
                      "/usr/local/lib/libVkLayer_khronos_validation.dylib"])
                if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out _))
                    break;
            layers.Add("VK_LAYER_KHRONOS_validation");
        }

        using var extensionNames = new Utf8StringArray(extensions);
        using var layerNames = new Utf8StringArray(layers);
        var engineName = Marshal.StringToCoTaskMemUTF8("Photon");
        try
        {
            var applicationInfo = new VkApplicationInfo
            {
                SType = VkStructureType.ApplicationInfo,
                PEngineName = engineName,
                ApiVersion = Vk.ApiVersion12, // SPIR-V 1.5 (the committed Slang output) is core in 1.2
            };
            var createInfo = new VkInstanceCreateInfo
            {
                SType = VkStructureType.InstanceCreateInfo,
                Flags = flags,
                PApplicationInfo = &applicationInfo,
                EnabledLayerCount = layerNames.Count,
                PpEnabledLayerNames = layerNames.Pointer,
                EnabledExtensionCount = extensionNames.Count,
                PpEnabledExtensionNames = extensionNames.Pointer,
            };
            IntPtr instance;
            Vk.Check(Vk.vkCreateInstance(&createInfo, IntPtr.Zero, &instance), "instance creation");
            return instance;
        }
        finally
        {
            Marshal.FreeCoTaskMem(engineName);
        }
    }

    private static (IntPtr PhysicalDevice, uint QueueFamily) PickPhysicalDevice(IntPtr instance)
    {
        uint count = 0;
        Vk.Check(Vk.vkEnumeratePhysicalDevices(instance, &count, null), "physical device enumeration");
        if (count == 0) throw new PlatformNotSupportedException("No Vulkan physical device.");
        var devices = new IntPtr[count];
        fixed (IntPtr* devicePtr = devices)
            Vk.Check(Vk.vkEnumeratePhysicalDevices(instance, &count, devicePtr), "physical device enumeration");

        foreach (var candidate in devices)
        {
            var family = FindGraphicsQueueFamily(candidate);
            if (family is null) continue;
            if (!SupportsShaderDrawParameters(candidate)) continue;
            return (candidate, family.Value);
        }
        throw new PlatformNotSupportedException(
            "No Vulkan device with a graphics queue and shaderDrawParameters (required by the SPIR-V's SV_VertexID lowering).");
    }

    private static uint? FindGraphicsQueueFamily(IntPtr physicalDevice)
    {
        uint count = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, null);
        if (count == 0) return null;
        var families = new VkQueueFamilyProperties[count];
        fixed (VkQueueFamilyProperties* familyPtr = families)
            Vk.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, familyPtr);
        for (var i = 0u; i < count; i++)
            if ((families[i].QueueFlags & 0x1) != 0) return i; // GRAPHICS
        return null;
    }

    private static bool SupportsShaderDrawParameters(IntPtr physicalDevice)
    {
        var drawParameters = new VkPhysicalDeviceShaderDrawParametersFeatures
        {
            SType = VkStructureType.PhysicalDeviceShaderDrawParametersFeatures,
        };
        var features = new VkPhysicalDeviceFeatures2
        {
            SType = VkStructureType.PhysicalDeviceFeatures2,
            PNext = (IntPtr)(&drawParameters),
        };
        Vk.vkGetPhysicalDeviceFeatures2(physicalDevice, &features);
        return drawParameters.ShaderDrawParameters != 0;
    }

    private static IntPtr CreateLogicalDevice(IntPtr physicalDevice, uint queueFamily)
    {
        var extensions = new List<string>();
        // Spec rule: a device advertising portability_subset MUST enable it (MoltenVK dev hosts).
        if (DeviceExtensionPresent(physicalDevice, "VK_KHR_portability_subset"))
            extensions.Add("VK_KHR_portability_subset");
        // The swapchain itself: a device feature, not an instance one.
        if (DeviceExtensionPresent(physicalDevice, "VK_KHR_swapchain"))
            extensions.Add("VK_KHR_swapchain");

        using var extensionNames = new Utf8StringArray(extensions);
        var priority = 1f;
        var queueInfo = new VkDeviceQueueCreateInfo
        {
            SType = VkStructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = queueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };
        var drawParameters = new VkPhysicalDeviceShaderDrawParametersFeatures
        {
            SType = VkStructureType.PhysicalDeviceShaderDrawParametersFeatures,
            ShaderDrawParameters = 1,
        };
        var createInfo = new VkDeviceCreateInfo
        {
            SType = VkStructureType.DeviceCreateInfo,
            PNext = (IntPtr)(&drawParameters),
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueInfo,
            EnabledExtensionCount = extensionNames.Count,
            PpEnabledExtensionNames = extensionNames.Pointer,
        };
        IntPtr device;
        Vk.Check(Vk.vkCreateDevice(physicalDevice, &createInfo, IntPtr.Zero, &device), "device creation");
        return device;
    }

    private static bool InstanceExtensionPresent(string name)
    {
        uint count = 0;
        if (Vk.vkEnumerateInstanceExtensionProperties(null, &count, null) != Vk.Success || count == 0) return false;
        var properties = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* propertyPtr = properties)
        {
            if (Vk.vkEnumerateInstanceExtensionProperties(null, &count, propertyPtr) != Vk.Success) return false;
            for (var i = 0; i < count; i++)
                if (Vk.FixedName(propertyPtr[i].ExtensionName) == name) return true;
        }
        return false;
    }

    private static bool DeviceExtensionPresent(IntPtr physicalDevice, string name)
    {
        uint count = 0;
        if (Vk.vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, null) != Vk.Success || count == 0) return false;
        var properties = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* propertyPtr = properties)
        {
            if (Vk.vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, propertyPtr) != Vk.Success) return false;
            for (var i = 0; i < count; i++)
                if (Vk.FixedName(propertyPtr[i].ExtensionName) == name) return true;
        }
        return false;
    }

    private static bool LayerPresent(string name)
    {
        uint count = 0;
        if (Vk.vkEnumerateInstanceLayerProperties(&count, null) != Vk.Success || count == 0) return false;
        var properties = new VkLayerProperties[count];
        fixed (VkLayerProperties* propertyPtr = properties)
        {
            if (Vk.vkEnumerateInstanceLayerProperties(&count, propertyPtr) != Vk.Success) return false;
            for (var i = 0; i < count; i++)
                if (Vk.FixedName(propertyPtr[i].LayerName) == name) return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (_device != IntPtr.Zero)
        {
            Vk.vkDeviceWaitIdle(_device);
            Vk.vkUnmapMemory(_device, _uniformMemory);
            Vk.vkDestroyBuffer(_device, _uniformBuffer, IntPtr.Zero);
            Vk.vkFreeMemory(_device, _uniformMemory, IntPtr.Zero);
            foreach (var pipeline in _pipelines.Values) Vk.vkDestroyPipeline(_device, pipeline, IntPtr.Zero);
            if (_pipelineCache != 0) Vk.vkDestroyPipelineCache(_device, _pipelineCache, IntPtr.Zero);
            foreach (var renderPass in _renderPasses.Values) Vk.vkDestroyRenderPass(_device, renderPass, IntPtr.Zero);
            Vk.vkDestroySampler(_device, _nearestSampler, IntPtr.Zero);
            Vk.vkDestroySampler(_device, _linearSampler, IntPtr.Zero);
            Vk.vkDestroyDescriptorPool(_device, _descriptorPool, IntPtr.Zero); // reclaims every set
            Vk.vkDestroyPipelineLayout(_device, _pipelineLayout, IntPtr.Zero);
            Vk.vkDestroyDescriptorSetLayout(_device, _descriptorSetLayout, IntPtr.Zero);
            Vk.vkDestroyShaderModule(_device, _vertexModule, IntPtr.Zero);
            foreach (var module in _fragmentModules.Values) Vk.vkDestroyShaderModule(_device, module, IntPtr.Zero);
            Vk.vkDestroyCommandPool(_device, _commandPool, IntPtr.Zero);
            Vk.vkDestroyDevice(_device, IntPtr.Zero);
        }
        if (_instance != IntPtr.Zero) Vk.vkDestroyInstance(_instance, IntPtr.Zero);
    }
}
