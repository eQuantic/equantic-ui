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
        MetalDevice.IsApple && ObjC.MTLCreateSystemDefaultDevice() != IntPtr.Zero;

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
    /// Shell path: draws the display list into a WINDOW drawable's texture and presents it on
    /// commit, through the same phases <see cref="Render"/> runs (#1952): its layers and its
    /// backdrops draw into the renderer's own targets first, and only the last pass draws into the
    /// drawable. That pass is presented ASYNCHRONOUSLY — the CAMetalLayer's drawable pool provides
    /// the backpressure (nextDrawable blocks when the queue is full). Offscreen renders (goldens,
    /// parity readbacks) go through <see cref="Render"/> and wait: the caller reads pixels right after.
    /// </summary>
    public void RenderToDrawable(DisplayList displayList, IntPtr drawableTexture, ulong pixelFormat, IntPtr drawable) =>
        _renderer.Render(displayList, new MetalDrawableFrame(_device, drawableTexture, pixelFormat, drawable));

    public void Dispose()
    {
        _renderer.Dispose();
        _device.Dispose();
    }
}
