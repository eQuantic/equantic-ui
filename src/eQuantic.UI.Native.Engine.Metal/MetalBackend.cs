using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// The Photon Metal backend (M0 spike): renders display lists into offscreen
/// <c>RGBA8Unorm_sRGB</c> textures on the system GPU. One render pass per frame, one pipeline built
/// once at device init (runtime-compiled MSL for the spike — the offline Slang toolchain replaces
/// that, plan D3), one fullscreen-triangle draw per command with the SDF evaluated in the fragment —
/// the same sampling model as the Reference backend, so the two are pixel-comparable.
/// </summary>
public sealed class MetalBackend : IRenderBackend
{
    private const ulong PixelFormatRgba8UnormSrgb = 71; // MTLPixelFormatRGBA8Unorm_sRGB
    private const ulong LoadActionClear = 2;
    private const ulong StoreActionStore = 1;
    private const ulong PrimitiveTypeTriangle = 3;
    private const ulong TextureUsageRenderTarget = 0x04;
    private const ulong StorageModeShared = 0;

    private readonly IntPtr _device;
    private readonly IntPtr _queue;
    private readonly IntPtr _pipeline;
    private readonly IntPtr _vertexFn;
    private readonly IntPtr _fragmentFn;
    private readonly IntPtr _texturedFn;
    private readonly Dictionary<(ulong Format, bool Textured), IntPtr> _pipelines = new();
    private readonly Dictionary<TextureData, IntPtr> _textures = new();
    private IntPtr _dummyTexture;

    /// <summary>True when a Metal device exists (Apple hardware; CI without GPU skips).</summary>
    public static bool IsSupported =>
        OperatingSystem.IsMacOS() && ObjC.MTLCreateSystemDefaultDevice() != IntPtr.Zero;

    public MetalBackend()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The Metal backend requires macOS/Apple hardware.");

        _device = ObjC.MTLCreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
            throw new PlatformNotSupportedException("No Metal device available.");

        _queue = ObjC.Send(_device, Sel("newCommandQueue"));

        // Compile the spike shader once; pipelines are built PER TARGET FORMAT (offscreen
        // surfaces are RGBA8_sRGB; a window's CAMetalLayer only offers BGRA8 variants).
        var library = ObjC.Send(_device, Sel("newLibraryWithSource:options:error:"),
            ObjC.NSString(MetalShaders.Source), IntPtr.Zero, out var libraryError);
        if (library == IntPtr.Zero)
            throw new InvalidOperationException(
                $"MSL compilation failed: {DescribeError(libraryError)}");

        _vertexFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("fullscreen_vertex"));
        _fragmentFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("sdf_fragment"));
        _texturedFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("textured_fragment"));
        _pipeline = PipelineFor(PixelFormatRgba8UnormSrgb);
    }

    /// <summary>MTLPixelFormatBGRA8Unorm_sRGB — the CAMetalLayer window format (shell path).</summary>
    public const ulong PixelFormatBgra8UnormSrgb = 81;

    private const ulong PixelFormatR8Unorm = 10;

    /// <summary>Uploads (and caches by IDENTITY — the raster caches reuse instances) an A8
    /// coverage raster as an R8 texture. Per-process lifetime (the documented spike leak fence).</summary>
    private IntPtr TextureFor(TextureData data)
    {
        if (_textures.TryGetValue(data, out var cached)) return cached;
        var descriptor = ObjC.Send(ObjC.objc_getClass("MTLTextureDescriptor"),
            Sel("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            PixelFormatR8Unorm, (ulong)data.Width, (ulong)data.Height, false);
        ObjC.SendVoid(descriptor, Sel("setStorageMode:"), StorageModeShared);
        var texture = ObjC.Send(_device, Sel("newTextureWithDescriptor:"), descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException("Metal texture creation failed.");
        unsafe
        {
            fixed (byte* bytes = data.Alpha)
            {
                ObjC.SendVoid(texture, Sel("replaceRegion:mipmapLevel:withBytes:bytesPerRow:"),
                    new MTLRegion { Width = (ulong)data.Width, Height = (ulong)data.Height, Depth = 1 },
                    0, (IntPtr)bytes, (ulong)data.Width);
            }
        }
        _textures[data] = texture;
        return texture;
    }

    /// <summary>The slangc-generated entry points share ONE texture binding — a 1×1 dummy keeps
    /// the SDF pipeline valid under strict API validation.</summary>
    private IntPtr DummyTexture() => _dummyTexture != IntPtr.Zero
        ? _dummyTexture
        : _dummyTexture = TextureFor(new TextureData(1, 1, new byte[] { 0 }));

    private IntPtr PipelineFor(ulong pixelFormat, bool textured = false)
    {
        if (_pipelines.TryGetValue((pixelFormat, textured), out var cached)) return cached;

        var descriptor = ObjC.Send(ObjC.Send(ObjC.objc_getClass("MTLRenderPipelineDescriptor"), Sel("alloc")), Sel("init"));
        ObjC.SendVoid(descriptor, Sel("setVertexFunction:"), _vertexFn);
        ObjC.SendVoid(descriptor, Sel("setFragmentFunction:"), textured ? _texturedFn : _fragmentFn);

        var attachment = ObjC.Send(ObjC.Send(descriptor, Sel("colorAttachments")), Sel("objectAtIndexedSubscript:"), 0ul);
        ObjC.SendVoid(attachment, Sel("setPixelFormat:"), pixelFormat);
        ObjC.SendVoid(attachment, Sel("setBlendingEnabled:"), true);
        // Premultiplied source-over: dst' = src + dst · (1 − src.A) — LinearColor.Over, in hardware.
        const ulong factorOne = 1;                    // MTLBlendFactorOne
        const ulong factorOneMinusSourceAlpha = 5;    // MTLBlendFactorOneMinusSourceAlpha
        ObjC.SendVoid(attachment, Sel("setSourceRGBBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setSourceAlphaBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setDestinationRGBBlendFactor:"), factorOneMinusSourceAlpha);
        ObjC.SendVoid(attachment, Sel("setDestinationAlphaBlendFactor:"), factorOneMinusSourceAlpha);

        var pipeline = ObjC.Send(_device, Sel("newRenderPipelineStateWithDescriptor:error:"),
            descriptor, out var pipelineError);
        if (pipeline == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Pipeline creation failed: {DescribeError(pipelineError)}");
        _pipelines[(pixelFormat, textured)] = pipeline;
        return pipeline;
    }

    public RenderBackendKind Kind => RenderBackendKind.Metal;

    /// <summary>The MTLDevice handle — the shell hands it to the window's CAMetalLayer.</summary>
    public IntPtr DeviceHandle => _device;

    public IRenderSurface CreateSurface(int width, int height)
    {
        var descriptor = ObjC.Send(ObjC.objc_getClass("MTLTextureDescriptor"),
            Sel("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            PixelFormatRgba8UnormSrgb, (ulong)width, (ulong)height, false);
        ObjC.SendVoid(descriptor, Sel("setUsage:"), TextureUsageRenderTarget);
        ObjC.SendVoid(descriptor, Sel("setStorageMode:"), StorageModeShared);

        var texture = ObjC.Send(_device, Sel("newTextureWithDescriptor:"), descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException("Metal texture creation failed.");
        return new MetalSurface(texture, width, height);
    }

    public void Render(DisplayList displayList, IRenderSurface surface)
    {
        var target = (MetalSurface)surface;
        RenderCore(displayList, target.Texture, PixelFormatRgba8UnormSrgb, IntPtr.Zero);
    }

    /// <summary>
    /// Shell path: encode the display list straight into a WINDOW drawable's texture and present
    /// it on commit. The pipeline is resolved (and cached) for the layer's pixel format — the
    /// shaders are format-agnostic; the write swizzle is the hardware's.
    /// </summary>
    public void RenderToDrawable(DisplayList displayList, IntPtr drawableTexture, ulong pixelFormat, IntPtr drawable)
        => RenderCore(displayList, drawableTexture, pixelFormat, drawable);

    private void RenderCore(DisplayList displayList, IntPtr targetTexture, ulong pixelFormat, IntPtr presentDrawable)
    {
        var pipeline = PipelineFor(pixelFormat);
        var texturedPipeline = PipelineFor(pixelFormat, textured: true);
        var texturedActive = false;

        // The pass clears to the display list's leading Clear command (ours always start with one).
        var clearColor = Color.Transparent;
        var firstDraw = 0;
        var commands = displayList.Commands;
        if (commands.Length > 0 && commands[0].Kind == DrawCommandKind.Clear)
        {
            clearColor = commands[0].Paint.Color;
            firstDraw = 1;
        }

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
        ObjC.SendVoid(encoder, Sel("setRenderPipelineState:"), pipeline);
        // The generated entry points share one texture binding — keep it valid for the SDF path.
        ObjC.SendVoid(encoder, Sel("setFragmentTexture:atIndex:"), DummyTexture(), 0ul);

        // SPIKE FENCE: group-opacity layers approximate as per-command alpha (no offscreen pass in
        // the M0 spike) — overlapping children inside a layer double-blend here; the reference
        // backend is normative and the D3 pipeline brings the real offscreen composite.
        var layerAlpha = 1f;
        Stack<float>? layerStack = null;

        for (var i = firstDraw; i < commands.Length; i++)
        {
            ref readonly var command = ref commands[i];
            if (command.Kind == DrawCommandKind.Clear) continue; // leading clears only (spike fence)
            // W4b: a Texture command switches to the textured pipeline (A8 coverage × tint,
            // Load-based nearest — exact Reference parity), binds the uploaded raster, and reuses
            // the SDF uniforms with the texture size riding the gradient slot.
            if (command.Kind == DrawCommandKind.Texture)
            {
                if (command.TextureId < 0 || command.TextureId >= displayList.Textures.Count) continue;
                var data = displayList.Textures[command.TextureId];
                if (!TryBuildUniforms(in command, out var textured)) continue;
                if (layerAlpha < 1f) textured.ColorA.W *= layerAlpha;
                textured.Gradient = new Float4(data.Width, data.Height, 0, 0);
                textured.Flags = new Float4(0, 0, command.Clip is null ? 0 : 1, 0);

                if (!texturedActive)
                {
                    ObjC.SendVoid(encoder, Sel("setRenderPipelineState:"), texturedPipeline);
                    texturedActive = true;
                }
                ObjC.SendVoid(encoder, Sel("setFragmentTexture:atIndex:"), TextureFor(data), 0ul);
                unsafe
                {
                    ObjC.SendVoid(encoder, Sel("setFragmentBytes:length:atIndex:"),
                        (IntPtr)(&textured), (ulong)sizeof(DrawUniforms), 0);
                }
                ObjC.SendVoid(encoder, Sel("drawPrimitives:vertexStart:vertexCount:"), PrimitiveTypeTriangle, 0, 3);
                continue;
            }
            if (texturedActive)
            {
                ObjC.SendVoid(encoder, Sel("setRenderPipelineState:"), pipeline);
                texturedActive = false;
            }
            if (command.Kind == DrawCommandKind.BeginLayer)
            {
                (layerStack ??= new()).Push(layerAlpha);
                layerAlpha *= command.StrokeWidth;
                continue;
            }
            if (command.Kind == DrawCommandKind.EndLayer)
            {
                layerAlpha = layerStack!.Pop();
                continue;
            }
            if (!TryBuildUniforms(in command, out var uniforms)) continue;
            if (layerAlpha < 1f)
            {
                uniforms.ColorA.W *= layerAlpha;
                uniforms.ColorB.W *= layerAlpha;
            }

            unsafe
            {
                ObjC.SendVoid(encoder, Sel("setFragmentBytes:length:atIndex:"),
                    (IntPtr)(&uniforms), (ulong)sizeof(DrawUniforms), 0);
            }
            ObjC.SendVoid(encoder, Sel("drawPrimitives:vertexStart:vertexCount:"), PrimitiveTypeTriangle, 0, 3);
        }

        ObjC.SendVoid(encoder, Sel("endEncoding"));
        if (presentDrawable != IntPtr.Zero)
            ObjC.SendVoid(commandBuffer, Sel("presentDrawable:"), presentDrawable);
        ObjC.SendVoid(commandBuffer, Sel("commit"));
        ObjC.SendVoid(commandBuffer, Sel("waitUntilCompleted"));
    }

    private static bool TryBuildUniforms(in DrawCommand command, out DrawUniforms uniforms)
    {
        uniforms = default;
        var inverse = command.Transform.Invert();
        if (inverse is null) return false;
        var inv = inverse.Value;
        var scale = command.Transform.AverageScale();
        if (scale <= 0) return false;

        var shape = command.Shape;
        var paint = command.Paint;

        uniforms.Inv0 = new Float4(inv.M11, inv.M21, inv.M31, scale);
        uniforms.Inv1 = new Float4(inv.M12, inv.M22, inv.M32, command.StrokeWidth);
        uniforms.Rect = new Float4(shape.Rect.Center.X, shape.Rect.Center.Y, shape.Rect.Width / 2, shape.Rect.Height / 2);
        uniforms.Radii = new Float4(shape.Radii.TopLeft, shape.Radii.TopRight, shape.Radii.BottomRight, shape.Radii.BottomLeft);
        uniforms.ColorA = ToFloat4(paint.Color);
        uniforms.ColorB = ToFloat4(paint.EndColor);
        uniforms.Gradient = new Float4(paint.GradientStart.X, paint.GradientStart.Y, paint.GradientEnd.X, paint.GradientEnd.Y);
        uniforms.Flags = new Float4(
            command.Kind switch { DrawCommandKind.StrokeRRect => 1, DrawCommandKind.ShadowRRect => 2, _ => 0 },
            paint.Kind == PaintKind.LinearGradient ? 1 : 0,
            command.Clip is null ? 0 : 1, 0);
        if (command.Clip is { } clip)
        {
            uniforms.ClipRect = new Float4(clip.Rect.Center.X, clip.Rect.Center.Y, clip.Rect.Width / 2, clip.Rect.Height / 2);
            uniforms.ClipRadii = new Float4(clip.Radii.TopLeft, clip.Radii.TopRight, clip.Radii.BottomRight, clip.Radii.BottomLeft);
        }
        return true;
    }

    private static Float4 ToFloat4(Color color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static IntPtr Sel(string name) => ObjC.sel_registerName(name);

    private static string DescribeError(IntPtr nsError)
    {
        if (nsError == IntPtr.Zero) return "(no NSError)";
        var description = ObjC.Send(nsError, ObjC.sel_registerName("localizedDescription"));
        return ObjC.NSStringToManaged(description) ?? "(unreadable NSError)";
    }

    public void Dispose()
    {
        // Spike: ObjC objects leak-per-process (no autorelease pool management yet) — the binding
        // layer decision (open question 2) owns lifetime handling for the real backend.
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Float4
{
    public float X, Y, Z, W;

    public Float4(float x, float y, float z, float w)
    {
        X = x; Y = y; Z = z; W = w;
    }
}

/// <summary>Matches the MSL <c>DrawUniforms</c> layout (ten float4s, 160 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DrawUniforms
{
    public Float4 Inv0;
    public Float4 Inv1;
    public Float4 Rect;
    public Float4 Radii;
    public Float4 ColorA;
    public Float4 ColorB;
    public Float4 Gradient;
    public Float4 Flags;
    public Float4 ClipRect;
    public Float4 ClipRadii;
}
