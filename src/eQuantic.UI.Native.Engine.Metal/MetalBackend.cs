using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// The Photon Metal backend: the coarse <see cref="IRenderBackend"/> adapter over
/// <see cref="MetalDevice"/> (the Metal RHI implementation) and the SHARED
/// <see cref="RhiRenderer"/> encode loop — the same loop the Vulkan backend runs, so the two GPU
/// backends can only differ in API calls, never in frame semantics. Shaders come precompiled
/// (offline metallib, plan D3); pipelines are the fixed registry (plan D5).
/// </summary>
public sealed class MetalBackend : IRenderBackend
{
    private readonly MetalDevice _device;
    private readonly RhiRenderer _renderer;

    /// <summary>True when a Metal device exists (Apple hardware; CI without GPU skips).</summary>
    public static bool IsSupported =>
        OperatingSystem.IsMacOS() && ObjC.MTLCreateSystemDefaultDevice() != IntPtr.Zero;

    public MetalBackend()
    {
        _device = new MetalDevice();
        _renderer = new RhiRenderer(_device);
    }

    public RenderBackendKind Kind => RenderBackendKind.Metal;

    /// <summary>MTLPixelFormatBGRA8Unorm_sRGB — the CAMetalLayer window format (shell path).</summary>
    public const ulong PixelFormatBgra8UnormSrgb = MetalDevice.PixelFormatBgra8UnormSrgb;

    /// <summary>The MTLDevice handle — the shell hands it to the window's CAMetalLayer.</summary>
    public IntPtr DeviceHandle => _device.Handle;

    public IRenderSurface CreateSurface(int width, int height) =>
        new RhiSurface(_device.CreateRenderTarget(width, height));

    public void Render(DisplayList displayList, IRenderSurface surface) =>
        _renderer.Render(displayList, ((RhiSurface)surface).Target);

    /// <summary>
    /// Shell path: encode the display list straight into a WINDOW drawable's texture and present
    /// it on commit. Presents are ASYNCHRONOUS — the CAMetalLayer's drawable pool provides the
    /// backpressure (nextDrawable blocks when the queue is full), so the CPU never stalls on the
    /// GPU. Offscreen renders (goldens, parity readbacks) go through <see cref="Render"/> and
    /// wait: the caller reads pixels right after.
    /// </summary>
    public void RenderToDrawable(DisplayList displayList, IntPtr drawableTexture, ulong pixelFormat, IntPtr drawable)
    {
        var commands = _device.BeginDrawable(drawableTexture, pixelFormat, drawable, RhiRenderer.ClearColorOf(displayList));
        _renderer.Encode(displayList, commands);
        commands.Submit(waitUntilCompleted: false);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _device.Dispose();
    }
}
