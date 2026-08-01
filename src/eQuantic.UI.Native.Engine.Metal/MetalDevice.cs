using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// The Metal <see cref="IRhiDevice"/>: owns the MTLDevice/queue, loads the offline-compiled
/// metallib (plan D3 — the MSL source path stays as the dev fallback so a fresh Slang regen
/// without the Metal Toolchain still runs), and builds the FIXED pipeline set per target pixel
/// format (plan D5; the offscreen SDF pipeline is created with the device, fail-fast). Interop is
/// the spike-proven slim typed <c>objc_msgSend</c> layer (<see cref="ObjC"/>); Objective-C object
/// lifetimes stay per-process — the documented fence, owned by the binding layer's retain/release
/// work item (plan open question 2).
/// </summary>
public sealed class MetalDevice : IRhiDevice
{
    internal const ulong PixelFormatRgba8UnormSrgb = 71; // MTLPixelFormatRGBA8Unorm_sRGB
    internal const ulong PixelFormatBgra8UnormSrgb = 81; // MTLPixelFormatBGRA8Unorm_sRGB (CAMetalLayer)
    private const ulong PixelFormatR8Unorm = 10;
    private const ulong LoadActionClear = 2;
    private const ulong StoreActionStore = 1;
    // A layer target is WRITTEN as an attachment and then READ as a texture when it composites,
    // so it needs both usages — ShaderRead alone would fail the draw, RenderTarget alone the bind.
    private const ulong TextureUsageRenderTarget = 0x04 | 0x01;
    private const ulong StorageModeShared = 0;

    private static readonly RhiPipelineKind[] RegistryKinds =
        [RhiPipelineKind.Sdf, RhiPipelineKind.TexturedA8, RhiPipelineKind.TexturedRgba,
         RhiPipelineKind.LayerComposite, RhiPipelineKind.BlurDown, RhiPipelineKind.BlurUp];

    /// <summary>The FIXED registry's target formats (D5): offscreen RGBA8 + the CAMetalLayer's BGRA8.</summary>
    private static readonly ulong[] RegistryFormats = [PixelFormatRgba8UnormSrgb, PixelFormatBgra8UnormSrgb];

    private readonly IntPtr _device;
    private readonly IntPtr _queue;
    private readonly IntPtr _vertexFn;
    private readonly IntPtr _fragmentFn;
    private readonly IntPtr _texturedFn;
    private readonly IntPtr _texturedRgbaFn;
    private readonly IntPtr _layerCompositeFn;
    private readonly IntPtr _blurDownFn;
    private readonly IntPtr _blurUpFn;
    private readonly IntPtr _nearestSampler;
    private readonly IntPtr _linearSampler;
    private readonly IntPtr _binaryArchive;
    private readonly string _binaryArchivePath = string.Empty;
    private readonly bool _binaryArchiveExisted;
    private readonly Dictionary<(ulong Format, RhiPipelineKind Kind), IntPtr> _pipelines = new();

    public MetalDevice()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The Metal backend requires macOS/Apple hardware.");

        _device = ObjC.MTLCreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
            throw new PlatformNotSupportedException("No Metal device available.");

        _queue = ObjC.Send(_device, Sel("newCommandQueue"));

        // D3: the PRECOMPILED metallib loads first — zero runtime shader compilation (the
        // engine's founding thesis). The MSL source path stays as the dev fallback. The loaded
        // bytes also FINGERPRINT the cross-launch binary archive: a new shader targets a new file.
        var metallib = ReadResource("Photon.Sdf.metallib");
        var library = metallib is null ? IntPtr.Zero : LoadLibraryFromData(metallib);
        byte[] shaderBytes;
        if (library != IntPtr.Zero)
        {
            shaderBytes = metallib!;
        }
        else
        {
            library = ObjC.Send(_device, Sel("newLibraryWithSource:options:error:"),
                ObjC.NSString(MetalShaders.Source), IntPtr.Zero, out var libraryError);
            if (library == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"MSL compilation failed: {DescribeError(libraryError)}");
            shaderBytes = System.Text.Encoding.UTF8.GetBytes(MetalShaders.Source);
        }

        _vertexFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("fullscreen_vertex"));
        _fragmentFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("sdf_fragment"));
        _texturedFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("textured_fragment"));
        _texturedRgbaFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("textured_rgba_fragment"));
        _layerCompositeFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("layer_composite"));
        _blurDownFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("blur_down"));
        _blurUpFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("blur_up"));

        // D6b: the two fixed sampler states. Clamp-to-edge matches the CPU twin's uv clamp.
        _nearestSampler = CreateSampler(linear: false);
        _linearSampler = CreateSampler(linear: true);

        (_binaryArchive, _binaryArchivePath, _binaryArchiveExisted) = TryOpenBinaryArchive(shaderBytes);

        // D5: the ENTIRE fixed registry is born with the device — first launch compiles and
        // serializes the binary archive; every later launch builds all PSOs from it.
        foreach (var format in RegistryFormats)
            foreach (var kind in RegistryKinds)
                CreatePipelineState(format, kind);
        SerializeBinaryArchive();
    }

    /// <summary>The MTLDevice handle — the shell hands it to the window's CAMetalLayer.</summary>
    public IntPtr Handle => _device;

    public IRhiRenderTarget CreateRenderTarget(int width, int height)
    {
        var descriptor = ObjC.Send(ObjC.objc_getClass("MTLTextureDescriptor"),
            Sel("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            PixelFormatRgba8UnormSrgb, (ulong)width, (ulong)height, false);
        ObjC.SendVoid(descriptor, Sel("setUsage:"), TextureUsageRenderTarget);
        ObjC.SendVoid(descriptor, Sel("setStorageMode:"), StorageModeShared);

        var texture = ObjC.Send(_device, Sel("newTextureWithDescriptor:"), descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException("Metal render-target creation failed.");
        return new MetalRenderTarget(texture, width, height);
    }

    public IRhiTexture CreateTexture(TextureData data)
    {
        var rgba = data.Format == TextureFormat.Rgba8;
        var descriptor = ObjC.Send(ObjC.objc_getClass("MTLTextureDescriptor"),
            Sel("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            rgba ? PixelFormatRgba8UnormSrgb : PixelFormatR8Unorm, (ulong)data.Width, (ulong)data.Height, false);
        ObjC.SendVoid(descriptor, Sel("setStorageMode:"), StorageModeShared);
        var texture = ObjC.Send(_device, Sel("newTextureWithDescriptor:"), descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException("Metal texture creation failed.");
        var bytesPerRow = (ulong)data.Width * (rgba ? 4ul : 1ul);
        unsafe
        {
            fixed (byte* bytes = data.Alpha)
            {
                ObjC.SendVoid(texture, Sel("replaceRegion:mipmapLevel:withBytes:bytesPerRow:"),
                    new MTLRegion { Width = (ulong)data.Width, Height = (ulong)data.Height, Depth = 1 },
                    0, (IntPtr)bytes, bytesPerRow);
            }
        }
        return new MetalTexture(texture, data.Width, data.Height);
    }

    public IRhiCommandList Begin(IRhiRenderTarget target, Color clearColor) =>
        BeginPass(((MetalRenderTarget)target).Texture, PixelFormatRgba8UnormSrgb, IntPtr.Zero, clearColor);

    /// <summary>Shell path: a pass targeting a WINDOW drawable's texture — the drawable is
    /// presented when the command list submits. Pipelines resolve for the layer's pixel format
    /// (the shaders are format-agnostic; the write swizzle is the hardware's).</summary>
    public IRhiCommandList BeginDrawable(IntPtr drawableTexture, ulong pixelFormat, IntPtr drawable, Color clearColor) =>
        BeginPass(drawableTexture, pixelFormat, drawable, clearColor);

    private MetalCommandList BeginPass(IntPtr targetTexture, ulong pixelFormat, IntPtr drawable, Color clearColor)
    {
        var passDescriptor = ObjC.Send(ObjC.objc_getClass("MTLRenderPassDescriptor"), Sel("renderPassDescriptor"));
        var attachment = ObjC.Send(ObjC.Send(passDescriptor, Sel("colorAttachments")), Sel("objectAtIndexedSubscript:"), 0ul);
        ObjC.SendVoid(attachment, Sel("setTexture:"), targetTexture);
        ObjC.SendVoid(attachment, Sel("setLoadAction:"), LoadActionClear);
        ObjC.SendVoid(attachment, Sel("setStoreAction:"), StoreActionStore);
        // Clear colors are written pre-encoding (linear) — mirror ColorSpace.ToPremultipliedLinear.
        var linear = ColorSpace.ToPremultipliedLinear(clearColor);
        ObjC.SendVoid(attachment, Sel("setClearColor:"),
            new MTLClearColor { Red = linear.R, Green = linear.G, Blue = linear.B, Alpha = linear.A });

        var commandBuffer = ObjC.Send(_queue, Sel("commandBuffer"));
        var encoder = ObjC.Send(commandBuffer, Sel("renderCommandEncoderWithDescriptor:"), passDescriptor);
        return new MetalCommandList(this, commandBuffer, encoder, pixelFormat, drawable);
    }

    /// <summary>Registry lookup — plan D5's assert: every pipeline is created at device init, so
    /// a miss here is a bug by definition, never a compile trigger.</summary>
    internal IntPtr PipelineState(ulong pixelFormat, RhiPipelineKind kind)
    {
        if (_pipelines.TryGetValue((pixelFormat, kind), out var pipeline)) return pipeline;
        throw new InvalidOperationException(
            $"Pipeline (pixel format {pixelFormat}, {kind}) is outside the fixed registry (plan D5) — " +
            "extend the registry created at device init; pipelines are never created at draw time.");
    }

    // Kind mapping: Sdf → sdf_fragment, TexturedA8 → textured_fragment, TexturedRgba → textured_rgba_fragment.
    private void CreatePipelineState(ulong pixelFormat, RhiPipelineKind kind)
    {
        var descriptor = ObjC.Send(ObjC.Send(ObjC.objc_getClass("MTLRenderPipelineDescriptor"), Sel("alloc")), Sel("init"));
        ObjC.SendVoid(descriptor, Sel("setVertexFunction:"), _vertexFn);
        ObjC.SendVoid(descriptor, Sel("setFragmentFunction:"), kind switch
        {
            RhiPipelineKind.TexturedA8 => _texturedFn,
            RhiPipelineKind.TexturedRgba => _texturedRgbaFn,
            RhiPipelineKind.LayerComposite => _layerCompositeFn,
            RhiPipelineKind.BlurDown => _blurDownFn,
            RhiPipelineKind.BlurUp => _blurUpFn,
            _ => _fragmentFn,
        });

        var attachment = ObjC.Send(ObjC.Send(descriptor, Sel("colorAttachments")), Sel("objectAtIndexedSubscript:"), 0ul);
        ObjC.SendVoid(attachment, Sel("setPixelFormat:"), pixelFormat);
        // Blur levels REPLACE their target (no destination to blend with); everything else is
        // premultiplied source-over.
        var blends = kind is not (RhiPipelineKind.BlurDown or RhiPipelineKind.BlurUp);
        ObjC.SendVoid(attachment, Sel("setBlendingEnabled:"), blends);
        // Premultiplied source-over: dst' = src + dst · (1 − src.A) — LinearColor.Over, in hardware.
        const ulong factorOne = 1;                    // MTLBlendFactorOne
        const ulong factorOneMinusSourceAlpha = 5;    // MTLBlendFactorOneMinusSourceAlpha
        ObjC.SendVoid(attachment, Sel("setSourceRGBBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setSourceAlphaBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setDestinationRGBBlendFactor:"), factorOneMinusSourceAlpha);
        ObjC.SendVoid(attachment, Sel("setDestinationAlphaBlendFactor:"), factorOneMinusSourceAlpha);

        // The cross-launch cache (D3 tail): with the archive attached, creation is a lookup on
        // every launch after the first; on the first, the compiled PSO is added for serialization.
        if (_binaryArchive != IntPtr.Zero)
            ObjC.SendVoid(descriptor, Sel("setBinaryArchives:"),
                ObjC.Send(ObjC.objc_getClass("NSArray"), Sel("arrayWithObject:"), _binaryArchive));

        var pipeline = ObjC.Send(_device, Sel("newRenderPipelineStateWithDescriptor:error:"),
            descriptor, out var pipelineError);
        if (pipeline == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Pipeline creation failed: {DescribeError(pipelineError)}");
        if (_binaryArchive != IntPtr.Zero && !_binaryArchiveExisted)
            ObjC.SendBool(_binaryArchive, Sel("addRenderPipelineFunctionsWithDescriptor:error:"), descriptor, out _);
        _pipelines[(pixelFormat, kind)] = pipeline;
    }

    /// <summary>Opens the on-disk binary archive for this shader fingerprint: loads it when
    /// present, starts empty on first launch (or when the file is corrupt — deleted and rebuilt).
    /// Zero on any failure — caching degrades, the device never does.</summary>
    private (IntPtr Archive, string Path, bool Existed) TryOpenBinaryArchive(byte[] shaderBytes)
    {
        if (!PipelineCache.TryGetFile("Sdf", shaderBytes, ".metalarchive", out var path))
            return (IntPtr.Zero, string.Empty, false);
        var descriptorClass = ObjC.objc_getClass("MTLBinaryArchiveDescriptor");
        if (descriptorClass == IntPtr.Zero) return (IntPtr.Zero, string.Empty, false);

        var existed = File.Exists(path);
        var descriptor = ObjC.Send(ObjC.Send(descriptorClass, Sel("alloc")), Sel("init"));
        if (existed)
            ObjC.SendVoid(descriptor, Sel("setUrl:"), FileUrl(path));
        var archive = ObjC.Send(_device, Sel("newBinaryArchiveWithDescriptor:error:"), descriptor, out _);
        if (archive == IntPtr.Zero && existed)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            ObjC.SendVoid(descriptor, Sel("setUrl:"), IntPtr.Zero);
            archive = ObjC.Send(_device, Sel("newBinaryArchiveWithDescriptor:error:"), descriptor, out _);
            existed = false;
        }
        return (archive, archive == IntPtr.Zero ? string.Empty : path, existed);
    }

    /// <summary>First launch only: persists the archive holding the whole fixed registry.</summary>
    private void SerializeBinaryArchive()
    {
        if (_binaryArchive == IntPtr.Zero || _binaryArchiveExisted || _binaryArchivePath.Length == 0) return;
        ObjC.SendBool(_binaryArchive, Sel("serializeToURL:error:"), FileUrl(_binaryArchivePath), out _);
    }

    private static IntPtr FileUrl(string path) =>
        ObjC.Send(ObjC.objc_getClass("NSURL"), Sel("fileURLWithPath:"), ObjC.NSString(path));

    private static byte[]? ReadResource(string name)
    {
        using var stream = typeof(MetalDevice).Assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Loads the offline-compiled metallib bytes (dispatch_data with the DEFAULT
    /// destructor — libdispatch copies the buffer). Zero = fall back to source compilation.</summary>
    private IntPtr LoadLibraryFromData(byte[] bytes)
    {
        unsafe
        {
            fixed (byte* buffer = bytes)
            {
                var data = ObjC.dispatch_data_create((IntPtr)buffer, (nuint)bytes.Length, IntPtr.Zero, IntPtr.Zero);
                if (data == IntPtr.Zero) return IntPtr.Zero;
                return ObjC.Send(_device, Sel("newLibraryWithData:error:"), data, out _);
            }
        }
    }

    internal IntPtr Sampler(RhiFilter filter) => filter == RhiFilter.Linear ? _linearSampler : _nearestSampler;

    private IntPtr CreateSampler(bool linear)
    {
        var descriptor = ObjC.Send(ObjC.Send(ObjC.objc_getClass("MTLSamplerDescriptor"), Sel("alloc")), Sel("init"));
        var filter = linear ? 1ul : 0ul;           // MTLSamplerMinMagFilter
        const ulong clampToEdge = 0;               // MTLSamplerAddressModeClampToEdge
        ObjC.SendVoid(descriptor, Sel("setMinFilter:"), filter);
        ObjC.SendVoid(descriptor, Sel("setMagFilter:"), filter);
        ObjC.SendVoid(descriptor, Sel("setSAddressMode:"), clampToEdge);
        ObjC.SendVoid(descriptor, Sel("setTAddressMode:"), clampToEdge);
        var state = ObjC.Send(_device, Sel("newSamplerStateWithDescriptor:"), descriptor);
        if (state == IntPtr.Zero) throw new InvalidOperationException("Metal sampler creation failed.");
        return state;
    }

    private static IntPtr Sel(string name) => ObjC.sel_registerName(name);

    private static string DescribeError(IntPtr nsError)
    {
        if (nsError == IntPtr.Zero) return "(no NSError)";
        var description = ObjC.Send(nsError, ObjC.sel_registerName("localizedDescription"));
        return ObjC.NSStringToManaged(description) ?? "(unreadable NSError)";
    }

    public void Dispose()
    {
        // Objective-C objects leak per-process (no autorelease pool / retain-release management
        // yet) — the binding layer decision (plan open question 2) owns lifetime handling.
    }
}
