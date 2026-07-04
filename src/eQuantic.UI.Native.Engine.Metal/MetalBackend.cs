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

        // Compile the spike shader and build the ONE pipeline (premultiplied src-over, sRGB target).
        var library = ObjC.Send(_device, Sel("newLibraryWithSource:options:error:"),
            ObjC.NSString(MetalShaders.Source), IntPtr.Zero, out var libraryError);
        if (library == IntPtr.Zero)
            throw new InvalidOperationException(
                $"MSL compilation failed: {DescribeError(libraryError)}");

        var vertexFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("fullscreen_vertex"));
        var fragmentFn = ObjC.Send(library, Sel("newFunctionWithName:"), ObjC.NSString("sdf_fragment"));

        var descriptor = ObjC.Send(ObjC.Send(ObjC.objc_getClass("MTLRenderPipelineDescriptor"), Sel("alloc")), Sel("init"));
        ObjC.SendVoid(descriptor, Sel("setVertexFunction:"), vertexFn);
        ObjC.SendVoid(descriptor, Sel("setFragmentFunction:"), fragmentFn);

        var attachment = ObjC.Send(ObjC.Send(descriptor, Sel("colorAttachments")), Sel("objectAtIndexedSubscript:"), 0ul);
        ObjC.SendVoid(attachment, Sel("setPixelFormat:"), PixelFormatRgba8UnormSrgb);
        ObjC.SendVoid(attachment, Sel("setBlendingEnabled:"), true);
        // Premultiplied source-over: dst' = src + dst · (1 − src.A) — LinearColor.Over, in hardware.
        const ulong factorOne = 1;                    // MTLBlendFactorOne
        const ulong factorOneMinusSourceAlpha = 5;    // MTLBlendFactorOneMinusSourceAlpha
        ObjC.SendVoid(attachment, Sel("setSourceRGBBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setSourceAlphaBlendFactor:"), factorOne);
        ObjC.SendVoid(attachment, Sel("setDestinationRGBBlendFactor:"), factorOneMinusSourceAlpha);
        ObjC.SendVoid(attachment, Sel("setDestinationAlphaBlendFactor:"), factorOneMinusSourceAlpha);

        _pipeline = ObjC.Send(_device, Sel("newRenderPipelineStateWithDescriptor:error:"),
            descriptor, out var pipelineError);
        if (_pipeline == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Pipeline creation failed: {DescribeError(pipelineError)}");
    }

    public RenderBackendKind Kind => RenderBackendKind.Metal;

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
        ObjC.SendVoid(attachment, Sel("setTexture:"), target.Texture);
        ObjC.SendVoid(attachment, Sel("setLoadAction:"), LoadActionClear);
        ObjC.SendVoid(attachment, Sel("setStoreAction:"), StoreActionStore);
        // Clear colors are written pre-encoding (linear) — mirror ColorSpace.ToPremultipliedLinear.
        var linear = ColorSpace.ToPremultipliedLinear(clearColor);
        ObjC.SendVoid(attachment, Sel("setClearColor:"),
            new MTLClearColor { Red = linear.R, Green = linear.G, Blue = linear.B, Alpha = linear.A });

        var commandBuffer = ObjC.Send(_queue, Sel("commandBuffer"));
        var encoder = ObjC.Send(commandBuffer, Sel("renderCommandEncoderWithDescriptor:"), passDescriptor);
        ObjC.SendVoid(encoder, Sel("setRenderPipelineState:"), _pipeline);

        for (var i = firstDraw; i < commands.Length; i++)
        {
            ref readonly var command = ref commands[i];
            if (command.Kind == DrawCommandKind.Clear) continue; // leading clears only (spike fence)
            if (!TryBuildUniforms(in command, out var uniforms)) continue;

            unsafe
            {
                ObjC.SendVoid(encoder, Sel("setFragmentBytes:length:atIndex:"),
                    (IntPtr)(&uniforms), (ulong)sizeof(DrawUniforms), 0);
            }
            ObjC.SendVoid(encoder, Sel("drawPrimitives:vertexStart:vertexCount:"), PrimitiveTypeTriangle, 0, 3);
        }

        ObjC.SendVoid(encoder, Sel("endEncoding"));
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
            command.Kind == DrawCommandKind.StrokeRRect ? 1 : 0,
            paint.Kind == PaintKind.LinearGradient ? 1 : 0,
            0, 0);
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

/// <summary>Matches the MSL <c>DrawUniforms</c> layout (eight float4s, 128 bytes).</summary>
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
}
