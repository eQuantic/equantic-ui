using eQuantic.UI.Primitives;
namespace eQuantic.UI.Native.Engine;

public enum RenderBackendKind : byte
{
    /// <summary>Scalar CPU rasterizer — golden-test infrastructure only, never a product tier (plan D7).</summary>
    Reference = 0,
    Metal = 1,
    Vulkan = 2,
}

/// <summary>
/// The engine-facing seam a rendering backend implements: create surfaces, render display lists into
/// them. This is deliberately the COARSE contract — the fine-grained RHI underneath the GPU backends
/// (plan W1) lives in <c>Rhi.cs</c> (<see cref="IRhiDevice"/>/<see cref="IRhiCommandList"/>/…,
/// extracted 2026-07 from the proven Metal spike shape); GPU backends implement it and adapt to this
/// seam through <see cref="RhiSurface"/> + <see cref="RhiRenderer"/>. The Reference backend stays a
/// direct implementation — a CPU rasterizer has no device to abstract.
/// </summary>
public interface IRenderBackend : IDisposable
{
    RenderBackendKind Kind { get; }

    IRenderSurface CreateSurface(int width, int height);

    /// <summary>Renders <paramref name="displayList"/> into <paramref name="surface"/> (full redraw; damage tracking is a compositor concern).</summary>
    void Render(DisplayList displayList, IRenderSurface surface);
}

/// <summary>A render target the backend can draw into and the harness can read back.</summary>
public interface IRenderSurface : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Reads the surface back as tightly-packed 8-bit sRGB RGBA scanlines (straight alpha, top-down)
    /// — the golden-image interchange format. GPU backends implement this with a readback blit;
    /// it is a test/tooling path, never a frame-loop path.
    /// </summary>
    void ReadPixelsSrgb(Span<byte> destinationRgba);
}
