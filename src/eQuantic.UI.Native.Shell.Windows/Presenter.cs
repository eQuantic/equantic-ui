using System.Runtime.InteropServices;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Vulkan;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// What puts a display list on the window. Two of them, chosen once at startup and invisible above:
/// VULKAN through a Win32 surface where a driver exposes one, and the NORMATIVE Reference backend
/// blitted through GDI where none does — a virtual machine, a remote desktop session, a machine
/// whose driver has no Vulkan ICD. Correct by definition, slower, and the same display list either
/// way, exactly as the Android shell falls back.
/// </summary>
internal interface IPresenter : IDisposable
{
    /// <summary>The name the self-test prints, so a screenshot is never mistaken for the other path.</summary>
    string Name { get; }

    /// <summary>The window's client area changed shape, in device pixels.</summary>
    void Resize(int width, int height);

    /// <summary>Draws one frame. <paramref name="hdc"/> is the paint DC when called from
    /// <c>WM_PAINT</c>, zero otherwise; only the software path cares.</summary>
    void Present(DisplayList displayList, IntPtr hdc);
}

/// <summary>
/// The GPU path: a swapchain over the HWND, FIFO-presented (vsync), through the same RhiRenderer the
/// Metal backend runs. A present that answers false means the swapchain no longer matches the window
/// — a resize raced the frame — and is a fact about the window, not a failure: the swapchain is
/// rebuilt and the next frame lands in it.
/// </summary>
internal sealed class VulkanPresenter : IPresenter
{
    private readonly VulkanBackend _backend;
    private readonly IntPtr _hinstance;
    private readonly IntPtr _hwnd;
    private VulkanSwapchain? _swapchain;
    private int _width;
    private int _height;

    public VulkanPresenter(IntPtr hinstance, IntPtr hwnd, int width, int height)
    {
        _backend = new VulkanBackend();
        _hinstance = hinstance;
        _hwnd = hwnd;
        Resize(width, height);
    }

    public string Name => "Vulkan";

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _width = width;
        _height = height;
        if (_swapchain is null) _swapchain = _backend.CreateWin32Swapchain(_hinstance, _hwnd, width, height);
        else _swapchain.Resize(width, height);
    }

    public void Present(DisplayList displayList, IntPtr hdc)
    {
        if (_swapchain is null) return;
        if (!_backend.Present(displayList, _swapchain)) _swapchain.Resize(_width, _height);
    }

    public void Dispose()
    {
        _swapchain?.Dispose();
        _backend.Dispose();
    }
}

/// <summary>
/// The software path: the Reference backend renders into a reusable CPU surface, the surface is read
/// back as straight sRGB and swizzled into the BGRA that GDI wants, and one <c>SetDIBitsToDevice</c>
/// puts the whole frame on the window. The surface is kept between frames — sixteen bytes a pixel of
/// linear float colour is not something to allocate at 60 Hz — and reallocated only on resize.
/// </summary>
internal sealed unsafe class SoftwarePresenter : IPresenter
{
    private readonly IntPtr _hwnd;
    // Every core the machine has: the golden harness keeps this backend single-threaded to stay the
    // simple rasterizer it was written as, but a window is not a golden — a frame that takes a
    // second on one core takes an eighth of it on eight, and the pixels are the same either way.
    private readonly ReferenceBackend _backend = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    private IRenderSurface? _surface;
    private byte[] _rgba = [];
    private byte[] _bgra = [];
    private int _width;
    private int _height;

    public SoftwarePresenter(IntPtr hwnd, int width, int height)
    {
        _hwnd = hwnd;
        Resize(width, height);
    }

    public string Name => "the Reference backend (no Vulkan driver)";

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == _width && height == _height && _surface is not null) return;
        _surface?.Dispose();
        _width = width;
        _height = height;
        _surface = _backend.CreateSurface(width, height);
        _rgba = new byte[width * height * 4];
        _bgra = new byte[width * height * 4];
    }

    public void Present(DisplayList displayList, IntPtr hdc)
    {
        if (_surface is null) return;
        _backend.Render(displayList, _surface);
        _surface.ReadPixelsSrgb(_rgba);

        // The engine speaks RGBA; a Windows DIB wants BGRA. The window is opaque (the theme clears
        // every pixel), so the alpha byte is carried but never read.
        var source = _rgba;
        var destination = _bgra;
        for (var i = 0; i < source.Length; i += 4)
        {
            destination[i] = source[i + 2];
            destination[i + 1] = source[i + 1];
            destination[i + 2] = source[i];
            destination[i + 3] = source[i + 3];
        }

        var borrowed = hdc == IntPtr.Zero;
        var dc = borrowed ? GetDC(_hwnd) : hdc;
        if (dc == IntPtr.Zero) return;
        try
        {
            var header = new BITMAPINFOHEADER
            {
                Size = (uint)sizeof(BITMAPINFOHEADER),
                Width = _width,
                Height = -_height,          // negative: top-down rows, the order the engine produced
                Planes = 1,
                BitCount = 32,
                Compression = 0,            // BI_RGB
            };
            fixed (byte* bits = destination)
            {
                SetDIBitsToDevice(dc, 0, 0, (uint)_width, (uint)_height, 0, 0, 0, (uint)_height, bits, &header, 0);
            }
        }
        finally
        {
            if (borrowed) ReleaseDC(_hwnd, dc);
        }
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _backend.Dispose();
    }
}
