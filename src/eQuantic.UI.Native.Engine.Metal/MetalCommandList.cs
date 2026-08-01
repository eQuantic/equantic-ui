namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// One Metal render pass as an <see cref="IRhiCommandList"/>: pipeline selection resolves against
/// the pass target's pixel format through the device's fixed registry, uniforms travel inline via
/// <c>setFragmentBytes</c> (160 bytes — comfortably under Metal's 4 KB inline limit), and every
/// draw is the fullscreen triangle. Submitting a drawable pass presents on commit.
/// </summary>
internal sealed class MetalCommandList : IRhiCommandList
{
    private const ulong PrimitiveTypeTriangle = 3;

    private readonly MetalDevice _device;
    private readonly IntPtr _commandBuffer;
    private readonly IntPtr _encoder;
    private readonly ulong _pixelFormat;
    private readonly IntPtr _drawable;

    internal MetalCommandList(MetalDevice device, IntPtr commandBuffer, IntPtr encoder, ulong pixelFormat, IntPtr drawable)
    {
        _device = device;
        _commandBuffer = commandBuffer;
        _encoder = encoder;
        _pixelFormat = pixelFormat;
        _drawable = drawable;
    }

    public void SetPipeline(RhiPipelineKind kind) =>
        ObjC.SendVoid(_encoder, Sel("setRenderPipelineState:"), _device.PipelineState(_pixelFormat, kind));

    public void BindTexture(int slot, IRhiTexture texture, RhiFilter filter = RhiFilter.Nearest)
    {
        ObjC.SendVoid(_encoder, Sel("setFragmentTexture:atIndex:"), ((IMetalBindable)texture).Texture, (ulong)slot);
        // ONE sampler slot serves every entry point (the shader declares a single SamplerState);
        // the filtered paths read through it, the Load paths never notice.
        ObjC.SendVoid(_encoder, Sel("setFragmentSamplerState:atIndex:"), _device.Sampler(filter), 0ul);
    }

    public void Draw(in DrawUniforms uniforms)
    {
        unsafe
        {
            fixed (DrawUniforms* bytes = &uniforms)
            {
                ObjC.SendVoid(_encoder, Sel("setFragmentBytes:length:atIndex:"),
                    (IntPtr)bytes, (ulong)sizeof(DrawUniforms), 0);
            }
        }
        ObjC.SendVoid(_encoder, Sel("drawPrimitives:vertexStart:vertexCount:"), PrimitiveTypeTriangle, 0, 3);
    }

    public void Submit(bool waitUntilCompleted)
    {
        ObjC.SendVoid(_encoder, Sel("endEncoding"));
        if (_drawable != IntPtr.Zero)
            ObjC.SendVoid(_commandBuffer, Sel("presentDrawable:"), _drawable);
        ObjC.SendVoid(_commandBuffer, Sel("commit"));
        if (waitUntilCompleted)
            ObjC.SendVoid(_commandBuffer, Sel("waitUntilCompleted"));
    }

    private static IntPtr Sel(string name) => ObjC.sel_registerName(name);
}
